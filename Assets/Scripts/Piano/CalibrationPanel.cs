using System.Text;
using TMPro;
using UnityEngine;
using PianoGlass.Midi;
using PianoGlass.Scoring;
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
        [SerializeField] private ScoreKeeper score;
        [SerializeField] private Metronome metronome;
        [SerializeField] private PracticeLoop loop;

        private readonly StringBuilder _sb = new StringBuilder(256);
        private PianoGlass.UI.GlassPanel _glass;

        /// <summary>The panel's line count changes with what it is saying, and
        /// its longest text runs past the Canvas rect. In steps of 10 mm, so
        /// the glass only rebuilds when a line or so comes or goes.</summary>
        private void LateUpdate()
        {
            if (_glass == null || text == null || text.textInfo == null || text.textInfo.characterCount == 0) return;
            var canvasRect = (RectTransform)_glass.transform;
            float bottom = canvasRect.InverseTransformPoint(text.transform.TransformPoint(text.textBounds.min)).y;
            float over = canvasRect.rect.yMin - bottom;
            _glass.SetBottomExtension(over > 0f ? Mathf.Ceil(over / 10f) * 10f : 0f);
        }

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
            if (score == null) score = FindAnyObjectByType<ScoreKeeper>();
            if (metronome == null) metronome = FindAnyObjectByType<Metronome>();
            if (loop == null) loop = FindAnyObjectByType<PracticeLoop>();
            _glass = GetComponentInParent<PianoGlass.UI.GlassPanel>();

            // Development builds: the headset test checklist, beside this panel.
            if (Debug.isDebugBuild && flow != null && text != null)
                gameObject.AddComponent<DeviceChecklistRunner>().Init(flow, transport, text);
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

            // The Verifying banner is the prompt line; make it the thing the
            // eye lands on.
            if (flow != null && flow.Active && flow.Status == AnchorPanelStatus.Verifying)
                _sb.AppendLine($"<b><color=#E8D48A>{calibration.Prompt}</color></b>");
            else
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
                if (!string.IsNullOrEmpty(flow.CorrectionNote))
                    _sb.AppendLine($"<color=#A9C67C>{flow.CorrectionNote}</color>");
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
                _sb.AppendLine($"transport:  {StateLabel(transport.CurrentState)}   t = {clock.SongTime:0.0}s   " +
                               $"{TempoStep.Label(clock.TempoMultiplier)}" +
                               (metronome != null ? $"   metronome {(metronome.IsOn ? "on" : "off")}" : ""));
            }

            if (loop != null && loop.IsActive) AppendLoop(loop);

            if (score != null && score.Session != null) AppendScore(score);

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

        private void AppendLoop(PracticeLoop practice)
        {
            string last = practice.LastAccuracy >= 0.0 ? $"   last pass {practice.LastAccuracy:0}%" : "";
            _sb.AppendLine($"<color=#E8D48A>loop:       {practice.Range.Value.Label}, pass {practice.Pass}{last}   Y = full song</color>");
        }

        private void AppendScore(ScoreKeeper keeper)
        {
            ScoringSession s = keeper.Session;
            _sb.AppendLine($"score:      {s.Score}   {s.Accuracy:0}%   perfect {s.Perfect} · great {s.Great} · " +
                           $"good {s.Good} · miss {s.Miss} · wrong {s.WrongPitchCount + s.ExtraCount}");

            JudgementResult r = s.LastResult;
            if (r != null)
            {
                string colour = r.judgement switch
                {
                    Judgement.Perfect => "#A9C67C",
                    Judgement.Great   => "#8FC1E8",
                    Judgement.Good    => "#E8D48A",
                    _                 => "#E5948A",
                };
                _sb.AppendLine($"last:       <color={colour}>{r.rating.ToUpperInvariant()}</color>  " +
                               $"{MidiInput.NoteName(r.pitch)}" + (r.pressed ? $"  {r.errorMs:+0;-0;0} ms" : ""));
            }

            WrongNote w = s.LastWrongNote;
            if (w != null)
                _sb.AppendLine($"<color=#E5948A>wrong:      {MidiInput.NoteName(w.pressedPitch)}" +
                               (w.expectedPitch >= 0 ? $" instead of {MidiInput.NoteName(w.expectedPitch)}" : " (nothing due)") +
                               $", bar {w.bar}</color>");

            if (keeper.AttemptSaved)
            {
                BarStats worst = s.WorstBar();
                _sb.AppendLine(worst != null && worst.accuracy < 100.0
                    ? $"done:       weakest bar {worst.bar} ({worst.accuracy:0}%)   Y = play again"
                    : "done:       every bar clean   Y = play again");
            }
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
