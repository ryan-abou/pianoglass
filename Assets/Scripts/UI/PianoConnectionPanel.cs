using TMPro;
using UnityEngine;
using PianoGlass.Midi;
using PianoGlass.Piano;
using PianoGlass.Song;
using PianoGlass.Transport;

namespace PianoGlass.UI
{
    /// <summary>
    /// The piano popup (#44): asks for the MIDI keyboard, or lets the player
    /// go on without one. PianoConnectionFlow decides; this feeds it the
    /// keyboard state every frame, pauses the song on an unplug, tells the
    /// transport about no-piano mode, and shows the popup on Liquid Glass
    /// with its one button (controller ray or a poke).
    ///
    /// It appears in front of the player when it comes up and stays there:
    /// it does not follow the head. The Connect popup waits until the keys
    /// are placed and any launch recenter is done, so it never covers the
    /// setup steps; the Unplugged one comes up at once, since a song was
    /// playing. The editor counts as connected (MidiEditorSimulator).
    /// Builds itself after the scene loads; no scene change. Logs as "[Piano]".
    /// </summary>
    public sealed class PianoConnectionPanel : MonoBehaviour
    {
        private static readonly Vector2 Size = new Vector2(560f, 250f);
        private const float Distance = 0.75f, Drop = 0.12f;

        private readonly PianoConnectionFlow _flow = new PianoConnectionFlow();
        private SongTransport _transport;
        private KeyboardCalibration _calibration;

        private RectTransform _canvas;
        private TextMeshProUGUI _title, _detail, _buttonLabel;
        private PianoPopup _shown = PianoPopup.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add() => new GameObject(nameof(PianoConnectionPanel)).AddComponent<PianoConnectionPanel>();

        private void Start()
        {
            _transport = FindAnyObjectByType<SongTransport>();
            _calibration = FindAnyObjectByType<KeyboardCalibration>();
            var picker = FindAnyObjectByType<SongPicker>(FindObjectsInactive.Include);
            if (_transport == null || _calibration == null || picker == null)
            {
                Debug.LogWarning("[Piano] off: no transport, calibration or song picker in the scene");
                enabled = false;
                return;
            }
            Build(new GlassBuilder(picker));
        }

        private void Build(GlassBuilder g)
        {
            _canvas = g.Canvas(transform, "PianoPopupCanvas", Size);
            _canvas.GetComponent<Canvas>().sortingOrder = 40; // over the other glass
            float left = -Size.x * 0.5f + 22f, width = Size.x - 44f;
            _title = g.Text(_canvas, "Title", 24f, g.Primary, TextAlignmentOptions.TopLeft,
                            new Vector2(left, Size.y * 0.5f - 18f), width, true);
            _detail = g.Text(_canvas, "Detail", 16f, g.Secondary, TextAlignmentOptions.TopLeft,
                             new Vector2(left, Size.y * 0.5f - 58f), width);
            _detail.textWrappingMode = TextWrappingModes.Normal;
            _detail.overflowMode = TextOverflowModes.Overflow;
            ((RectTransform)_detail.transform).sizeDelta = new Vector2(width, 60f);
            GlassButton button = g.Pill(_canvas, "Button", new Vector2(0f, -Size.y * 0.5f + 50f),
                                        new Vector2(width, 60f), "", OnPressed, out _buttonLabel, 19f);
            button.AlwaysShown = true;
        }

        private void Update()
        {
            bool connected = Application.isEditor ||
                             (MidiInput.Instance != null && MidiInput.Instance.IsConnected);
            PianoPopup before = _flow.Popup;
            PianoFlowAction action = _flow.Tick(Time.realtimeSinceStartupAsDouble, connected, _transport.SongUnderWay);
            if (action == PianoFlowAction.PauseSong)
            {
                _transport.Pause();
                Debug.Log("[Piano] keyboard unplugged mid-song: paused");
            }
            if (_flow.Popup != before) Debug.Log($"[Piano] popup {before} -> {_flow.Popup}");

            _transport.SetNoPiano(_flow.NoPiano);
            SongTransport.PlayBlocked = _flow.Popup != PianoPopup.None;

            bool keysReady = _calibration.IsPlacementValid && !_calibration.NeedsPlayer && !LaunchResync.WaitingForRecenter;
            PianoPopup show = _flow.Popup == PianoPopup.Connect && !keysReady ? PianoPopup.None : _flow.Popup;
            if (show == _shown) return;

            if (show != PianoPopup.None && _shown == PianoPopup.None) PlaceInFront();
            _shown = show;
            _canvas.gameObject.SetActive(show != PianoPopup.None);
            if (show == PianoPopup.None) return;
            _title.text = _flow.Title;
            _detail.text = _flow.Detail;
            _buttonLabel.text = _flow.ButtonLabel;
        }

        private void OnPressed()
        {
            PianoPopup was = _flow.Popup;
            PianoFlowAction action = _flow.Press();
            Debug.Log($"[Piano] '{(was == PianoPopup.Reconnected ? PianoConnectionFlow.ResumeLabel : PianoConnectionFlow.ContinueShortLabel)}' pressed" +
                      (_flow.NoPiano ? ": no-piano mode" : ""));
            _transport.SetNoPiano(_flow.NoPiano);
            SongTransport.PlayBlocked = _flow.Popup != PianoPopup.None;
            if (action == PianoFlowAction.ResumeSong) _transport.ResumeAfterPopup();
        }

        /// <summary>Once, when it comes up: in front, a little below the eyes, upright.</summary>
        private void PlaceInFront()
        {
            Camera head = Camera.main;
            if (head == null) return;
            Vector3 forward = head.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 pos = head.transform.position + forward * Distance + Vector3.down * Drop;
            // A canvas is read from behind its +Z: +Z points away from the player.
            _canvas.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
