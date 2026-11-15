using TMPro;
using UnityEngine;
using PianoGlass.Piano;

namespace PianoGlass.UI
{
    /// <summary>
    /// Settings (#5, S1): everything the controllers did that a song doesn't,
    /// on Liquid Glass buttons a fingertip can poke, so the app needs no
    /// controllers at all. Calibration (by pressing keys, HandCalibration),
    /// "looks right", the Middle C fix, a nudge pad, clear nudge and the
    /// display mode; in development builds also passthrough, the test
    /// checklist and recording mode (compiled out of release).
    ///
    /// Opened from the song panel and shown in its place, within the same
    /// reach. Built in code from the song picker's glass; no scene change.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        /// <summary>One poke of the nudge pad: metres, and degrees for turning.</summary>
        private const float NudgeStep = 0.002f, TurnStep = 0.2f;

        private static readonly Vector2 Size = new Vector2(300f, 500f);
        private const float BtnH = 44f, Gap = 8f;

        public static bool IsOpen { get; private set; }

        private KeyboardCalibration _calibration;
        private CalibrationFlowController _flow;
        private PianoKeyHighlighter _highlighter;
        private OvrKeyOverlayInput _overlayInput;

        private RectTransform _canvas;
        private GrabbablePanel _grab;
        private TextMeshProUGUI _prompt, _middleLabel, _displayLabel;
        private GlassButton _confirm, _middle;
        private readonly System.Collections.Generic.List<GlassButton> _nudgeButtons =
            new System.Collections.Generic.List<GlassButton>();
        private string _shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add()
        {
            IsOpen = false;
            var go = new GameObject(nameof(SettingsPanel));
            go.AddComponent<SettingsPanel>();
        }

        private void Start()
        {
            _calibration = FindAnyObjectByType<KeyboardCalibration>();
            _flow = FindAnyObjectByType<CalibrationFlowController>();
            _highlighter = FindAnyObjectByType<PianoKeyHighlighter>();
            _overlayInput = FindAnyObjectByType<OvrKeyOverlayInput>();
            var picker = FindAnyObjectByType<SongPicker>(FindObjectsInactive.Include);
            if (_calibration == null || picker == null)
            {
                Debug.LogWarning("[Settings] off: no calibration or song picker in the scene");
                enabled = false;
                return;
            }
            Build(new GlassBuilder(picker));
            SongPanel.SettingsPressed += Open;
        }

        private void OnDestroy() => SongPanel.SettingsPressed -= Open;

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            IsOpen = open;
            _shown = null;
            Debug.Log($"[Settings] {(open ? "open" : "closed")}");
        }

        private void Build(GlassBuilder g)
        {
            _canvas = g.Canvas(transform, "SettingsCanvas", Size);
            Transform root = _canvas;
            float left = -Size.x * 0.5f + 16f, w2 = (Size.x - 32f - Gap) * 0.5f, w3 = (Size.x - 32f - Gap * 2f) / 3f;
            float c2a = -w2 * 0.5f - Gap * 0.5f, c2b = -c2a;
            float c3a = -w3 - Gap, c3c = w3 + Gap;
            float y = Size.y * 0.5f - 14f;

            g.Text(root, "Title", 22f, g.Primary, TextAlignmentOptions.TopLeft, new Vector2(left, y), 160f, true).text = "Settings";
            g.Pill(root, "Close", new Vector2(Size.x * 0.5f - 16f - 45f, y - 16f), new Vector2(90f, 36f), "Close", Close, out _, 16f);
            y -= 46f;

            _prompt = g.Text(root, "Prompt", 14f, g.Secondary, TextAlignmentOptions.TopLeft, new Vector2(left, y), Size.x - 32f);
            _prompt.textWrappingMode = TextWrappingModes.Normal;
            ((RectTransform)_prompt.transform).sizeDelta = new Vector2(Size.x - 32f, 40f);
            y -= 46f + BtnH * 0.5f;

            g.Pill(root, "Calibrate", new Vector2(c2a, y), new Vector2(w2, BtnH), "Calibrate with hands",
                   () => _calibration.Recalibrate(), out _, 14f);
            _confirm = g.Pill(root, "LooksRight", new Vector2(c2b, y), new Vector2(w2, BtnH), "Looks right",
                              () => _calibration.Confirm(), out _, 15f);
            y -= BtnH + Gap;
            _middle = g.Pill(root, "MiddleC", new Vector2(c2a, y), new Vector2(w2, BtnH), "Middle C fix",
                             () => _calibration.ToggleCorrection(), out _middleLabel, 15f);
            g.Pill(root, "ClearNudge", new Vector2(c2b, y), new Vector2(w2, BtnH), "Clear nudge",
                   () => _calibration.ClearNudge(), out _, 15f);
            y -= BtnH + Gap + 8f;

            g.Text(root, "NudgeTitle", 14f, g.Secondary, TextAlignmentOptions.TopLeft,
                   new Vector2(left, y + BtnH * 0.5f + 2f), Size.x - 32f).text = "Move the keys";
            y -= 14f;
            NudgeRow(g, root, y, c3a, c3c, w3, ("Left", Nudge(-1, 0, 0)), ("Nearer", Nudge(0, 0, 1)), ("Right", Nudge(1, 0, 0)));
            y -= BtnH + Gap;
            NudgeRow(g, root, y, c3a, c3c, w3, ("Turn left", Turn(-1)), ("Further", Nudge(0, 0, -1)), ("Turn right", Turn(1)));
            y -= BtnH + Gap;
            NudgeRow(g, root, y, c3a, c3c, w3, ("Down", Nudge(0, -1, 0)), ("", null), ("Up", Nudge(0, 1, 0)));
            y -= BtnH + Gap + 8f;

            g.Pill(root, "Display", new Vector2(c2a, y), new Vector2(w2, BtnH), "Display",
                   () => { if (_highlighter != null) _highlighter.CycleDisplayMode(); _shown = null; }, out _displayLabel, 13f);
            g.Pill(root, "ResetPanels", new Vector2(c2b, y), new Vector2(w2, BtnH), "Reset panels",
                   GrabbablePanel.ResetAll, out _, 14f);
            y -= BtnH + Gap;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Debug.isDebugBuild)
            {
                g.Pill(root, "Passthrough", new Vector2(c3a, y), new Vector2(w3, BtnH), "Passthrough",
                       () => { if (_overlayInput != null) _overlayInput.TogglePassthrough(); }, out _, 13f);
                g.Pill(root, "Checklist", new Vector2(0f, y), new Vector2(w3, BtnH), "Checklist",
                       PianoGlass.DevTools.DevOverlayController.ToggleChecklistFromMenu, out _, 13f);
                g.Pill(root, "Recording", new Vector2(c3c, y), new Vector2(w3, BtnH), "Recording",
                       PianoGlass.DevTools.DevOverlayController.ToggleRecordingFromMenu, out _, 13f);
            }
