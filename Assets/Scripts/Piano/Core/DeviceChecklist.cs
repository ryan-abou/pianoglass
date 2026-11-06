using System;
using System.Text;
using UnityEngine;

namespace PianoGlass.Piano
{
    public enum CheckResult { Pending, Passed, Failed }

    /// <summary>What DeviceChecklist keeps between app runs (step 4 needs a
    /// relaunch). Reset whenever the build changes.</summary>
    [Serializable]
    public sealed class DeviceChecklistState
    {
        public string build = "";
        /// <summary>Incremented every app run.</summary>
        public int run;
        public int[] results = new int[DeviceChecklist.Count];
        /// <summary>The run in which a Middle C fix was last applied; 0 = never.</summary>
        public int fixRun;
        public Vector3 fixErrorMm;
        public float cappedYMm;
        public float seatedYMm;
        public float seatedDropM;
    }

    /// <summary>
    /// The Phase 5 headset checklist, shown in the headset so the tester does
    /// not take it off between steps. Each step ticks itself from what the
    /// flow, transport, anchor and MIDI actually did -- the tester still
    /// judges by eye whether the keys line up and the wording is right.
    ///
    /// Pure: the shell (DeviceChecklistRunner) feeds it observations and
    /// does the file I/O. Development builds only.
    /// </summary>
    public sealed class DeviceChecklist
    {
        public const int Count = 10;

        public const int LaunchVerifying = 0, ConfirmNotPlay = 1, MiddleCFix = 2, FixSurvivesRelaunch = 3,
                         CancelFromReady = 4, TooFarRefused = 5, VerticalCap = 6, Recalibrate = 7,
                         Seated = 8, RecenterAndMidi = 9;

        /// <summary>A song starting this soon after A means that A started it.</summary>
        public const double ConfirmGraceSeconds = 0.5;
        /// <summary>Head this far below its standing height: sitting.</summary>
        public const float SeatedDropMetres = 0.25f;
        /// <summary>A single-frame jump of the same anchor's world pose this
        /// big is a recenter, not drift.</summary>
        public const float RecenterJumpMetres = 0.03f;
        public const float RecenterJumpDegrees = 2f;

        public static readonly string[] Titles =
        {
            "Launch shows Verifying",
            "A confirms -- and does not play",
            "Middle C fix",
            "Fix survives a relaunch",
            "Cancel from Ready",
            "Over 150 mm is refused",
            "Vertical cap (10 mm)",
            "Recalibrate wording",
            "Seated check",
            "Recenter, then play",
        };

        public static readonly string[] Instructions =
        {
            "Launch with the piano where it was. Keys drawn on the piano, yellow banner showing.",
            "Press A: the song must NOT start (stays Stopped). Then press A again: it counts in. Pause it.",
            "Standing (stand for everything until step 9). Right grip. Touch the front edge of real Middle C, trigger. Keys shift a little. A to accept.",
            "Quit the app and relaunch. Keys come back in the corrected place. Press A.",
            "From Ready: right grip, then right grip again. Nothing moves, stays Ready.",
            "Right grip. Touch a key 3+ keys from Middle C, trigger. Refused: \"piano may have moved\". Keys stay. A.",
            "Standing. Right grip. Hold the tip ~2 cm ABOVE Middle C's edge, trigger. Panel: y capped. A.",
            "Press B. Must say \"recalibrating -- touch the lowest key\", NOT \"no saved piano\". Lowest, highest, A.",
            "Sit down. Do A0 and C8 still line up? Right grip, touch Middle C's edge exactly, trigger. A.",
            "Recenter (hold the Oculus button). Keys stay on the piano. Then play a few notes: keys light.",
        };

        public DeviceChecklistState State { get; }

        /// <summary>Something worth saving changed since MarkSaved.</summary>
        public bool Dirty { get; private set; }

        private string _launchCorrection;
        private double _confirmAt = double.NegativeInfinity;
        private bool _recalibrating;
        private bool _recentered;
        private float _standingHead = float.NegativeInfinity;
        private float _head = float.NaN;

        /// <summary>One app run. <paramref name="restored"/> is what the last
        /// run saved (or null); it is kept only if it is for this build.</summary>
        public DeviceChecklist(DeviceChecklistState restored, string build)
        {
            build ??= "";
            State = restored != null && restored.build == build && restored.results != null &&
                    restored.results.Length == Count
                ? restored
                : new DeviceChecklistState { build = build };
            State.run++;
            Dirty = true;
        }

        public CheckResult Result(int step) => (CheckResult)State.results[step];

        /// <summary>First step neither passed nor failed; -1 when all are done.</summary>
        public int Current
        {
            get
            {
                for (int i = 0; i < Count; i++)
                    if (Result(i) == CheckResult.Pending) return i;
                return -1;
            }
        }

