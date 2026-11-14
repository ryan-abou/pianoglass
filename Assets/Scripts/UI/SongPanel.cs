using System;
using TMPro;
using UnityEngine;
using PianoGlass.Piano;
using PianoGlass.Song;
using PianoGlass.Transport;

namespace PianoGlass.UI
{
    /// <summary>
    /// The song panel (#5, F12 + F24): play/pause, restart, tempo, metronome,
    /// wait mode, memory fade, songs and settings on Liquid Glass at the left
    /// end of the keyboard, tipped back toward the player. Every button works
    /// with a fingertip poke (HandPokePointer) or the controller ray, so a song
    /// can be run with the controllers put down, and every piano chord has a
    /// matching button here.
    ///
    /// While a song plays only play/pause and restart can be pressed, so a hand
    /// near the panel can't change anything mid-song (the transport ignores
    /// those changes while playing anyway). Registered on the keyboard like
    /// every other canvas: hidden until the keys are placed. Built in code from
    /// the song picker's glass; no scene change.
    /// </summary>
    public sealed class SongPanel : MonoBehaviour
    {
        /// <summary>From A0's front edge, metres: left of the keyboard, a hand
        /// above it, clear of where hands rest.</summary>
        private const float Leftward = 0.17f, Above = 0.13f, Nearer = 0.03f;
        /// <summary>Tipped back so its face points up toward the eyes.</summary>
        private const float TiltDegrees = -30f;

        private static readonly Vector2 Size = new Vector2(240f, 340f);
        private const float Col = 56f, BtnW = 104f, BtnH = 48f;

        public static SongPanel Instance { get; private set; }

        /// <summary>Settings asked for: SettingsPanel listens.</summary>
        public static event Action SettingsPressed;

        private KeyboardCalibration _calibration;
        private SongTransport _transport;
        private SongClock _clock;
        private Metronome _metronome;
        private SongPicker _picker;

        private RectTransform _canvas;
        private TextMeshProUGUI _status, _playLabel;
        private GlassButton _play, _restart, _songs, _slower, _faster, _metro, _wait, _fade, _settings;
        private PianoKeyLayout _placedFor;
        private string _shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add()
        {
            var go = new GameObject(nameof(SongPanel));
            go.AddComponent<SongPanel>();
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _calibration = FindAnyObjectByType<KeyboardCalibration>();
            _transport = FindAnyObjectByType<SongTransport>();
            _clock = FindAnyObjectByType<SongClock>();
            _metronome = FindAnyObjectByType<Metronome>();
            _picker = FindAnyObjectByType<SongPicker>(FindObjectsInactive.Include);
            if (_calibration == null || _transport == null || _clock == null || _picker == null)
            {
                Debug.LogWarning("[SongPanel] off: no calibration, transport, clock or song picker in the scene");
                enabled = false;
                return;
            }
            Build(new GlassBuilder(_picker));
        }

        private void Build(GlassBuilder g)
        {
            _canvas = g.Canvas(transform, "SongPanelCanvas", Size);
            Transform root = _canvas;
            _status = g.Text(root, "Status", 15f, g.Secondary, TextAlignmentOptions.TopLeft,
                             new Vector2(-Size.x * 0.5f + 16f, Size.y * 0.5f - 12f), Size.x - 32f);

            float y = Size.y * 0.5f - 66f;
            _play = g.Pill(root, "PlayPause", new Vector2(0f, y), new Vector2(BtnW * 2f + 8f, BtnH), "Play",
                           () => _transport.TogglePlayPause(), out _playLabel, 20f);
            y -= BtnH + 10f;
            _restart = g.Pill(root, "Restart", new Vector2(-Col, y), new Vector2(BtnW, BtnH), "Restart",
                              () => _transport.RestartAndPlay(), out _);
            _songs = g.Pill(root, "Songs", new Vector2(Col, y), new Vector2(BtnW, BtnH), "Songs", () => _picker.Open(), out _);
            y -= BtnH + 10f;
            _slower = g.Pill(root, "Slower", new Vector2(-Col, y), new Vector2(BtnW, BtnH), "Tempo -",
                             () => _transport.StepTempo(-1), out _);
            _faster = g.Pill(root, "Faster", new Vector2(Col, y), new Vector2(BtnW, BtnH), "Tempo +",
                             () => _transport.StepTempo(+1), out _);
            y -= BtnH + 10f;
            _metro = g.Pill(root, "Metronome", new Vector2(-Col, y), new Vector2(BtnW, BtnH), "Metronome",
                            () => _transport.ToggleMetronome(), out _, 15f);
            _wait = g.Pill(root, "Wait", new Vector2(Col, y), new Vector2(BtnW, BtnH), "Wait mode",
                           () => _transport.ToggleWaitMode(), out _, 15f);
            y -= BtnH + 10f;
            _fade = g.Pill(root, "Fade", new Vector2(-Col, y), new Vector2(BtnW, BtnH), "Memory fade",
                           () => _transport.ToggleMemoryFade(), out _, 15f);
            _settings = g.Pill(root, "Settings", new Vector2(Col, y), new Vector2(BtnW, BtnH), "Settings",
                               () => SettingsPressed?.Invoke(), out _);

            foreach (GlassButton b in new[] { _play, _restart, _songs, _slower, _faster, _metro, _wait, _fade, _settings })
                b.AlwaysShown = true;
        }

