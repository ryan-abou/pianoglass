using UnityEngine;
using PianoGlass.Midi;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Calibrating without controllers (#5): press the lowest and then the
    /// highest key with an index finger, and the fingertip that pressed each
    /// one is the touch. MIDI says exactly which key went down and when, and
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

        private void OnDestroy()
        {
            if (MidiInput.Instance != null) MidiInput.Instance.NoteOn -= OnNoteOn;
        }

        private void OnNoteOn(int note, int velocity, double time)
        {
            TrackedHands hands = TrackedHands.Instance;
            if (hands == null || !hands.TryPressingIndexTip(out Vector3 tip)) return;

            KeyboardCalibration.Stage stage = _calibration.CurrentStage;
            bool wanted =
                (stage == KeyboardCalibration.Stage.AwaitingLowest && note == _calibration.LowestNote) ||
                (stage == KeyboardCalibration.Stage.AwaitingHighest && note == _calibration.HighestNote) ||
                (stage == KeyboardCalibration.Stage.Calibrated && note == MiddleC &&
                 _flow != null && _flow.Active && _flow.IsCorrecting);
            if (!wanted) return;

            Debug.Log($"[HandCalibration] {MidiInput.NoteName(note)} pressed: fingertip at {tip} is the touch ({stage})");
            _calibration.CapturePointAt(tip, byHand: true);
        }
    }
}
