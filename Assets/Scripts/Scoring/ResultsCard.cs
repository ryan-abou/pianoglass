using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using PianoGlass.UI;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// The results card (F30) with its bar heat strip (F32), on Liquid Glass
    /// v2. Appears when an attempt completes, disappears when the next one
    /// resets (restart, or play from the top). Not on "play" as such: the
    /// clock is still running when the last note is judged.
    ///
    /// Everything it says comes from ResultsSummary (pure, tested), worked
    /// out from the attempt just saved plus the earlier ones in the attempts
    /// folder. This class is only layout, matching GlassPreview's sample:
    ///
    ///   context line          song · tempo                  (secondary)
    ///   92%  Great            headline and verdict
    ///   [ vs last | timing | wrong notes ]   one grouped glass row,
    ///                                        label over value, hairline dividers
    ///   (1)(2)(3)...           heat strip: glass pills tinted on the ramp,
    ///                          the worst one ringed, bar numbers under
    ///   weakest: bar 3 (62%)   the worst bar in words -- never colour alone
    ///
    /// SceneSetup (Configure Results Card) makes the world-space Canvas, its
    /// panel glass and its keyboard follower; rows and cells are built here
    /// in code. Canvas units are millimetres (scale 0.001).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class ResultsCard : MonoBehaviour
    {
        [SerializeField] private ScoreKeeper scoreKeeper;
        [SerializeField] private GlassTokens tokens;
        [Tooltip("Assigned by SceneSetup, never looked up at runtime.")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("The shared glass text material (soft underlay). Assigned by SceneSetup.")]
        [SerializeField] private Material textMaterial;
        [Tooltip("LiquidGlass material for the nested row and the heat cells.")]
        [SerializeField] private Material glassMaterial;

        [Header("Layout (mm)")]
        [SerializeField] private Vector2 size = new Vector2(460f, 270f);

        private const float Left = -208f, Inner = 424f;
        private const float RowY = -22f, RowHeight = 50f;
        private const float CellY = -78f, CellHeight = 24f, CellGap = 8.6f;

        private Canvas _canvas;
        private GlassPanel _glass;
        private TextMeshProUGUI _context, _headline, _verdict, _worst;
        private readonly TextMeshProUGUI[] _values = new TextMeshProUGUI[3];
        private readonly List<GameObject> _cells = new List<GameObject>();
        private Color _primary, _secondary;

        public bool IsShowing => _canvas != null && _canvas.enabled;
        public ResultsSummary Last { get; private set; }

        private void Awake()
        {
            if (scoreKeeper == null) scoreKeeper = FindAnyObjectByType<ScoreKeeper>();
            _canvas = GetComponent<Canvas>();
            _glass = GetComponent<GlassPanel>();
            _primary = tokens != null ? tokens.textPrimary : GlassTokens.DefaultTextPrimary;
            _secondary = tokens != null ? tokens.textSecondary : GlassTokens.DefaultTextSecondary;
            if (font == null || glassMaterial == null)
                Debug.LogError("[Results] font or glass material missing -- run PianoGlass/Scene/Configure Results Card.");

            ((RectTransform)transform).sizeDelta = size;
            Build();
            Hide();
        }

        private void OnEnable()
        {
            if (scoreKeeper == null) return;
            scoreKeeper.AttemptEnded += OnAttemptEnded;
            scoreKeeper.AttemptReset += Hide;
        }

        private void OnDisable()
        {
            if (scoreKeeper == null) return;
            scoreKeeper.AttemptEnded -= OnAttemptEnded;
            scoreKeeper.AttemptReset -= Hide;
        }

        private void OnAttemptEnded(AttemptRecord rec, string path)
        {
            // An abandoned attempt is a restart: there is no result to show.
            if (rec.outcome != AttemptRecord.Completed) return;
            Show(ResultsSummary.From(rec, LoadPrevious(path)));
        }

        public void Show(ResultsSummary s)
        {
            Last = s;
            _context.text = s.ContextText;
            _headline.text = s.AccuracyText;
            _verdict.text = s.Verdict;
            _values[0].text = s.Change.HasValue ? ArrowOnly(s.ChangeText) : "first";
            _values[1].text = string.IsNullOrEmpty(s.TimingText) ? "–" : s.TimingText;
            _values[2].text = s.WrongNotes.ToString();
            _worst.text = s.WorstBarText ?? "";
            FillStrip(s.Bars);

            _canvas.enabled = true;
            SetGlass(true);
            Debug.Log($"[Results] {s.AccuracyText} {s.Verdict} | {s.ChangeText} | {s.TimingText} | " +
                      $"{s.WrongNotesText} | {s.WorstBarText}");
        }

        public void Hide()
        {
            if (_canvas != null) _canvas.enabled = false;
            SetGlass(false);
        }

        /// <summary>"▲ 6% vs last" -> "▲ 6%": the row's label already says "vs last".</summary>
        private static string ArrowOnly(string changeText) =>
            changeText.EndsWith(" vs last", StringComparison.Ordinal)
                ? changeText.Substring(0, changeText.Length - " vs last".Length)
                : changeText;

        /// <summary>Glass quads are renderers of their own, not part of the
        /// Canvas, so hiding the Canvas does not hide them.</summary>
        private void SetGlass(bool on)
        {
            foreach (GlassPanel g in GetComponentsInChildren<GlassPanel>(true)) g.enabled = on;
        }

        // ------------------------------------------------------------------

        private static List<AttemptRecord> LoadPrevious(string currentPath)
        {
            var list = new List<AttemptRecord>();
            try
            {
                string dir = ScoreKeeper.AttemptsDirectory;
                if (!Directory.Exists(dir)) return list;
                foreach (string file in Directory.GetFiles(dir, "*.json"))
                {
                    if (currentPath != null && string.Equals(Path.GetFullPath(file), Path.GetFullPath(currentPath),
                                                             StringComparison.OrdinalIgnoreCase))
                        continue;
                    AttemptRecord r = AttemptRecord.Parse(File.ReadAllText(file));
                    if (r != null) list.Add(r);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Results] could not read earlier attempts: {e.Message}");
            }
            return list;
        }

        private void Build()
        {
            float top = size.y * 0.5f - 22f;

            _context = Text("Context", 17f, _secondary, TextAlignmentOptions.TopLeft, new Vector2(Left, top), Inner);
            _headline = Text("Accuracy", 76f, _primary, TextAlignmentOptions.TopLeft, new Vector2(Left, top - 21f), 220f);
            _headline.fontStyle = FontStyles.Bold;
            _verdict = Text("Verdict", 30f, _primary, TextAlignmentOptions.TopLeft, new Vector2(-10f, top - 67f), 200f);

            // The grouped row: three items, label over value, split by
            // hairline dividers; the items have no borders of their own.
            Glass("Row", GlassStyle.Row, new Vector2(0f, RowY), new Vector2(Inner, RowHeight), 0.0014f, null);
            string[] labels = { "vs last", "timing", "wrong notes" };
            float third = Inner / 3f;
            for (int i = 0; i < 3; i++)
            {
                float cx = -Inner * 0.5f + third * (i + 0.5f);
                Text($"Label_{i}", 12f, _secondary, TextAlignmentOptions.Top,
                     new Vector2(cx - third * 0.5f, RowY + 19f), third).text = labels[i];
                _values[i] = Text($"Value_{i}", 19f, _primary, TextAlignmentOptions.Top,
                                  new Vector2(cx - third * 0.5f, RowY + 4f), third);
                if (i > 0)
                    Glass($"Divider_{i}", GlassStyle.Pill, new Vector2(cx - third * 0.5f, RowY),
                          new Vector2(0.8f, 30f), 0.001f, new Color(1f, 1f, 1f, 0.22f));
            }

            _worst = Text("Worst", 16f, _secondary, TextAlignmentOptions.TopLeft,
                          new Vector2(Left, -size.y * 0.5f + 26f), 300f);
        }

        private void FillStrip(IReadOnlyList<BarCell> bars)
        {
            foreach (GameObject c in _cells) Destroy(c);
            _cells.Clear();
            if (bars.Count == 0) return;

            float pitch = Inner / bars.Count;
            float cell = Mathf.Max(6f, pitch - CellGap);
            for (int i = 0; i < bars.Count; i++)
            {
                BarCell b = bars[i];
                float cx = -Inner * 0.5f + pitch * (i + 0.5f);

                // The worst bar: a bright ring (a slightly larger light pill
                // behind it) -- and named in words underneath.
                if (b.IsWorst)
                    _cells.Add(Glass($"Worst_{b.Bar}", GlassStyle.Pill, new Vector2(cx, CellY),
                                     new Vector2(cell + 6f, CellHeight + 6f), 0.0011f,
                                     new Color(1f, 1f, 1f, 0.55f)).gameObject);

                Color tint = b.Colour;
                tint.a = b.HasNotes ? 0.85f : 0.35f;
                _cells.Add(Glass($"Bar_{b.Bar}", GlassStyle.Pill, new Vector2(cx, CellY),
                                 new Vector2(cell, CellHeight), 0.0008f, tint).gameObject);

                TextMeshProUGUI n = Text($"BarNo_{b.Bar}", 12f, _secondary, TextAlignmentOptions.Top,
                                         new Vector2(cx - pitch * 0.5f, CellY - CellHeight * 0.5f - 3f), pitch);
                n.text = b.Bar.ToString();
                _cells.Add(n.gameObject);
            }
        }

        /// <summary>A glass surface centred at <paramref name="centre"/> (mm
        /// from the card's centre), <paramref name="depthBehind"/> metres
        /// behind the text: nested glass sits nearer than the card's 2 mm.</summary>
        private GlassPanel Glass(string name, GlassStyle style, Vector2 centre, Vector2 sizeMm,
                                 float depthBehind, Color? tint)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeMm;
            rt.anchoredPosition = centre;
            var glass = go.AddComponent<GlassPanel>();
            glass.Init(glassMaterial, tokens, style, Vector2.zero, depthBehind);
            if (tint.HasValue) glass.SetTint(tint);
            glass.enabled = IsShowing;
            return glass;
        }

        private TextMeshProUGUI Text(string name, float fontSize, Color colour, TextAlignmentOptions align,
                                     Vector2 topLeft, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, fontSize * 1.4f);
            rt.anchoredPosition = topLeft;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (textMaterial != null) t.fontSharedMaterial = textMaterial;
            t.fontSize = fontSize;
            t.color = colour;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