        private void LateUpdate()
        {
            bool placed = _calibration.IsPlacementValid && _calibration.Layout != null;
            // Settings takes this spot while it is open.
            bool show = placed && !SettingsPanel.IsOpen;
            if (_canvas.gameObject.activeSelf != show) _canvas.gameObject.SetActive(show);
            if (!placed) return;

            PianoKeyLayout layout = _calibration.Layout;
            if (layout != _placedFor)
            {
                _placedFor = layout;
                Place(layout);
            }
            if (show) Refresh();
        }

        private void Place(PianoKeyLayout layout)
        {
            float rad = TiltDegrees * Mathf.Deg2Rad;
            Vector3 up = layout.Up * Mathf.Cos(rad) + layout.Forward * Mathf.Sin(rad);
            Vector3 towardPlayer = layout.Forward * Mathf.Cos(rad) - layout.Up * Mathf.Sin(rad);
            Vector3 pos = layout.LowFrontEdge - layout.Right * Leftward + layout.Up * Above + layout.Forward * Nearer;
            // A canvas is read from behind its +Z: +Z points away from the player.
            _canvas.SetPositionAndRotation(pos, Quaternion.LookRotation(-towardPlayer, up));
        }

        /// <summary>Puts another panel in this one's place: same tilt, bottom
        /// edges level, so a taller panel grows upward, away from the keys.</summary>
        public void PlaceLike(Transform other, float otherHeightMm)
        {
            if (_placedFor == null) return;
            Vector3 up = _canvas.up;
            other.SetPositionAndRotation(_canvas.position + up * ((otherHeightMm - Size.y) * 0.5f * 0.001f), _canvas.rotation);
        }

        /// <summary>Labels and which buttons work, only when something changed:
        /// a glass tint change rebuilds its quad.</summary>
        private void Refresh()
        {
            SongTransport.State state = _transport.CurrentState;
            bool running = state == SongTransport.State.Playing || state == SongTransport.State.CountingIn;
            bool metro = _metronome != null && _metronome.IsOn;
            string key = $"{state}|{_clock.TempoMultiplier:0.00}|{metro}|{_transport.WaitMode}|{_clock.MemoryFadeOn}";
            if (key == _shown) return;
            _shown = key;

            _playLabel.text = running ? "Pause" : "Play";
            _status.text = $"{(running ? "Playing" : state == SongTransport.State.Finished ? "Finished" : "Stopped")}" +
                           $"   {TempoStep.Label(_clock.TempoMultiplier)}";
            GlassBuilder.Set(_play, true);
            GlassBuilder.Set(_restart, true);
            GlassBuilder.Set(_songs, !running);
            GlassBuilder.Set(_slower, !running);
            GlassBuilder.Set(_faster, !running);
            GlassBuilder.Set(_metro, !running, metro);
            GlassBuilder.Set(_wait, !running, _transport.WaitMode);
            GlassBuilder.Set(_fade, !running, _clock.MemoryFadeOn);
            GlassBuilder.Set(_settings, !running);
        }
    }
}
