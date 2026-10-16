using System.Text;
using TMPro;
using UnityEngine;
using PianoGlass.Song;
using PianoGlass.Transport;

namespace PianoGlass.Piano
{
    /// <summary>
    /// In-headset readout for calibration: what to do next, and whether the
    /// result is believable.
    ///
    /// The believability part matters more than it sounds. A calibration that
    /// is subtly wrong — right keyboard, wrong two keys — produces a layout
    /// that looks plausible until you play a scale and every note is off by
    /// one. Showing the derived key width against the real-world 23.5 mm turns
    /// that from a mystery into a number you can read while wearing the
    /// headset, without going back to the console.
    /// </summary>
    public class CalibrationPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private KeyboardCalibration calibration;
        [SerializeField] private SongTransport transport;
        [SerializeField] private SongClock clock;
        [SerializeField] private ControllerTipCalibration tip;
        [Tooltip("Anchored calibration. Its status is shown here because the " +
                 "player has no PC attached -- a logcat-only message is invisible.")]
        [SerializeField] private CalibrationFlowController flow;

        private readonly StringBuilder _sb = new StringBuilder(256);

        private void Reset()
        {
            text = GetComponent<TMP_Text>();
            calibration = FindAnyObjectByType<KeyboardCalibration>();
            transport = FindAnyObjectByType<SongTransport>();
            clock = FindAnyObjectByType<SongClock>();
            tip = FindAnyObjectByType<ControllerTipCalibration>();
            flow = FindAnyObjectByType<CalibrationFlowController>();
        }

        private void Awake()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (calibration == null) calibration = FindAnyObjectByType<KeyboardCalibration>();
            if (transport == null) transport = FindAnyObjectByType<SongTransport>();
            if (clock == null) clock = FindAnyObjectByType<SongClock>();
            if (tip == null) tip = FindAnyObjectByType<ControllerTipCalibration>();
            if (flow == null) flow = FindAnyObjectByType<CalibrationFlowController>();
        }

        private void Update()
        {
            if (text == null || calibration == null) return;

            _sb.Clear();
            _sb.AppendLine("<b>PianoGlass — calibration</b>");

            // While the tip is being measured, that is the only thing the
            // player should be reading.
            if (tip != null && tip.IsBusy)
            {
                _sb.AppendLine(tip.Status);
                if (tip.CurrentPhase == ControllerTipCalibration.Phase.Recording)
                    _sb.AppendLine($"recording  {tip.Progress * 100f:0}%   (left menu = cancel)");
                text.text = _sb.ToString();
                return;
            }

            _sb.AppendLine(calibration.Prompt);

            if (flow != null && flow.Active)
            {
                string colour = flow.Status switch
                {
                    AnchorPanelStatus.Ready          => "#A9C67C",
                    AnchorPanelStatus.Untracked      => "#E5948A",
                    AnchorPanelStatus.LocalizeFailed => "#E5948A",
                    AnchorPanelStatus.NotFound       => "#E5948A",
                    _                                => "#E8D48A",
                };
                _sb.AppendLine($"anchor:     <color={colour}>{flow.StatusLine}</color>");
                // Cleared by every success, so whatever is here is current --
                // including "not saved" while the keys are showing.
                if (!string.IsNullOrEmpty(flow.LastProblem))
                    _sb.AppendLine($"<color=#E5948A>{flow.LastProblem}</color>");
            }

            if (tip != null)
            {
                _sb.AppendLine(tip.HasOffset
                    ? $"tip:        {tip.OffsetMetres.magnitude * 1000f:0.0} mm from origin, RMS {tip.RmsMm:0.0} mm   (left menu = redo)"
                    : "tip:        not measured   (left menu = measure)");
                if (!string.IsNullOrEmpty(tip.Status)) _sb.AppendLine(tip.Status);
            }

            // Shown unconditionally, not just once calibrated: a silent
            // lead-in with nothing on the wall yet is easy to mistake for a
            // broken build the moment you press play, and this is the one
            // line that says otherwise.
            if (transport != null && clock != null)
            {
                _sb.AppendLine();
                _sb.AppendLine($"transport:  {StateLabel(transport.CurrentState)}   t = {clock.SongTime:0.0}s");
            }

            if (calibration.IsCalibrated)
            {
                var layout = calibration.Layout;
                float mm = layout.WhiteKeySpacing * 1000f;

                // Colour the number rather than making the user judge it: green
                // when it lands near a real white key, red when it does not.
                bool plausible = layout.Warning == null;
                string colour = plausible ? "#A9C67C" : "#E5948A";

                _sb.AppendLine();
                _sb.AppendLine($"keys:       {layout.KeyCount}  ({NoteName(layout.LowestNote)} to {NoteName(layout.HighestNote)})");
                _sb.AppendLine($"key width:  <color={colour}>{mm:0.0} mm</color>   (expect ~23.5)");
                _sb.AppendLine($"span:       {SpanCm(layout):0.0} cm   (expect ~119.9)");

                Vector3 n = calibration.NudgeMillimetres;
                if (n.sqrMagnitude > 0.01f)
                    _sb.AppendLine($"nudge:      x {n.x:+0.0;-0.0;0} · y {n.y:+0.0;-0.0;0} · z {n.z:+0.0;-0.0;0} mm");

                if (Mathf.Abs(calibration.YawDegrees) > 0.005f)
                    _sb.AppendLine($"yaw:        {calibration.YawDegrees:+0.00;-0.00} deg");

                if (!plausible)
                {
                    _sb.AppendLine();
                    _sb.AppendLine("<color=#E5948A>Those were probably not the lowest and");
                    _sb.AppendLine("highest keys. Press B (or left trigger) to redo.</color>");
                }
            }

            text.text = _sb.ToString();
        }

        private static float SpanCm(PianoKeyLayout layout)
        {
            int whites = PianoKeyLayout.CountWhiteKeys(layout.LowestNote, layout.HighestNote);
            return (whites - 1) * layout.WhiteKeySpacing * 100f;
        }

        private static readonly string[] Names =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        private static string NoteName(int note) => Names[note % 12] + (note / 12 - 1);

        private static string StateLabel(SongTransport.State state) => state switch
        {
            SongTransport.State.Stopped    => "Stopped",
            SongTransport.State.CountingIn => "Counting in",
            SongTransport.State.Playing    => "Playing",
            SongTransport.State.Finished   => "Finished",
            _ => state.ToString()
        };
    }
}
