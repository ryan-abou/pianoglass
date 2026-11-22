using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PianoGlass.Midi;
using PianoGlass.Piano;
using PianoGlass.Scoring;
using PianoGlass.Song;
using PianoGlass.Transport;

namespace PianoGlass.UI
{
    /// <summary>
    /// The status strip (#37): one small Liquid Glass panel that says only
    /// what matters right now, and is hidden when there is nothing to say.
    ///
    ///   setting up       the next step, problems, and the keys' numbers
    ///                    (key width, span, nudge, turn) in plain words
    ///   playing          the score like a game: points, accuracy, the
    ///                    perfect/great/good/miss/wrong counts, the last hit,
    ///                    and a wrong note's name for a few seconds
    ///   no keyboard      a reminder to plug it in
    ///
    /// It replaces the old status panel for players: release builds switch
    /// that panel off, and development builds keep it as test info (hidden
    /// by recording mode). The keys' numbers show while setting up or
    /// checking the keys, in Settings, and for a few seconds after a nudge.
    ///
    /// It never follows the head (Ryan, round 11). It sits above the note
    /// wall, or wherever it was last put (GrabbablePanel), and moves in
    /// front of the player only once at launch before the keys are placed,
    /// and once when they start a calibration, so the steps are readable.
    /// Built in code from the song picker's glass; no scene change.
    /// </summary>
    public sealed class StatusStrip : MonoBehaviour
    {
        private const float Width = 470f, FontSize = 16f, PadX = 18f, PadY = 12f;
        /// <summary>Above the keys and behind them, metres: the bottom edge
        /// sits just over the 0.5 m note wall.</summary>
        private const float Above = 0.56f, Behind = 0.05f;
        /// <summary>In front of the eyes, and below them, when placed there.</summary>
        private const float FrontDistance = 0.8f, FrontDrop = 0.15f;
        /// <summary>Seconds a wrong note's name, or the keys' numbers after a nudge, stay up.</summary>
        private const float WrongSeconds = 4f, NudgeSeconds = 4f;

        private KeyboardCalibration _calibration;
        private CalibrationFlowController _flow;
        private ControllerTipCalibration _tip;
        private ScoreKeeper _score;
        private SongTransport _transport;
        private SongClock _clock;
        private PracticeLoop _loop;

        private RectTransform _canvas;
        private TextMeshProUGUI _text;
        private GrabbablePanel _grab;
        private PianoKeyLayout _placedFor;
        private bool _everPlaced, _wasCalibrating;
        private string _shown;

        private WrongNote _lastWrong;
        private float _wrongUntil;
        private Vector3 _lastNudge;
        private float _lastYaw;
        private float _nudgeUntil;

        private readonly List<string> _lines = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add() => new GameObject(nameof(StatusStrip)).AddComponent<StatusStrip>();

        private void Start()
        {
            _calibration = FindAnyObjectByType<KeyboardCalibration>();
            _flow = FindAnyObjectByType<CalibrationFlowController>();
            _tip = FindAnyObjectByType<ControllerTipCalibration>();
            _score = FindAnyObjectByType<ScoreKeeper>();
            _transport = FindAnyObjectByType<SongTransport>();
            _clock = FindAnyObjectByType<SongClock>();
            _loop = FindAnyObjectByType<PracticeLoop>();
            var picker = FindAnyObjectByType<SongPicker>(FindObjectsInactive.Include);
            if (_calibration == null || picker == null)
            {
                Debug.LogWarning("[StatusStrip] off: no calibration or song picker in the scene");
                enabled = false;
                return;
            }
            Build(new GlassBuilder(picker));
            HideOldPanelForPlayers();
        }

