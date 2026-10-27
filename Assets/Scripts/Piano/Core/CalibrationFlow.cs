using System.Collections.Generic;

namespace PianoGlass.Piano
{
    public enum FlowState
    {
        /// <summary>Reading the profile file. Nothing drawn.</summary>
        Launch,
        /// <summary>Touch two keys; then a new anchor is created and saved.
        /// Nothing drawn: there is no trustworthy placement yet.</summary>
        FullCalibration,
        /// <summary>Finding the saved anchor. Nothing drawn.</summary>
        Localizing,
        /// <summary>Overlay drawn; the player checks it and presses A.</summary>
        Verifying,
        /// <summary>Overlay drawn; Middle C or two-key correction in progress.</summary>
        Correcting,
        /// <summary>Overlay drawn, transport unlocked.</summary>
        Ready,
        /// <summary>Anchor untracked. Nothing drawn until it comes back.</summary>
        Suspended,
    }

    public enum FlowEvent
    {
        ProfileValid,
        ProfileMissingOrInvalid,
        LocalizeSucceeded,
        LocalizeTimedOut,
        LocalizeFailed,
        /// <summary>The load succeeded and returned nothing: the anchor is
        /// gone. Distinct from a failure -- there is nothing to retry.</summary>
        AnchorNotFound,
        RetryRequested,
        ConfirmPressed,
        CorrectionStarted,
        CorrectionApplied,
        /// <summary>The touched key was over the "moved" threshold away.</summary>
        CorrectionRejectedMoved,
        CorrectionCancelled,
        RecalibrateRequested,
        CaptureCompleted,
        NewAnchorSaved,
        NewAnchorSaveFailed,
        TrackingLost,
        TrackingRegained,
    }

    /// <summary>Side effects for the MonoBehaviour driver to carry out, in
    /// list order. The flow itself does no I/O.</summary>
    public enum FlowCommand
    {
        LoadAndLocalizeAnchor,
        BeginCapture,
        CreateAndSaveAnchor,
        /// <summary>Always before EraseOldAnchor in the same transition: the
        /// new profile is on disk before the old anchor is touched.</summary>
        WriteProfile,
        EraseOldAnchor,
        MarkVerified,
        OfferRecalibrate,
    }

    public readonly struct FlowTransition
    {
        public readonly FlowState From;
        public readonly FlowState To;
        public readonly bool Handled;
        public readonly IReadOnlyList<FlowCommand> Commands;

        public FlowTransition(FlowState from, FlowState to, bool handled, IReadOnlyList<FlowCommand> commands)
        {
            From = from; To = to; Handled = handled; Commands = commands;
        }

        public bool Has(FlowCommand c)
        {
            if (Commands == null) return false;
            foreach (var x in Commands) if (x == c) return true;
            return false;
        }

        public override string ToString() =>
            $"{From} -> {To}" + (Commands != null && Commands.Count > 0 ? $" [{string.Join(", ", Commands)}]" : "");
    }

    /// <summary>
    /// The anchored-calibration state machine, as a pure transition function
    /// so every path can be tested without a scene or a headset.
    ///
    /// The invariants it exists to hold:
    ///   * Overlays are drawn only in Verifying, Correcting and Ready.
    ///   * No failure event ever yields EraseOldAnchor. A failed or slow
    ///     localization loops back with the profile untouched.
    ///   * The old anchor is erased only in the transition that reports the
    ///     new one saved, and only after WriteProfile.
    /// </summary>
    public sealed class CalibrationFlow
    {
        public const int DefaultMaxLocalizeAttempts = 3;

        public FlowState State { get; private set; } = FlowState.Launch;

        /// <summary>True once a valid profile has existed this session -- an
        /// old anchor that a recalibration must replace.</summary>
        public bool HasProfile { get; private set; }

        /// <summary>Failed localize attempts since the last success.</summary>
        public int LocalizeFailures { get; private set; }

        /// <summary>Set when the player should be offered a recalibration:
        /// localization keeps failing, or a correction said the piano moved.</summary>
        public bool RecalibrateOffered { get; private set; }

        /// <summary>Within FullCalibration: points captured, new anchor being saved.</summary>
        public bool SavingNewAnchor { get; private set; }

        private readonly int _maxLocalizeAttempts;
        private FlowState _resumeState = FlowState.Ready;
        /// <summary>Where a cancelled correction goes back to: a Middle C
        /// fix started from Ready and abandoned changed nothing, so it
        /// does not need confirming again.</summary>
        private FlowState _correctionReturn = FlowState.Verifying;

        public CalibrationFlow(int maxLocalizeAttempts = DefaultMaxLocalizeAttempts)
        {
            _maxLocalizeAttempts = maxLocalizeAttempts < 1 ? 1 : maxLocalizeAttempts;
        }

        public static bool OverlayVisible(FlowState s) =>
            s == FlowState.Verifying || s == FlowState.Correcting || s == FlowState.Ready;

        public bool IsOverlayVisible => OverlayVisible(State);

        public FlowTransition Handle(FlowEvent e)
        {
            FlowState from = State;
            var commands = new List<FlowCommand>();
            bool handled = Step(e, commands);
            return new FlowTransition(from, State, handled, commands);
        }