        public int PassedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++) if (Result(i) == CheckResult.Passed) n++;
                return n;
            }
        }

        public void MarkSaved() => Dirty = false;

        // ------------------------------------------------------------------
        // Observations
        // ------------------------------------------------------------------

        /// <summary>The lastCorrection of the profile read at launch.</summary>
        public void OnLaunchProfile(string lastCorrection) => _launchCorrection = lastCorrection;

        /// <summary>Every frame: head height, and whether the keys are on show.
        /// The highest head while they are is "standing".</summary>
        public void OnHead(float headY, bool overlayVisible)
        {
            _head = headY;
            if (overlayVisible && headY > _standingHead) _standingHead = headY;
        }

        /// <summary>Every handled flow transition. <paramref name="rawErrorMm"/>
        /// is the session's LastVerifyErrorMm (matters for CorrectionApplied).</summary>
        public void OnFlow(FlowEvent e, FlowTransition t, Vector3 rawErrorMm, double now)
        {
            switch (e)
            {
                case FlowEvent.LocalizeSucceeded:
                    Pass(LaunchVerifying);
                    if (_launchCorrection == "middle-c" && State.fixRun > 0 && State.fixRun < State.run)
                        Pass(FixSurvivesRelaunch);
                    break;

                case FlowEvent.ConfirmPressed:
                    _confirmAt = now;
                    break;

                case FlowEvent.CorrectionApplied:
                    State.fixRun = State.run;
                    Dirty = true;
                    if (_standingHead - _head >= SeatedDropMetres)
                    {
                        State.seatedYMm = rawErrorMm.y;
                        State.seatedDropM = _standingHead - _head;
                        Pass(Seated);
                    }
                    else if (Mathf.Abs(rawErrorMm.y) > AnchoredKeyboard.VerticalCapMetres * 1000f)
                    {
                        State.cappedYMm = rawErrorMm.y;
                        Pass(VerticalCap);
                    }
                    else
                    {
                        State.fixErrorMm = rawErrorMm;
                        Pass(MiddleCFix);
                    }
                    break;

                case FlowEvent.CorrectionCancelled:
                    if (t.From == FlowState.Correcting && t.To == FlowState.Ready) Pass(CancelFromReady);
                    break;

                case FlowEvent.CorrectionRejectedMoved:
                    Pass(TooFarRefused);
                    break;

                case FlowEvent.RecalibrateRequested:
                    _recalibrating = true;
                    break;

                case FlowEvent.NewAnchorSaved:
                    if (_recalibrating && t.To == FlowState.Verifying) Pass(Recalibrate);
                    _recalibrating = false;
                    break;
            }
        }

        /// <summary>The song went from not running to counting in or playing.</summary>
        public void OnTransportStarted(FlowState flow, double now)
        {
            if (now - _confirmAt <= ConfirmGraceSeconds)
            {
                // The confirming A started the song -- the bug this step is for.
                // Sticky: a later clean start does not undo it.
                if (Result(ConfirmNotPlay) != CheckResult.Failed) Set(ConfirmNotPlay, CheckResult.Failed);
                return;
            }
            if (flow == FlowState.Ready && !double.IsNegativeInfinity(_confirmAt)) Pass(ConfirmNotPlay);
        }

        /// <summary>The bound anchor's world pose moved this much in one frame
        /// (same anchor; a new anchor is not a jump).</summary>
        public void OnAnchorJump(float metres, float degrees, FlowState flow)
        {
            if (!CalibrationFlow.OverlayVisible(flow) && flow != FlowState.Suspended) return;
            if (metres > RecenterJumpMetres || degrees > RecenterJumpDegrees) _recentered = true;
        }

        public void OnNoteOn(FlowState flow)
        {
            if (_recentered && flow == FlowState.Ready) Pass(RecenterAndMidi);
        }

        private void Pass(int step)
        {
            if (Result(step) == CheckResult.Pending) Set(step, CheckResult.Passed);
        }

        private void Set(int step, CheckResult r)
        {
            State.results[step] = (int)r;
            Dirty = true;
        }

        // ------------------------------------------------------------------
        // Panel text
        // ------------------------------------------------------------------

        public string Render()
        {
            var sb = new StringBuilder(512);
            sb.AppendLine($"<b>PHASE 5 TEST</b>   {PassedCount}/{Count} passed");

            int current = Current;
            for (int i = 0; i < Count; i++)
            {
                string n = (i + 1).ToString();
                string colour = Result(i) switch
                {
                    CheckResult.Passed => "#A9C67C",
                    CheckResult.Failed => "#E5948A",
                    _                  => i == current ? "#E8D48A" : "#8A8A8A",
                };
                sb.Append(i == current ? $"<color={colour}><b>[{n}]</b></color> " : $"<color={colour}>{n}</color> ");
            }
            sb.AppendLine();
            sb.AppendLine();

            if (current >= 0)
            {
                sb.AppendLine($"<b><color=#E8D48A>{current + 1}. {Titles[current]}</color></b>");
                sb.AppendLine(Instructions[current]);
            }
            else
            {
                sb.AppendLine("<b><color=#A9C67C>All steps done.</color></b>");
                sb.AppendLine("Take the headset off and send the results.");
            }

            for (int i = 0; i < Count; i++)
                if (Result(i) == CheckResult.Failed)
                    sb.AppendLine($"<color=#E5948A>FAILED {i + 1}: {Titles[i]}</color>");

            sb.AppendLine();
            if (Result(MiddleCFix) == CheckResult.Passed)
                sb.AppendLine($"fix error:  x {Signed(State.fixErrorMm.x)} · y {Signed(State.fixErrorMm.y)} · z {Signed(State.fixErrorMm.z)} mm");
            if (Result(VerticalCap) == CheckResult.Passed)
                sb.AppendLine($"cap test:   measured y {Signed(State.cappedYMm)} mm");
            if (Result(Seated) == CheckResult.Passed)
                sb.AppendLine($"seated:     y {Signed(State.seatedYMm)} mm  (head {State.seatedDropM * 100f:0} cm lower)");
            return sb.ToString();
        }

        private static string Signed(float v) => v.ToString("+0.0;-0.0;0");
    }
}
