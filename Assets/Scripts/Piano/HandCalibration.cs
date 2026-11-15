using UnityEngine;
using PianoGlass.Midi;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Calibrating without controllers (#5): press the lowest and then the
    /// highest key with an index finger, and the fingertip that pressed each
    /// one is the touch (PressingFinger tells which hand that was). MIDI says exactly which key went down and when, and
    /// the tracked fingertip says where, which the press probe found accurate
    /// along the keys (within a few mm; 328 presses, 2026-11-05). A Middle C
    /// fix works the same way: start it, then press Middle C.
    ///
    /// Only while hands are tracked, so a controller tip that happens to press
    /// a key never counts twice. Touches are marked as by hand, so they learn
    /// their own calibration bias, apart from the controller tip's.
    /// Adds itself after the scene loads; no scene change.
    /// </summary>
    public sealed class HandCalibration : MonoBehaviour
    {
        private const int MiddleC = 60;

        private KeyboardCalibration _calibration;
        private CalibrationFlowController _flow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add()
        {
            if (Application.isEditor) return;
            var go = new GameObject(nameof(HandCalibration));
            DontDestroyOnLoad(go);
            go.AddComponent<HandCalibration>();
        }

        private void Start()
        {
            _calibration = FindAnyObjectByType<KeyboardCalibration>();
            _flow = FindAnyObjectByType<CalibrationFlowController>();
            if (_calibration == null || MidiInput.Instance == null)
            {
                Debug.LogWarning("[HandCalibration] off: no calibration or MIDI");
                enabled = false;
                return;
            }
            MidiInput.Instance.NoteOn += OnNoteOn;
        }

        private static string Describe(PressingFinger.Tip[] tips)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < tips.Length; i++)
                parts.Add(tips[i].Tracked ? $"hand {i} at {tips[i].Position} came down {tips[i].Drop * 1000f:0} mm"
                                          : $"hand {i} not tracked");
            return string.Join(", ", parts);
        }

        private void OnDestroy()
        {
            if (MidiInput.Instance != null) MidiInput.Instance.NoteOn -= OnNoteOn;
        }

        private void OnNoteOn(int note, int velocity, double time)
        {
            TrackedHands hands = TrackedHands.Instance;
            if (hands == null || !hands.AnyTracked) return;

            KeyboardCalibration.Stage stage = _calibration.CurrentStage;
            bool wanted =
                (stage == KeyboardCalibration.Stage.AwaitingLowest && note == _calibration.LowestNote) ||
                (stage == KeyboardCalibration.Stage.AwaitingHighest && note == _calibration.HighestNote) ||
                (stage == KeyboardCalibration.Stage.Calibrated && note == MiddleC &&
                 _flow != null && _flow.Active && _flow.IsCorrecting);
            if (!wanted) return;

            // Which hand pressed it: the fingertip that just came down, not the
            // lower one (the other hand in the lap is lower, 2026-11-14).
            PressingFinger.Tip[] tips = hands.PressTips();
            Vector3? last = null;
            // While capturing, Layout is cleared: where the keys were before it.
            PianoKeyLayout layout = _calibration.Layout ?? _calibration.PreviousLayout;
            if (layout != null && layout.TryGetKey(note, out PianoKey key)) last = key.Center;
            int i = PressingFinger.Choose(tips, last);
            string seen = Describe(tips);
            if (i < 0)
            {
                Debug.Log($"[HandCalibration] {MidiInput.NoteName(note)} pressed, but which finger pressed it " +
                          $"can't be told ({seen}): press it again, with the other hand still and off the keys");
                return;
            }

            Vector3 tip = tips[i].Position;
            Debug.Log($"[HandCalibration] {MidiInput.NoteName(note)} pressed: hand {i} fingertip at {tip} " +
                      $"is the touch ({stage}; {seen})");
            _calibration.CapturePointAt(tip, byHand: true);
        }
    }
}