        private bool Step(FlowEvent e, List<FlowCommand> cmd)
        {
            // Recalibration is available from anywhere except mid-save, and
            // never touches the existing profile or anchor on the way in.
            if (e == FlowEvent.RecalibrateRequested)
            {
                if (State == FlowState.FullCalibration && SavingNewAnchor) return false;
                Go(FlowState.FullCalibration);
                SavingNewAnchor = false;
                RecalibrateOffered = false;
                cmd.Add(FlowCommand.BeginCapture);
                return true;
            }

            switch (State)
            {
                case FlowState.Launch:
                    if (e == FlowEvent.ProfileValid)
                    {
                        HasProfile = true;
                        StartLocalizing(cmd);
                        return true;
                    }
                    if (e == FlowEvent.ProfileMissingOrInvalid)
                    {
                        Go(FlowState.FullCalibration);
                        cmd.Add(FlowCommand.BeginCapture);
                        return true;
                    }
                    return false;

                case FlowState.Localizing:
                    switch (e)
                    {
                        case FlowEvent.LocalizeSucceeded:
                            LocalizeFailures = 0;
                            RecalibrateOffered = false;
                            Go(FlowState.Verifying);
                            return true;

                        case FlowEvent.LocalizeTimedOut:
                        case FlowEvent.LocalizeFailed:
                            LocalizeFailures++;
                            if (LocalizeFailures >= _maxLocalizeAttempts)
                            {
                                // Stop retrying on our own; the player
                                // chooses retry or recalibrate. The profile
                                // and its anchor are left exactly as they are.
                                RecalibrateOffered = true;
                                cmd.Add(FlowCommand.OfferRecalibrate);
                            }
                            else
                            {
                                cmd.Add(FlowCommand.LoadAndLocalizeAnchor);
                            }
                            return true;

                        case FlowEvent.RetryRequested:
                            if (!RecalibrateOffered) return false;
                            LocalizeFailures = 0;
                            RecalibrateOffered = false;
                            cmd.Add(FlowCommand.LoadAndLocalizeAnchor);
                            return true;

                        case FlowEvent.AnchorNotFound:
                            // Gone, not failing. Nothing to erase -- it does
                            // not exist -- and the profile stays on disk until
                            // a replacement saves.
                            Go(FlowState.FullCalibration);
                            cmd.Add(FlowCommand.BeginCapture);
                            return true;
                    }
                    return false;

                case FlowState.FullCalibration:
                    if (!SavingNewAnchor && e == FlowEvent.CaptureCompleted)
                    {
                        SavingNewAnchor = true;
                        cmd.Add(FlowCommand.CreateAndSaveAnchor);
                        return true;
                    }
                    if (SavingNewAnchor && e == FlowEvent.NewAnchorSaved)
                    {
                        SavingNewAnchor = false;
                        cmd.Add(FlowCommand.WriteProfile);
                        if (HasProfile) cmd.Add(FlowCommand.EraseOldAnchor);
                        HasProfile = true;
                        LocalizeFailures = 0;
                        RecalibrateOffered = false;
                        Go(FlowState.Verifying);
                        return true;
                    }
                    if (SavingNewAnchor && e == FlowEvent.NewAnchorSaveFailed)
                    {
                        SavingNewAnchor = false;
                        cmd.Add(FlowCommand.BeginCapture);
                        return true;
                    }
                    return false;

                case FlowState.Verifying:
                    switch (e)
                    {
                        case FlowEvent.ConfirmPressed:
                            RecalibrateOffered = false;
                            cmd.Add(FlowCommand.MarkVerified);
                            Go(FlowState.Ready);
                            return true;
                        case FlowEvent.CorrectionStarted:
                            StartCorrecting();
                            return true;
                        case FlowEvent.TrackingLost:
                            Suspend();
                            return true;
                    }
                    return false;

                case FlowState.Correcting:
                    switch (e)
                    {
                        case FlowEvent.CorrectionApplied:
                            cmd.Add(FlowCommand.WriteProfile);
                            Go(FlowState.Verifying);
                            return true;
                        case FlowEvent.CorrectionRejectedMoved:
                            RecalibrateOffered = true;
                            cmd.Add(FlowCommand.OfferRecalibrate);
                            Go(FlowState.Verifying);
                            return true;
                        case FlowEvent.CorrectionCancelled:
                            Go(_correctionReturn);
                            return true;
                        case FlowEvent.TrackingLost:
                            // A correction measured against a lost anchor is
                            // meaningless; resume at Verifying, not mid-touch.
                            _resumeState = FlowState.Verifying;
                            Go(FlowState.Suspended);
                            return true;
                    }
                    return false;

                case FlowState.Ready:
                    if (e == FlowEvent.TrackingLost) { Suspend(); return true; }
                    if (e == FlowEvent.CorrectionStarted) { StartCorrecting(); return true; }
                    return false;

                case FlowState.Suspended:
                    if (e == FlowEvent.TrackingRegained)
                    {
                        Go(_resumeState);
                        return true;
                    }
                    return false;
            }

            return false;
        }

        private void StartLocalizing(List<FlowCommand> cmd)
        {
            LocalizeFailures = 0;
            RecalibrateOffered = false;
            Go(FlowState.Localizing);
            cmd.Add(FlowCommand.LoadAndLocalizeAnchor);
        }

        /// <summary>An applied correction always comes back to Verifying --
        /// the placement changed, so it is checked again.</summary>
        private void StartCorrecting()
        {
            _correctionReturn = State;
            Go(FlowState.Correcting);
        }

        private void Suspend()
        {
            _resumeState = State;
            Go(FlowState.Suspended);
        }

        private void Go(FlowState next) => State = next;
    }
}
