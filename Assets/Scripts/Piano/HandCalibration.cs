using System.Collections;
using System.Collections.Generic;
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
    /// The touch is where the fingertip rests while the key is held down
    /// (SettledTouch), not the moment it went down (2026-11-15).
    /// Adds itself after the scene loads; no scene change.
    /// </summary>
    public sealed class HandCalibration : MonoBehaviour
    {
        private const int MiddleC = 60;

        private KeyboardCalibration _calibration;
        private CalibrationFlowController _flow;
        private bool _settling;

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
            if (_settling || !Wanted(note, stage)) return;

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

            StartCoroutine(Settle(note, i, tips[i].Position, stage, seen));
        }

        private bool Wanted(int note, KeyboardCalibration.Stage stage) =>
            (stage == KeyboardCalibration.Stage.AwaitingLowest && note == _calibration.LowestNote) ||
            (stage == KeyboardCalibration.Stage.AwaitingHighest && note == _calibration.HighestNote) ||
            (stage == KeyboardCalibration.Stage.Calibrated && note == MiddleC &&
             _flow != null && _flow.Active && _flow.IsCorrecting);

        /// <summary>Follows the pressing fingertip while the key stays down,
        /// then captures where it rested.</summary>
        private IEnumerator Settle(int note, int hand, Vector3 first, KeyboardCalibration.Stage stage, string seen)
        {
            _settling = true;
            var frames = new List<Vector3>();
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < SettledTouch.Until && MidiInput.Instance != null &&
                   MidiInput.Instance.IsHeld(note))
            {
                yield return null;
                TrackedHands hands = TrackedHands.Instance;
                if (Time.unscaledTime - start >= SettledTouch.From && hands != null &&
                    hands.TryIndexTip(hand, out Vector3 p))
                    frames.Add(p);
            }
            _settling = false;
            string name = MidiInput.NoteName(note);
            if (_calibration.CurrentStage != stage || !Wanted(note, stage))
            {
                Debug.Log($"[HandCalibration] {name}: the calibration moved on while the key was down; not used");
                yield break;
            }

            Vector3 tip;
            string how;
            if (frames.Count >= SettledTouch.MinFrames)
            {
                tip = SettledTouch.Median(frames);
                Vector3 d = (tip - first) * 1000f;
                how = $"rested at {Mm(tip)} over {frames.Count} frames (spread {SettledTouch.Spread(frames, tip) * 1000f:0} mm; " +
                      $"{d.x:+0;-0;0}/{d.y:+0;-0;0}/{d.z:+0;-0;0} mm from the moment it went down)";
            }
            else
            {
                tip = first;
                how = $"let go too soon to settle ({frames.Count} frames): the moment it went down, {Mm(tip)}";
            }
            Debug.Log($"[HandCalibration] {name} pressed: hand {hand} fingertip {how} is the touch ({stage}; {seen})");
            _calibration.CapturePointAt(tip, byHand: true);
        }

        private static string Mm(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";
    }
}
