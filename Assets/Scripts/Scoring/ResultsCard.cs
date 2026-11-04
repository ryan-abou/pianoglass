using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PianoGlass.UI;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// The results card (F30) with its bar heat strip (F32), on Liquid Glass.
    /// Appears when an attempt completes, disappears when the next one
    /// resets (restart, or play from the top). Not on "play" as such: the
    /// clock is still running when the last note is judged.
    ///
    /// Everything it says comes from ResultsSummary (pure, tested), worked
    /// out from the attempt just saved plus the earlier ones in the attempts
    /// folder. This class is only layout: SceneSetup (Configure Results
    /// Card) makes the world-space Canvas, its glass and its keyboard
    /// follower; the text and cells are built here in code, so the layout
    /// is reviewable C# rather than scene YAML. Canvas units are millimetres
    /// (scale 0.001), the same as the status panel.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class ResultsCard : MonoBehaviour
    {
        [SerializeField] private ScoreKeeper scoreKeeper;
        [SerializeField] private GlassTokens tokens;
        [Tooltip("Assigned by SceneSetup, never looked up at runtime.")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Layout (mm)")]
        [SerializeField] private Vector2 size = new Vector2(460f, 270f);
        [SerializeField] private float margin = 22f;
        [SerializeField] private float cellHeight = 30f;
        [SerializeField] private float cellGap = 6f;
        [SerializeField] private float outline = 3f;

        private Canvas _canvas;
        private GlassPanel _glass;
        private TextMeshProUGUI _context, _headline, _verdict, _detail, _worst;
        private RectTransform _strip;
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
            if (font == null)
                Debug.LogError("[Results] no font assigned -- run PianoGlass/Scene/Configure Results Card.");

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
            string timing = string.IsNullOrEmpty(s.TimingText) ? "" : $"   ·   {s.TimingText}";
            _detail.text = $"{s.ChangeText}{timing}   ·   {s.WrongNotesText}";
            _worst.text = s.WorstBarText ?? "";
            FillStrip(s.Bars);

            _canvas.enabled = true;
            if (_glass != null) _glass.enabled = true;
            Debug.Log($"[Results] {s.AccuracyText} {s.Verdict} | {_detail.text} | {s.WorstBarText}");
        }

        public void Hide()
        {
            if (_canvas != null) _canvas.enabled = false;
            if (_glass != null) _glass.enabled = false;
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
            float w = size.x, h = size.y;
            float top = h * 0.5f - margin;
            float left = -w * 0.5f + margin;
            float inner = w - 2f * margin;

            _context = Text("Context", 17f, _secondary, TextAlignmentOptions.TopLeft,
                            new Vector2(left, top), new Vector2(inner, 24f));
            _headline = Text("Accuracy", 76f, _primary, TextAlignmentOptions.TopLeft,
                             new Vector2(left, top - 26f), new Vector2(200f, 84f));
            _headline.fontStyle = FontStyles.Bold;
            _verdict = Text("Verdict", 30f, _primary, TextAlignmentOptions.BottomLeft,
                            new Vector2(left + 190f, top - 26f), new Vector2(inner - 190f, 72f));
            _detail = Text("Detail", 19f, _primary, TextAlignmentOptions.TopLeft,
                           new Vector2(left, top - 114f), new Vector2(inner, 26f));

            var stripGo = new GameObject("BarStrip", typeof(RectTransform));
            _strip = (RectTransform)stripGo.transform;
            _strip.SetParent(transform, false);
            Place(_strip, new Vector2(left, top - 146f), new Vector2(inner, cellHeight + 18f));

            _worst = Text("Worst", 16f, _secondary, TextAlignmentOptions.BottomLeft,
                          new Vector2(left, -h * 0.5f + margin + 20f), new Vector2(inner, 20f));
        }

        private void FillStrip(IReadOnlyList<BarCell> bars)
        {
            foreach (GameObject c in _cells) Destroy(c);
            _cells.Clear();
            if (bars.Count == 0) return;

            float inner = _strip.sizeDelta.x;
            float cell = (inner - cellGap * (bars.Count - 1)) / bars.Count;
            for (int i = 0; i < bars.Count; i++)
            {
                BarCell b = bars[i];
                float x = i * (cell + cellGap);

                // The worst bar gets a bright frame behind it, and is named
                // in words underneath -- never colour alone.
                if (b.IsWorst)
                    _cells.Add(Block($"Worst_{b.Bar}", _primary,
                                     new Vector2(x - outline, outline), new Vector2(cell + 2f * outline, cellHeight + 2f * outline)));
                _cells.Add(Block($"Bar_{b.Bar}", b.Colour, new Vector2(x, 0f), new Vector2(cell, cellHeight)));

                TextMeshProUGUI n = Text($"BarNo_{b.Bar}", 12f, _secondary, TextAlignmentOptions.Top,
                                         Vector2.zero, new Vector2(cell, 16f), _strip);
                Place(n.rectTransform, new Vector2(x, -cellHeight - 2f), new Vector2(cell, 16f));
                n.text = b.Bar.ToString();
                _cells.Add(n.gameObject);
            }
        }

        private GameObject Block(string name, Color colour, Vector2 topLeft, Vector2 blockSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_strip, false);
            Place(rt, topLeft, blockSize);
            var img = go.GetComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
            return go;
        }

        private TextMeshProUGUI Text(string name, float fontSize, Color colour, TextAlignmentOptions align,
                                     Vector2 topLeft, Vector2 boxSize, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent != null ? parent : transform, false);
            Place(rt, topLeft, boxSize);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = fontSize;
            t.color = colour;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Top-left anchored placement in the parent's units: position
        /// is the box's top-left corner relative to the parent's centre (the
        /// card) or top-left (the strip).</summary>
        private static void Place(RectTransform rt, Vector2 topLeft, Vector2 boxSize)
        {
            bool inStrip = rt.parent != null && rt.parent.name == "BarStrip";
            Vector2 anchor = inStrip ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = boxSize;
            rt.anchoredPosition = topLeft;
        }
    }
}