        /// <summary>The old status panel is test info now: release builds
        /// never show it. Development builds keep it, and recording mode
        /// hides it, so recording mode shows what a player sees.</summary>
        private static void HideOldPanelForPlayers()
        {
            if (Debug.isDebugBuild) return;
            var old = FindAnyObjectByType<CalibrationPanel>(FindObjectsInactive.Include);
            Canvas canvas = old != null ? old.GetComponentInParent<Canvas>(true) : null;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void Build(GlassBuilder g)
        {
            _canvas = g.Canvas(transform, "StatusStripCanvas", new Vector2(Width, 60f));
            _canvas.pivot = new Vector2(0.5f, 0f); // grows upward, away from the keys
            _text = g.Text(_canvas, "Text", FontSize, g.Primary, TextAlignmentOptions.TopLeft, Vector2.zero, Width);
            var rt = (RectTransform)_text.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(PadX, PadY);
            rt.offsetMax = new Vector2(-PadX, -PadY);
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.richText = true;
            _grab = GrabbablePanel.Add(_canvas.gameObject, "StatusStrip", _calibration, PlaceOnKeys);
        }

        private void LateUpdate()
        {
            Compose();
            bool show = _lines.Count > 0 && ResultsCard.StatusShown && !LaunchResync.WaitingForRecenter;
            if (_canvas.gameObject.activeSelf != show) _canvas.gameObject.SetActive(show);
            Place();
            if (!show) return;

            string text = string.Join("\n", _lines);
            if (text == _shown) return;
            _shown = text;
            _text.text = text;
            float inner = Width - PadX * 2f;
            float height = _text.GetPreferredValues(text, inner, 0f).y + PadY * 2f;
            _canvas.sizeDelta = new Vector2(Width, Mathf.Ceil(height / 10f) * 10f);
        }

        // ------------------------------------------------------------------
        // What it says
        // ------------------------------------------------------------------

        private void Compose()
        {
            _lines.Clear();
            bool setup = AddSetup();
            if (ShowKeys(setup)) AddKeys();
            if (!setup) AddScore();
            if (!setup) AddKeyboardMissing();
        }

        /// <summary>The next step, and any problem. True while setting up.</summary>
        private bool AddSetup()
        {
            if (_tip != null && _tip.IsBusy)
            {
                _lines.Add(_tip.Status);
                return true;
            }

            bool anchored = _flow != null && _flow.Active;
            if (anchored && !string.IsNullOrEmpty(_flow.LastProblem))
                _lines.Add(StatusStripText.Colour(_flow.LastProblem, StatusStripText.Red));

            if (!_calibration.NeedsPlayer) return false;

            if (anchored && _flow.Status == AnchorPanelStatus.Verifying)
            {
                _lines.Add("<b>Do the keys line up with your piano?</b>");
                _lines.Add(AnchorStatusText.VerifyBanner);
            }
            else if (!string.IsNullOrEmpty(_calibration.Prompt))
            {
                _lines.Add($"<b>{_calibration.Prompt}</b>");
            }
            return true;
        }

        private bool ShowKeys(bool setup)
        {
            PianoKeyLayout layout = _calibration.Layout;
            if (layout == null || !_calibration.IsPlacementValid) return false;

            Vector3 nudge = _calibration.NudgeMillimetres;
            float yaw = _calibration.YawDegrees;
            if ((nudge - _lastNudge).sqrMagnitude > 0.0025f || Mathf.Abs(yaw - _lastYaw) > 0.001f)
            {
                _nudgeUntil = Time.unscaledTime + NudgeSeconds;
                _lastNudge = nudge;
                _lastYaw = yaw;
            }
            return setup || SettingsPanel.IsOpen || Time.unscaledTime < _nudgeUntil;
        }

        private void AddKeys()
        {
            PianoKeyLayout layout = _calibration.Layout;
            Vector3 n = _calibration.NudgeMillimetres;
            int whites = PianoKeyLayout.CountWhiteKeys(layout.LowestNote, layout.HighestNote);
            _lines.AddRange(StatusStripText.Keys(new StripKeys
            {
                KeyCount = layout.KeyCount,
                Lowest = MidiInput.NoteName(layout.LowestNote),
                Highest = MidiInput.NoteName(layout.HighestNote),
                KeyWidthMm = layout.WhiteKeySpacing * 1000f,
                SpanCm = (whites - 1) * layout.WhiteKeySpacing * 100f,
                ExpectedSpanCm = (whites - 1) * StatusStripText.RealKeyWidthMm / 10f,
                Plausible = layout.Warning == null,
                NudgeRightMm = n.x, NudgeUpMm = n.y, NudgeNearerMm = n.z,
                YawDegrees = _calibration.YawDegrees,
            }));
        }

        /// <summary>While a song is under way, paused included; the results
        /// card takes over once it is saved.</summary>
        private void AddScore()
        {
            if (_score == null || _transport == null) return;
            ScoringSession s = _score.Session;
            if (s == null || _score.AttemptSaved) return;
            SongTransport.State state = _transport.CurrentState;
            bool running = state == SongTransport.State.Playing || state == SongTransport.State.CountingIn;
            if (!running && s.JudgedCount + s.WrongNotes.Count == 0) return;

            _lines.AddRange(StatusStripText.Score(new StripScore
            {
                PitchOnly = s.PitchOnly,
                Score = s.Score,
                Accuracy = s.Accuracy,
                Perfect = s.Perfect, Great = s.Great, Good = s.Good, Miss = s.Miss, Correct = s.Correct,
                Wrong = s.WrongPitchCount + s.ExtraCount,
            }));

            JudgementResult r = s.LastResult;
            if (r != null && !s.PitchOnly)
                _lines.Add(StatusStripText.LastHit(r.rating, MidiInput.NoteName(r.pitch), r.pressed, r.errorMs));

            WrongNote w = s.LastWrongNote;
            if (w != _lastWrong)
            {
                _lastWrong = w;
                _wrongUntil = Time.unscaledTime + WrongSeconds;
            }
            if (w != null && Time.unscaledTime < _wrongUntil)
            {
                bool hidden = _clock != null && _clock.NoteOpacity <= 0f;
                _lines.Add(StatusStripText.Wrong(MidiInput.NoteName(w.pressedPitch),
                                                 w.expectedPitch < 0 ? null : MidiInput.NoteName(w.expectedPitch),
                                                 w.bar, hidden));
            }

            if (_loop != null && _loop.IsActive && _loop.LastPassLine != null)
                _lines.Add(StatusStripText.Colour($"Last pass: {_loop.LastPassLine}", StatusStripText.Yellow));
        }

        private void AddKeyboardMissing()
        {
            MidiInput midi = MidiInput.Instance;
            if (midi == null || Application.isEditor) return;
            if (!midi.IsSupported)
                _lines.Add(StatusStripText.Colour("This headset can't read a MIDI keyboard right now.", StatusStripText.Red));
            else if (!midi.IsConnected)
                _lines.Add(StatusStripText.Colour("Plug your piano keyboard into the headset's USB-C port.", StatusStripText.Yellow));
        }

        // ------------------------------------------------------------------
        // Where it is
        // ------------------------------------------------------------------

        private void Place()
        {
            bool calibrating = _calibration.CurrentStage == KeyboardCalibration.Stage.AwaitingLowest;
            if (calibrating && !_wasCalibrating) PlaceInFront("a calibration started");
            _wasCalibrating = calibrating;

            PianoKeyLayout layout = _calibration.IsPlacementValid ? _calibration.Layout : null;
            if (layout != null)
            {
                // A spot of its own: GrabbablePanel keeps it there, on the keys as they are now.
                if (_grab != null && _grab.KeepsItsOwnSpot) { _placedFor = layout; _everPlaced = true; return; }
                if (layout != _placedFor) PlaceOnKeys();
                return;
            }

            // Never placed yet (launch, before the keys): once, in front, when the head is tracked.
            if (!_everPlaced && Time.realtimeSinceStartup > 1f) PlaceInFront("launch, before the keys are placed");
        }

        private void PlaceOnKeys()
        {
            PianoKeyLayout layout = _calibration.IsPlacementValid ? _calibration.Layout : null;
            if (layout == null || !layout.TryGetKey(layout.LowestNote, out PianoKey low) ||
                !layout.TryGetKey(layout.HighestNote, out PianoKey high))
                return;
            Vector3 centre = (low.Center + high.Center) * 0.5f;
            Vector3 pos = centre - layout.Forward * (layout.WhiteKeyDepth * 0.5f + Behind) + layout.Up * Above;
            // A canvas is read from behind its +Z: +Z points away from the player.
            _canvas.SetPositionAndRotation(pos, Quaternion.LookRotation(-layout.Forward, layout.Up));
            _placedFor = layout;
            _everPlaced = true;
        }

        private void PlaceInFront(string why)
        {
            Camera head = Camera.main;
            if (head == null) return;
            Vector3 forward = head.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return;
            forward.Normalize();
            Vector3 pos = head.transform.position + forward * FrontDistance + Vector3.down * FrontDrop;
            _canvas.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, Vector3.up));
            _placedFor = null;
            _everPlaced = true;
            Debug.Log($"[StatusStrip] in front of the player: {why}");
        }
    }
}