#endif
            // Its usual spot is the song panel's, set every frame below.
            _grab = GrabbablePanel.Add(_canvas.gameObject, "Settings", _calibration);
        }

        /// <summary>Short enough for a half-width button.</summary>
        private static string ShortName(string mode) => mode switch
        {
            "FullOverlay" => "Full",
            "ActiveKeysOnly" => "Active",
            "EdgeMarkers" => "Edges",
            _ => mode,
        };

        private void NudgeRow(GlassBuilder g, Transform root, float y, float xa, float xc, float w,
                              (string label, System.Action act) a, (string label, System.Action act) b,
                              (string label, System.Action act) c)
        {
            if (a.act != null) _nudgeButtons.Add(g.Pill(root, a.label, new Vector2(xa, y), new Vector2(w, BtnH), a.label, a.act, out _, 14f));
            if (b.act != null) _nudgeButtons.Add(g.Pill(root, b.label, new Vector2(0f, y), new Vector2(w, BtnH), b.label, b.act, out _, 14f));
            if (c.act != null) _nudgeButtons.Add(g.Pill(root, c.label, new Vector2(xc, y), new Vector2(w, BtnH), c.label, c.act, out _, 14f));
        }

        /// <summary>One step along the keys (x), up (y) or toward the player (z), saved straight away.</summary>
        private System.Action Nudge(int x, int y, int z) => () =>
        {
            _calibration.Nudge(new Vector3(x, y, z) * NudgeStep);
            _calibration.Save();
            Debug.Log($"[Settings] nudge -> {_calibration.NudgeMillimetres} mm");
        };

        private System.Action Turn(int direction) => () =>
        {
            _calibration.AdjustYaw(direction * TurnStep);
            _calibration.Save();
            Debug.Log($"[Settings] turn -> {_calibration.YawDegrees:+0.00;-0.00;0} deg");
        };

        private void LateUpdate()
        {
            bool show = IsOpen && _calibration.Layout != null && !LaunchResync.WaitingForRecenter;
            if (_canvas.gameObject.activeSelf != show) _canvas.gameObject.SetActive(show);
            if (!show) return;

            // In the song panel's place: same spot, same tilt. Not while it is
            // held or has been put somewhere: that put it back every frame and
            // it never reached the hand (2026-11-14).
            bool own = _grab != null && (_grab.Held || _grab.HasSaved);
            if (!own && SongPanel.Instance != null) SongPanel.Instance.PlaceLike(_canvas, Size.y);
            Refresh();
        }

        private void Refresh()
        {
            bool anchored = _flow != null && _flow.Active;
            bool correcting = anchored && _flow.IsCorrecting;
            bool calibrated = _calibration.IsCalibrated;
            string mode = _highlighter != null ? _highlighter.Mode.ToString() : "-";
            string key = $"{_calibration.Prompt}|{_calibration.AwaitingConfirmation}|{correcting}|{calibrated}|{anchored}|{mode}";
            if (key == _shown) return;
            _shown = key;

            _prompt.text = correcting ? "Press Middle C with one index finger (or touch it with the tip and pull the trigger)"
                                      : _calibration.Prompt;
            GlassBuilder.Set(_confirm, _calibration.AwaitingConfirmation);
            GlassBuilder.Set(_middle, anchored && calibrated, correcting);
            _middleLabel.text = correcting ? "Cancel fix" : "Middle C fix";
            foreach (GlassButton b in _nudgeButtons) GlassBuilder.Set(b, calibrated);
            _displayLabel.text = $"Display: {ShortName(mode)}";
        }
    }
}
