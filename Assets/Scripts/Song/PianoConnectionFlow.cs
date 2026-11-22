namespace PianoGlass.Song
{
    /// <summary>Which piano popup is up, if any (#44).</summary>
    public enum PianoPopup
    {
        None,
        /// <summary>No keyboard and no song under way: plug it in, or go on without.</summary>
        Connect,
        /// <summary>Unplugged part-way through a song, which is now paused.</summary>
        Unplugged,
        /// <summary>Plugged back in after that: the song waits for Resume.</summary>
        Reconnected,
    }

    /// <summary>What the caller must do after a step of the flow.</summary>
    public enum PianoFlowAction { None, PauseSong, ResumeSong }

    /// <summary>
    /// Playing without a MIDI keyboard (#44). The keyboard is how the app
    /// knows what was played, but some people have no MIDI cable, or play a
    /// grand piano, and can still follow the falling notes. So:
    ///
    ///   - No keyboard a few seconds after launch (it takes a moment to open):
    ///     the Connect popup, with "Continue without piano (not recommended)".
    ///   - Continue: no-piano mode. Notes fall and keys light up, nothing is
    ///     scored. A keyboard plugged in later ends it.
    ///   - Unplugged part-way through a song: the song pauses and the
    ///     Unplugged popup offers the same Continue button.
    ///   - Plugged back in: the popup says so and its button becomes Resume.
    ///     The song only goes on when Resume is pressed (Ryan: people plug the
    ///     keyboard back in with the headset off), from where it was, after
    ///     the resume countdown.
    ///
    /// Pure and tested; PianoConnection feeds it the keyboard state each frame.
    /// </summary>
    public sealed class PianoConnectionFlow
    {
        /// <summary>A keyboard that is already plugged in takes a moment to
        /// open at launch: no popup before this.</summary>
        public const double StartupGraceSeconds = 3.0;

        public const string ContinueLabel = "Continue without piano (not recommended)";
        public const string ContinueShortLabel = "Continue without piano";
        public const string ResumeLabel = "Resume";

        private bool _started, _wasConnected;
        private double _startedAt;

        public PianoPopup Popup { get; private set; }
        /// <summary>The player chose to go on without a keyboard.</summary>
        public bool NoPiano { get; private set; }
        public bool Connected { get; private set; }

        /// <summary>One frame. <paramref name="songUnderWay"/>: playing, or
        /// paused part-way, so an unplug interrupts something.</summary>
        public PianoFlowAction Tick(double now, bool connected, bool songUnderWay)
        {
            if (!_started)
            {
                _started = true;
                _startedAt = now;
                _wasConnected = connected;
            }
            Connected = connected;

            PianoFlowAction action = PianoFlowAction.None;
            if (_wasConnected && !connected)
            {
                // Unplugged. A keyboard that was here ends no-piano mode, so this
                // asks again either way.
                if (songUnderWay)
                {
                    Popup = PianoPopup.Unplugged;
                    action = PianoFlowAction.PauseSong;
                }
                else
                {
                    Popup = PianoPopup.Connect;
                }
            }
            else if (!_wasConnected && connected)
            {
                NoPiano = false;
                if (Popup == PianoPopup.Unplugged) Popup = PianoPopup.Reconnected;
                else if (Popup == PianoPopup.Connect) Popup = PianoPopup.None;
            }
            else if (!connected && !NoPiano && Popup == PianoPopup.None &&
                     now - _startedAt >= StartupGraceSeconds)
            {
                Popup = songUnderWay ? PianoPopup.Unplugged : PianoPopup.Connect;
                if (songUnderWay) action = PianoFlowAction.PauseSong;
            }
            _wasConnected = connected;
            return action;
        }

        /// <summary>The popup's button was pressed.</summary>
        public PianoFlowAction Press()
        {
            switch (Popup)
            {
                case PianoPopup.Connect:
                    Popup = PianoPopup.None;
                    NoPiano = true;
                    return PianoFlowAction.None;
                case PianoPopup.Unplugged:
                    Popup = PianoPopup.None;
                    NoPiano = true;
                    return PianoFlowAction.ResumeSong;
                case PianoPopup.Reconnected:
                    Popup = PianoPopup.None;
                    return PianoFlowAction.ResumeSong;
                default:
                    return PianoFlowAction.None;
            }
        }

        public string Title => Popup switch
        {
            PianoPopup.Connect => "Connect your piano keyboard",
            PianoPopup.Unplugged => "Your piano keyboard was unplugged",
            PianoPopup.Reconnected => "Your piano is connected again",
            _ => null,
        };

        public string Detail => Popup switch
        {
            PianoPopup.Connect => "Plug its USB cable into the headset. Without it the app can't hear what you play, so there's no score.",
            PianoPopup.Unplugged => "The song is paused. Plug it back in to keep your score.",
            PianoPopup.Reconnected => "The song is still paused. Press Resume when you're ready.",
            _ => null,
        };

        public string ButtonLabel => Popup switch
        {
            PianoPopup.Connect => ContinueLabel,
            PianoPopup.Unplugged => ContinueShortLabel,
            PianoPopup.Reconnected => ResumeLabel,
            _ => null,
        };
    }
}
