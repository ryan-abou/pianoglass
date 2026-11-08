using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PianoGlass.Piano;
using PianoGlass.Transport;
using PianoGlass.UI;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// The end-of-song results screen on Liquid Glass: the main card, the
    /// buttons under it, and the details panel to its right. Appears when an
    /// attempt completes, disappears when the next one resets.
    ///
    /// Everything it says comes from ResultsSummary (pure, tested), worked
    /// out from the attempt just saved plus the earlier ones in the attempts
    /// folder. This class is only layout. SceneSetup (Configure Results Card)
    /// makes the world-space Canvas and its wiring; everything else is built
    /// here in code. Canvas units are millimetres (scale 0.001).
    ///
    /// The card and the status panel take turns, because they share the same
    /// space above the keys: the status panel hides while the card is up, and
    /// when calibration has instructions for the player the card steps aside
    /// and comes back afterwards. Nudging leaves the card up. The song picker
    /// takes the same space, so the card and the status panel both give way
    /// to it, and the card comes back if the picker closes with no new song.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class ResultsCard : MonoBehaviour
    {
        [SerializeField] private ScoreKeeper scoreKeeper;
        [SerializeField] private SongTransport transport;
        [SerializeField] private KeyboardCalibration calibration;
        [SerializeField] private ControllerTipCalibration tip;
        [Tooltip("The status panel's Canvas, hidden while the card is up. Assigned by SceneSetup.")]
        [SerializeField] private Canvas statusCanvas;
        [SerializeField] private GlassTokens tokens;
        [Tooltip("Assigned by SceneSetup, never looked up at runtime.")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("The shared glass text material (soft underlay). Assigned by SceneSetup.")]
        [SerializeField] private Material textMaterial;
        [Tooltip("LiquidGlass material for every surface the card builds.")]
        [SerializeField] private Material glassMaterial;
        [Tooltip("Opened by the Songs button. Assigned by SceneSetup.")]
        [SerializeField] private SongPicker picker;

        /// <summary>First and last bar. PracticeLoop starts it.</summary>
        public event Action<int, int> LoopRequested;
        /// <summary>"left" or "right". CurrentSong switches and restarts.</summary>
        public event Action<string> SwitchHandRequested;

        /// <summary>The "mastered" suggestion's button opens the picker.</summary>
        private const bool HasSongs = true;

        // ---- Layout, mm from the card's centre ----------------------------
        private static readonly Vector2 Size = new Vector2(460f, 360f);
        private const float Left = -208f, Right = 208f, Inner = 416f;

        private const float TitleTop = 162f, SubTop = 134f;
        private const float HeroY = 64f, TileSize = 78f;
        private const float StripY = -12f, StripHeight = 50f;
        private const float PillY = -62f, PillHeight = 24f, PillGap = 8.6f;
        private const float SuggestionY = -135f, SuggestionHeight = 44f;

        private const float ButtonGap = 14f, ButtonHeight = 46f, ButtonWidth = 142f;

        private const float DetailsGap = 14f;
        private static readonly Vector2 DetailsSize = new Vector2(Size.x * 0.6f, Size.y);
        /// <summary>Canvas units behind the card, so it slides out from behind it.</summary>
        private const float DetailsBehind = 6f;

        // Glass depths, metres behind the text.
        private const float CardDepth = 0.002f, RowDepth = 0.0014f, PillDepth = 0.001f, RingDepth = 0.0012f;

        // Draw order, always below the Canvas text at 0. Fixed rather than
        // left to distance: seen off-centre, the card's own quad can be nearer
        // the eye than a pill on it, and would then draw over that pill and
        // dim it.
        private const int OrderDetails = -10, OrderDetailsRow = -9, OrderDetailsBar = -8, OrderDetailsFill = -7;
        private const int OrderCard = -6, OrderRow = -5, OrderRing = -4, OrderPill = -3;

        private static readonly Color Gold = GlassTokens.Hex("#FFE3A3");
        private static readonly Color UpGreen = GlassTokens.Hex("#B6E8CC");
        private static readonly Color EarlyOrange = GlassTokens.Hex("#E9A662");
        private static readonly Color LateBlue = GlassTokens.Hex("#8DB7FF");
        private static readonly Color CentreGrey = GlassTokens.Hex("#DFE2E6");
        private static readonly Color LoopOrange = new Color(0xB4 / 255f, 0x7A / 255f, 0x42 / 255f, 0.95f);
        private static readonly Color ButtonFill = new Color(0.60f, 0.65f, 0.70f, 0.22f);
        private static readonly Color SoftWhiteFill = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color HighlightFill = new Color(1f, 1f, 1f, 0.32f);
        private static readonly Color BarTrack = new Color(0f, 0f, 0f, 0.28f);

        // ---- Built once ----------------------------------------------------
        private Canvas _canvas;
        private bool _built;
        private Color _primary, _secondary;

        private TextMeshProUGUI _title, _sub, _rank, _accuracy, _accuracyLine, _suggestion;
        private readonly TextMeshProUGUI[] _stats = new TextMeshProUGUI[3];
        private GameObject _chip;
        private GlassButton _suggestionButton, _retry, _songs, _moreInfo;
        private TextMeshProUGUI _suggestionButtonText, _moreInfoText;
        private RectTransform _pillRoot, _pillPercents;
        private CanvasGroup _pillPercentGroup;

        private DetailsPanelTransition _details;
        private CanvasGroup[] _detailsSections;
        private Vector2 _detailsOpenPosition, _detailsClosedPosition;
        private TextMeshProUGUI _noteTotal, _spreadAverage, _attemptsTrend, _attemptsMessage;
        private readonly TextMeshProUGUI[] _noteCounts = new TextMeshProUGUI[4];
        private TextMeshProUGUI _firstNoteName;
        private readonly GlassPanel[] _noteFills = new GlassPanel[4];
        private readonly GlassPanel[] _histogramBars = new GlassPanel[TimingSpread.Bins];
        private RectTransform _chartRoot;
        private float _chartHeight, _chartWidth;
        private float _noteBarLeft, _noteBarWidth;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<GameObject> _chartSpawned = new List<GameObject>();
        private Suggestion _current;
        private Suggestion _suggested;
        private IReadOnlyList<BarGroup> _groups;
        private readonly BarSelection _selection = new BarSelection();
        /// <summary>One per pill: shown for the chosen ones, or for the weakest
        /// when nothing is chosen.</summary>
        private readonly List<GameObject> _rings = new List<GameObject>();
        private bool _hasResult;
        private bool? _statusOn;
        private bool _pickerWasUp;

        public bool IsShowing => _canvas != null && _canvas.enabled;
        public ResultsSummary Last { get; private set; }

        private void Awake()
        {
            if (scoreKeeper == null) scoreKeeper = FindAnyObjectByType<ScoreKeeper>();
            if (transport == null) transport = FindAnyObjectByType<SongTransport>();
            if (calibration == null) calibration = FindAnyObjectByType<KeyboardCalibration>();
            if (tip == null) tip = FindAnyObjectByType<ControllerTipCalibration>();
            if (picker == null) picker = FindAnyObjectByType<SongPicker>();
            if (font == null || glassMaterial == null)
                Debug.LogError("[Results] font or glass material missing -- run PianoGlass/Scene/Configure Results Card.");
            EnsureBuilt();
            Hide();
        }

        /// <summary>Builds the card if it has not been. Awake does this at
        /// runtime; the layout preview calls it in edit mode, with
        /// <see cref="UsePreviewAssets"/> first.</summary>
        public void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            _canvas = GetComponent<Canvas>();
            _primary = tokens != null ? tokens.textPrimary : GlassTokens.DefaultTextPrimary;
            _secondary = tokens != null ? tokens.textSecondary : GlassTokens.DefaultTextSecondary;
            ((RectTransform)transform).sizeDelta = Size;
            if (TryGetComponent(out GlassPanel cardGlass)) cardGlass.SetSortingOrder(OrderCard);
            BuildDetails();     // first: earlier in sibling order, so its text draws under the card's
            BuildCard();
            BuildButtons();
        }

        public void UsePreviewAssets(GlassTokens glassTokens, TMP_FontAsset fontAsset, Material text, Material glass)
        {
            tokens = glassTokens;
            font = fontAsset;
            textMaterial = text;
            glassMaterial = glass;
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
            // A loop pass: the next one is already coming, and the card would
            // cover it. The status panel shows how it went.
            if (rec.IsLoop) return;
            Show(ResultsSummary.From(rec, LoadPrevious(path), HasSongs));
        }

        /// <summary>One more fact for this result's line under the title.</summary>
        public void AddToSubLine(string text)
        {
            if (_sub != null) _sub.text += " · " + text;
        }

        public void Show(ResultsSummary s)
        {
            EnsureBuilt();
            Last = s;
            _suggested = s.Suggestion;
            _groups = s.Groups;
            _selection.Clear();

            // Header and hero
            _title.text = s.Title;
            _sub.text = s.SubLine;
            _chip.SetActive(s.IsNewBest);
            _rank.text = s.Rank;
            _rank.color = s.IsTopRank ? Gold : _primary;
            _accuracy.text = s.AccuracyText;
            _accuracy.color = s.IsTopRank ? Gold : _primary;
            _accuracyLine.text = "accuracy" + ChangeSuffix(s);

            // Stat strip
            _stats[0].text = s.TimingText;
            _stats[1].text = s.WrongNotes.ToString();
            _stats[2].text = s.StreakText;

            FillPills(s.Groups);

            ApplySuggestion(s.Suggestion);
            ApplyRings();

            _songs.SetFill(s.Suggestion.Rule == SuggestionRule.Mastered ? HighlightFill : ButtonFill);

            FillDetails(s);
            _details.SetInstant(false);
            ApplyMoreInfo(false);

            _hasResult = true;
            ApplyVisibility();
            Debug.Log($"[Results] {s.AccuracyText} {s.Rank} | {s.ChangeText ?? "first"} | {s.TimingText} | " +
                      $"{s.WrongNotes} wrong | streak {s.BestStreak} | {s.Suggestion.Message}");
        }

        private void ApplySuggestion(Suggestion suggestion)
        {
            _current = suggestion;
            bool hasButton = suggestion.Action != SuggestionAction.None;
            _suggestion.text = suggestion.Message;
            _suggestionButton.gameObject.SetActive(hasButton);
            if (hasButton)
            {
                _suggestionButtonText.text = suggestion.ButtonText;
                _suggestionButton.SetFill(suggestion.Action == SuggestionAction.LoopBars ? LoopOrange : ButtonFill);
            }
        }

        private void ApplyRings()
        {
            for (int i = 0; i < _rings.Count; i++)
                _rings[i].SetActive(_selection.IsEmpty ? _groups[i].IsWeakest : _selection.Contains(i));
        }

        /// <summary>A bar pill pressed: choose it, or put the suggestion back
        /// if that cleared the choice.</summary>
        private void OnPill(int index)
        {
            _selection.Tap(index);
            ApplySuggestion(_selection.ToSuggestion(_groups, _suggested.Rule) ?? _suggested);
            ApplyRings();
        }

        public void Hide()
        {
            _hasResult = false;
            ApplyVisibility();
        }

        private void Update()
        {
            if (_hasResult || PickerUp != _pickerWasUp) ApplyVisibility();
        }

        private bool PickerUp => picker != null && picker.IsShowing;

        private bool CalibrationNeedsPlayer =>
            (calibration != null && calibration.NeedsPlayer) || (tip != null && tip.IsBusy);

        /// <summary>Who is on show: the card, or the status panel. Only acts
        /// when that changes, except that closing is always safe to repeat.</summary>
        private void ApplyVisibility()
        {
            bool pickerUp = PickerUp;
            _pickerWasUp = pickerUp;
            bool card = _hasResult && !CalibrationNeedsPlayer && !pickerUp;
            bool status = !card && !pickerUp;
            if (!card)
            {
                if (_details != null) _details.SetInstant(false);
                ApplyMoreInfo(false);
            }
            if (_canvas != null && _canvas.enabled == card && _statusOn == status) return;
            _statusOn = status;

            if (_canvas != null) _canvas.enabled = card;
            SetGlass(card);
            if (statusCanvas != null)
            {
                statusCanvas.enabled = status;
                foreach (GlassPanel g in statusCanvas.GetComponentsInChildren<GlassPanel>(true)) g.enabled = status;
            }
        }

        /// <summary>For the layout preview: details open with no animation.</summary>
        public void SetDetailsInstant(bool open)
        {
            _details.SetInstant(open);
            ApplyMoreInfo(open);
        }

        /// <summary>" · ▲ 6% vs last". Soft green going up, neutral otherwise;
        /// nothing on a first attempt.</summary>
        private string ChangeSuffix(ResultsSummary s)
        {
            if (s.ChangeText == null) return "";
            bool up = Math.Round(s.Change.Value, MidpointRounding.AwayFromZero) > 0;
            return up ? $" · <color=#{ColorUtility.ToHtmlStringRGB(UpGreen)}>{s.ChangeText}</color>" : $" · {s.ChangeText}";
        }

        /// <summary>Glass quads are renderers of their own, not part of the
        /// Canvas, so hiding the Canvas does not hide them.</summary>
        private void SetGlass(bool on)
        {
            foreach (GlassPanel g in GetComponentsInChildren<GlassPanel>(true)) g.enabled = on;
        }

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

        // ------------------------------------------------------------------
        // Button actions
        // ------------------------------------------------------------------

        private void OnRetry()
        {
            if (transport != null) transport.RestartAndPlay();
        }

        private void OnMoreInfo()
        {
            _details.Toggle();
            ApplyMoreInfo(_details.IsOpen);
        }

        private void ApplyMoreInfo(bool open)
        {
            if (_moreInfo == null) return;
            _moreInfoText.text = open ? "Less info" : "More info";
            _moreInfo.SetFill(open ? SoftWhiteFill : ButtonFill);
        }

        private void OnSongs()
        {
            if (picker != null) picker.Open();
        }

        private void OnSuggestion()
        {
            if (_current == null) return;
            switch (_current.Action)
            {
                case SuggestionAction.LoopBars:
                    Debug.Log($"[Results] loop requested: bars {_current.FromBar}-{_current.ToBar}");
                    LoopRequested?.Invoke(_current.FromBar, _current.ToBar);
                    break;
                case SuggestionAction.TryTempo:
                    if (transport != null) transport.RestartAtTempo(_current.TargetTempoPercent);
                    break;
                case SuggestionAction.SwitchHand:
                    Debug.Log($"[Results] switch to {_current.TargetHand} hand requested");
                    SwitchHandRequested?.Invoke(_current.TargetHand);
                    break;
                case SuggestionAction.NextSong:
                    OnSongs();
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Main card
        // ------------------------------------------------------------------

        private void BuildCard()
        {
            Transform root = transform;

            _title = Text(root, "Title", 22f, _primary, TextAlignmentOptions.TopLeft, new Vector2(Left, TitleTop), 298f, true);
            _title.overflowMode = TextOverflowModes.Ellipsis;
            _sub = Text(root, "Sub", 14f, _secondary, TextAlignmentOptions.TopLeft, new Vector2(Left, SubTop), 298f);

            // "New best" chip
            GlassPanel chip = Glass(root, "Chip", GlassStyle.Pill, new Vector2(Right - 52f, TitleTop - 18f),
                                    new Vector2(104f, 32f), RowDepth, null);
            _chip = chip.gameObject;
            Text(chip.transform, "ChipText", 16f, _primary, TextAlignmentOptions.Center, new Vector2(-52f, 11f), 104f, true)
                .text = "★ New best";

            // Hero: the rank tile, the accuracy, the line under it.
            float tileX = Left + TileSize * 0.5f;
            Glass(root, "RankTile", GlassStyle.Row, new Vector2(tileX, HeroY), new Vector2(TileSize, TileSize), RowDepth, null);
            _rank = Text(root, "Rank", 50f, _primary, TextAlignmentOptions.Center,
                         new Vector2(tileX - TileSize * 0.5f, HeroY + 35f), TileSize, true);
            float heroLeft = Left + TileSize + 18f;
            _accuracy = Text(root, "Accuracy", 64f, _primary, TextAlignmentOptions.TopLeft,
                             new Vector2(heroLeft, HeroY + 49f), 260f, true);
            _accuracyLine = Text(root, "AccuracyLine", 14f, _secondary, TextAlignmentOptions.TopLeft,
                                 new Vector2(heroLeft, HeroY - 21f), 300f);

            // Stat strip: three items, label over value, hairline dividers.
            Glass(root, "Strip", GlassStyle.Row, new Vector2(0f, StripY), new Vector2(Inner, StripHeight), RowDepth, null);
            string[] labels = { "timing", "wrong notes", "best streak" };
            float third = Inner / 3f;
            for (int i = 0; i < 3; i++)
            {
                float x = Left + third * i;
                Text(root, $"Label_{i}", 13f, _secondary, TextAlignmentOptions.Top, new Vector2(x, StripY + 20f), third)
                    .text = labels[i];
                _stats[i] = Text(root, $"Stat_{i}", 19f, _primary, TextAlignmentOptions.Top, new Vector2(x, StripY + 4f), third, true);
                if (i > 0)
                    Glass(root, $"Divider_{i}", GlassStyle.Pill, new Vector2(x, StripY), new Vector2(0.8f, 30f),
                          PillDepth, new Color(1f, 1f, 1f, 0.22f));
            }

            // The pills are made per result; the % labels share one group so
            // they fade in together when the details panel opens.
            _pillRoot = Container(root, "Pills");
            _pillPercents = Container(root, "PillPercents");
            _pillPercentGroup = _pillPercents.gameObject.AddComponent<CanvasGroup>();
            _pillPercentGroup.blocksRaycasts = false;
            _details.Configure(_detailsOpenPosition, _detailsClosedPosition, _detailsSections, _pillPercentGroup);

            // Suggestion row: one message, at most one button.
            Glass(root, "Suggestion", GlassStyle.Row, new Vector2(0f, SuggestionY), new Vector2(Inner, SuggestionHeight), RowDepth, null);
            // Midline: the row stays put whether or not the message has a star.
            _suggestion = Text(root, "SuggestionText", 17f, _primary, TextAlignmentOptions.MidlineLeft,
                               new Vector2(Left + 14f, SuggestionY + 12f), Inner - 14f - 146f - 12f);

            _suggestionButton = Button(root, "SuggestionButton", new Vector2(Right - 4f - 73f, SuggestionY),
                                       new Vector2(146f, SuggestionHeight - 8f), PillDepth, 14f, true,
                                       out _suggestionButtonText, OnSuggestion);
        }

        private void FillPills(IReadOnlyList<BarGroup> groups)
        {
            foreach (GameObject g in _spawned) DestroySafely(g);
            _spawned.Clear();
            _rings.Clear();
            if (groups.Count == 0) return;

            float pitch = Inner / groups.Count;
            float cell = Mathf.Max(6f, pitch - PillGap);
            // Narrow pills: fit "100%" under them without touching.
            float percentSize = groups.Count > 10 ? 9f : 11f;

            for (int i = 0; i < groups.Count; i++)
            {
                BarGroup b = groups[i];
                float cx = Left + pitch * (i + 0.5f);

                // The weakest group, or a chosen one: a bright ring, a larger
                // white pill behind it.
                GameObject ring = Glass(_pillRoot, $"Ring_{b.FirstBar}", GlassStyle.Pill, new Vector2(cx, PillY),
                                        new Vector2(cell + 6f, PillHeight + 6f), RingDepth,
                                        new Color(1f, 1f, 1f, 0.85f)).gameObject;
                _rings.Add(ring);
                _spawned.Add(ring);

                Color tint = b.HasNotes ? BarGrouping.ColourFor(b.WorstAccuracy) : new Color(0.5f, 0.55f, 0.6f, 1f);
                tint.a = b.HasNotes ? 1f : 0.3f;
                if (b.HasNotes)
                {
                    int index = i;
                    GlassButton pill = Button(_pillRoot, $"Pill_{b.FirstBar}", new Vector2(cx, PillY),
                                              new Vector2(cell, PillHeight), PillDepth, 11f, false,
                                              out TextMeshProUGUI _, () => OnPill(index));
                    pill.SetFill(tint);
                    _spawned.Add(pill.gameObject);
                }
                else
                {
                    _spawned.Add(Glass(_pillRoot, $"Pill_{b.FirstBar}", GlassStyle.Pill, new Vector2(cx, PillY),
                                       new Vector2(cell, PillHeight), PillDepth, tint).gameObject);
                }

                float labelTop = PillY - PillHeight * 0.5f - 4f;
                TextMeshProUGUI n = Text(_pillRoot, $"BarNo_{b.FirstBar}", 13f, b.IsWeakest ? _primary : _secondary,
                                         TextAlignmentOptions.Top, new Vector2(cx - pitch * 0.5f, labelTop), pitch, b.IsWeakest);
                n.text = b.FirstBar.ToString();
                _spawned.Add(n.gameObject);

                if (b.HasNotes)
                {
                    TextMeshProUGUI p = Text(_pillPercents, $"Percent_{b.FirstBar}", percentSize, _secondary,
                                             TextAlignmentOptions.Top, new Vector2(cx - pitch * 0.5f, labelTop - 16f), pitch, b.IsWeakest);
                    p.text = $"{ResultRank.DisplayPercent(b.WorstAccuracy)}%";
                    _spawned.Add(p.gameObject);
                }
            }

            // Pills made after SetGlass(false) must match the card's state.
            foreach (GameObject g in _spawned)
                if (g.TryGetComponent(out GlassPanel glass)) glass.enabled = IsShowing;
        }

        private void BuildButtons()
        {
            float y = -Size.y * 0.5f - ButtonGap - ButtonHeight * 0.5f;
            const int count = 3;
            float pitch = ButtonWidth + ButtonGap;
            float x = -pitch * (count - 1) * 0.5f;
            var size = new Vector2(ButtonWidth, ButtonHeight);

            _retry = Button(transform, "Retry", new Vector2(x, y), size, 0f, 20f, true, out TextMeshProUGUI retry, OnRetry);
            retry.text = "Retry";
            x += pitch;
            _songs = Button(transform, "Songs", new Vector2(x, y), size, 0f, 20f, true, out TextMeshProUGUI songs, OnSongs);
            songs.text = "Songs";
            x += pitch;
            _moreInfo = Button(transform, "MoreInfo", new Vector2(x, y), size, 0f, 20f, true, out _moreInfoText, OnMoreInfo);
        }

        // ------------------------------------------------------------------
        // Details panel
        // ------------------------------------------------------------------

        private void BuildDetails()
        {
            var go = new GameObject("Details", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);       // turns about its left edge
            rt.sizeDelta = DetailsSize;
            rt.localPosition = new Vector3(0f, 0f, DetailsBehind);

            // Slightly darker than the card; sorted first so it is always
            // behind it while tucked.
            Color tint = tokens != null ? tokens.panel.tint : new Color(0.10f, 0.12f, 0.15f, 0.56f);
            tint = new Color(tint.r * 0.7f, tint.g * 0.7f, tint.b * 0.7f, Mathf.Clamp01(tint.a + 0.08f));
            var glass = go.AddComponent<GlassPanel>();
            glass.Init(glassMaterial, tokens, GlassStyle.Panel, Vector2.zero, CardDepth);
            glass.SetTint(tint);
            glass.SetSortingOrder(OrderDetails);

            float inner = DetailsSize.x - 30f, innerLeft = -inner * 0.5f;
            float top = DetailsSize.y * 0.5f;

            RectTransform content = Container(rt, "Content");
            // Container children measure from the rect's centre, wherever the pivot is.
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);

            Text(content, "DetailsTitle", 22f, _primary, TextAlignmentOptions.TopLeft, new Vector2(innerLeft, top - 15f), 150f, true)
                .text = "Details";
            _noteTotal = Text(content, "NoteTotal", 14f, _secondary, TextAlignmentOptions.TopRight,
                              new Vector2(innerLeft + inner - 120f, top - 21f), 120f);

            const float gap = 8f;
            float y = top - 47f;

            // notes
            const float notesH = 96f;
            CanvasGroup notes = Section(content, "Notes", new Vector2(0f, y - notesH * 0.5f), new Vector2(inner, notesH), out Transform n);
            BuildNotes(n, inner, notesH);
            y -= notesH + gap;

            // timing spread
            const float spreadH = 102f;
            CanvasGroup spread = Section(content, "Spread", new Vector2(0f, y - spreadH * 0.5f), new Vector2(inner, spreadH), out Transform sp);
            BuildSpread(sp, inner, spreadH);
            y -= spreadH + gap;

            // recent attempts: whatever height is left
            float bottom = -top + 12f;
            float attemptsH = y - bottom;
            CanvasGroup attempts = Section(content, "Attempts", new Vector2(0f, bottom + attemptsH * 0.5f),
                                           new Vector2(inner, attemptsH), out Transform at);
            BuildAttempts(at, inner, attemptsH);

            _details = go.AddComponent<DetailsPanelTransition>();
            var open = new Vector2(Size.x * 0.5f + DetailsGap, 0f);
            // Tucked: the right edge, at closed scale, lands behind the card's.
            var closed = new Vector2(Size.x * 0.5f - DetailsSize.x * 0.92f, 0f);
            _detailsSections = new[] { notes, spread, attempts };
            _detailsOpenPosition = open;
            _detailsClosedPosition = closed;
            rt.SetAsFirstSibling();
        }

        private CanvasGroup Section(Transform parent, string name, Vector2 centre, Vector2 size, out Transform inside)
        {
            GlassPanel glass = Glass(parent, name, GlassStyle.Row, centre, size, RowDepth, null);
            glass.SetSortingOrder(OrderDetailsRow);
            inside = glass.transform;
            return glass.gameObject.AddComponent<CanvasGroup>();
        }

        private void BuildNotes(Transform parent, float width, float height)
        {
            float left = -width * 0.5f + 13f, top = height * 0.5f;
            Text(parent, "NotesTitle", 13f, _secondary, TextAlignmentOptions.TopLeft, new Vector2(left, top - 7f), 120f)
                .text = "notes";

            string[] names = { "Perfect", "Great", "Good", "Miss" };
            Color[] colours =
            {
                PianoGlass.Song.OutcomeTint.Hit,
                new Color(1f, 1f, 1f, 0.9f), new Color(0.72f, 0.74f, 0.78f, 0.9f), EarlyOrange,
            };
            _noteBarLeft = left + 56f;
            _noteBarWidth = width - 56f - 13f - 13f - 28f;

            for (int i = 0; i < 4; i++)
            {
                float rowY = top - 33f - i * 17f;
                TextMeshProUGUI name = Text(parent, $"Name_{i}", 13f, _primary, TextAlignmentOptions.Left, new Vector2(left, rowY + 9f), 56f);
                name.text = names[i];
                if (i == 0) _firstNoteName = name;
                GlassPanel track = Glass(parent, $"Track_{i}", GlassStyle.Pill, new Vector2(_noteBarLeft + _noteBarWidth * 0.5f, rowY),
                                         new Vector2(_noteBarWidth, 5f), PillDepth, BarTrack);
                track.SetPlain(true);
                track.SetSortingOrder(OrderDetailsBar);
                _noteFills[i] = Glass(parent, $"Fill_{i}", GlassStyle.Pill, new Vector2(_noteBarLeft + 2.5f, rowY),
                                      new Vector2(5f, 5f), PillDepth * 0.7f, colours[i]);
                _noteFills[i].SetPlain(true);
                _noteFills[i].SetSortingOrder(OrderDetailsFill);
                _noteCounts[i] = Text(parent, $"Count_{i}", 13f, _primary, TextAlignmentOptions.Right,
                                      new Vector2(width * 0.5f - 13f - 28f, rowY + 9f), 28f, true);
            }
        }

        private void BuildSpread(Transform parent, float width, float height)
        {
            float left = -width * 0.5f + 13f, top = height * 0.5f, inner = width - 26f;
            Text(parent, "SpreadTitle", 13f, _secondary, TextAlignmentOptions.TopLeft, new Vector2(left, top - 7f), 110f)
                .text = "timing spread";
            _spreadAverage = Text(parent, "SpreadAverage", 13f, _primary, TextAlignmentOptions.TopRight,
                                  new Vector2(left + inner - 120f, top - 7f), 120f, true);

            float pitch = inner / TimingSpread.Bins;
            const float axisTop = -16f;
            for (int i = 0; i < TimingSpread.Bins; i++)
            {
                float cx = left + pitch * (i + 0.5f);
                Color c = i < TimingSpread.CentreBin ? EarlyOrange : i == TimingSpread.CentreBin ? CentreGrey : LateBlue;
                c.a = 0.95f;
                _histogramBars[i] = Glass(parent, $"Bin_{i}", GlassStyle.Row, new Vector2(cx, axisTop + 1f),
                                          new Vector2(pitch - 2.5f, 2f), PillDepth, c);
                _histogramBars[i].SetPlain(true);
                _histogramBars[i].SetSortingOrder(OrderDetailsBar);
            }

            // Bins -50, -20, 0, +20, +50 under their bars.
            (int bin, string text)[] ticks = { (0, "-50"), (3, "-20"), (5, "0"), (7, "+20"), (10, "+50") };
            foreach ((int bin, string label) in ticks)
            {
                float cx = left + pitch * (bin + 0.5f);
                Text(parent, $"Tick_{bin}", 10.5f, _secondary, TextAlignmentOptions.Top,
                     new Vector2(cx - 16f, axisTop - 2f), 32f).text = label;
            }

            float rowTop = axisTop - 16f;
            Text(parent, "Early", 10.5f, EarlyOrange, TextAlignmentOptions.TopLeft, new Vector2(left, rowTop), 60f).text = "early";
            Text(parent, "Ms", 10.5f, _secondary, TextAlignmentOptions.Top, new Vector2(left + inner * 0.5f - 30f, rowTop), 60f).text = "ms";
            Text(parent, "Late", 10.5f, LateBlue, TextAlignmentOptions.TopRight, new Vector2(left + inner - 60f, rowTop), 60f).text = "late";
        }

        private void BuildAttempts(Transform parent, float width, float height)
        {
            float left = -width * 0.5f + 13f, top = height * 0.5f, inner = width - 26f;
            Text(parent, "AttemptsTitle", 13f, _secondary, TextAlignmentOptions.TopLeft, new Vector2(left, top - 7f), 110f)
                .text = "recent attempts";
            _attemptsTrend = Text(parent, "Trend", 13f, _primary, TextAlignmentOptions.TopRight,
                                  new Vector2(left + inner - 130f, top - 7f), 130f, true);

            _attemptsMessage = Text(parent, "OnlyOne", 14f, _secondary, TextAlignmentOptions.Center,
                                    new Vector2(left, 8f), inner);
            _attemptsMessage.textWrappingMode = TextWrappingModes.Normal;
            _attemptsMessage.rectTransform.sizeDelta = new Vector2(inner, 40f);
            _attemptsMessage.text = "Play again to start tracking your progress";

            _chartRoot = Container(parent, "Chart");
            _chartWidth = inner;
            // Title 24 above; % label 14 over the tallest bar; label 15 under.
            _chartHeight = Mathf.Max(8f, height - 24f - 14f - 15f - 6f);
        }

        private void FillDetails(ResultsSummary s)
        {
            _noteTotal.text = s.TotalNotes == 1 ? "1 note" : $"{s.TotalNotes} notes";

            // Wait mode has one kind of hit, on the top row.
            _firstNoteName.text = s.IsWaitMode ? "Correct" : "Perfect";
            int[] counts = { s.IsWaitMode ? s.Correct : s.Perfect, s.Great, s.Good, s.Miss };
            for (int i = 0; i < 4; i++)
            {
                _noteCounts[i].text = counts[i].ToString();
                float f = s.TotalNotes > 0 ? (float)counts[i] / s.TotalNotes : 0f;
                float w = Mathf.Max(5f, _noteBarWidth * f);
                var rt = (RectTransform)_noteFills[i].transform;
                rt.sizeDelta = new Vector2(w, 5f);
                rt.anchoredPosition = new Vector2(_noteBarLeft + w * 0.5f, rt.anchoredPosition.y);
            }

            int max = 1;
            foreach (int c in s.Histogram) max = Mathf.Max(max, c);
            const float axisTop = -16f, tallest = 40f;
            for (int i = 0; i < TimingSpread.Bins; i++)
            {
                float h = Mathf.Max(1.5f, tallest * s.Histogram[i] / max);
                var rt = (RectTransform)_histogramBars[i].transform;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, axisTop + h * 0.5f);
            }

            _spreadAverage.text = s.AverageTimingText;
            _spreadAverage.color = s.TimingDirection > 0 ? LateBlue : s.TimingDirection < 0 ? EarlyOrange : _primary;

            FillChart(s);
        }

        private void FillChart(ResultsSummary s)
        {
            foreach (GameObject g in _chartSpawned) DestroySafely(g);
            _chartSpawned.Clear();

            _attemptsMessage.gameObject.SetActive(!s.HasChart);
            _attemptsTrend.gameObject.SetActive(s.HasChart);
            if (!s.HasChart) return;

            _attemptsTrend.text = s.TrendText;
            _attemptsTrend.color = Math.Round(s.TrendChange, MidpointRounding.AwayFromZero) > 0 ? UpGreen : _secondary;

            int n = s.Recent.Count;
            float pitch = _chartWidth / AttemptHistory.ChartAttempts;
            float start = -_chartWidth * 0.5f + (AttemptHistory.ChartAttempts - n) * pitch;
            float baseY = _chartBaseY;
            for (int i = 0; i < n; i++)
            {
                RecentAttempt a = s.Recent[i];
                float cx = start + pitch * (i + 0.5f);
                float h = Mathf.Max(3f, _chartHeight * (float)a.Height);
                Color c = a.IsCurrent ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.3f);
                GlassPanel bar = Glass(_chartRoot, $"Attempt_{a.Number}", GlassStyle.Row, new Vector2(cx, baseY + h * 0.5f),
                                       new Vector2(pitch - 10f, h), PillDepth, c);
                bar.SetPlain(true);
                bar.SetSortingOrder(OrderDetailsBar);
                bar.enabled = IsShowing;
                _chartSpawned.Add(bar.gameObject);

                TextMeshProUGUI pct = Text(_chartRoot, $"AttemptPct_{a.Number}", 11.5f, a.IsCurrent ? _primary : _secondary,
                                           TextAlignmentOptions.Bottom, new Vector2(cx - pitch * 0.5f, baseY + h + 17f), pitch, a.IsCurrent);
                pct.text = a.AccuracyText;
                _chartSpawned.Add(pct.gameObject);

                TextMeshProUGUI label = Text(_chartRoot, $"AttemptLabel_{a.Number}", 11f, a.IsCurrent ? _primary : _secondary,
                                             TextAlignmentOptions.Top, new Vector2(cx - pitch * 0.5f, baseY - 1f), pitch, a.IsCurrent);
                label.text = a.Label;
                _chartSpawned.Add(label.gameObject);
            }
        }

        /// <summary>The chart's baseline, from the section's centre: room for
        /// the attempt labels under the bars.</summary>
        private float _chartBaseY => -(_chartHeight + 24f + 14f + 15f + 6f) * 0.5f + 16f;

        // ------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------

        private static void DestroySafely(GameObject g)
        {
            if (Application.isPlaying) Destroy(g);
            else DestroyImmediate(g);
        }

        private static RectTransform Container(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>A glass surface centred at <paramref name="centre"/> (mm
        /// from its parent's centre), <paramref name="depthBehind"/> metres
        /// behind the text: nested glass sits nearer than its parent.</summary>
        private GlassPanel Glass(Transform parent, string name, GlassStyle style, Vector2 centre, Vector2 sizeMm,
                                 float depthBehind, Color? tint)
        {
            RectTransform rt = Container(parent, name);
            rt.sizeDelta = sizeMm;
            rt.anchoredPosition = centre;
            var glass = rt.gameObject.AddComponent<GlassPanel>();
            glass.Init(glassMaterial, tokens, style, Vector2.zero, depthBehind);
            glass.SetSortingOrder(depthBehind >= RowDepth ? OrderRow : depthBehind >= RingDepth ? OrderRing : OrderPill);
            if (tint.HasValue) glass.SetTint(tint);
            return glass;
        }

        private GlassButton Button(Transform parent, string name, Vector2 centre, Vector2 sizeMm, float depthBehind,
                                   float fontSize, bool bold, out TextMeshProUGUI label, Action onClick)
        {
            GlassPanel glass = Glass(parent, name, GlassStyle.Pill, centre, sizeMm,
                                     depthBehind > 0f ? depthBehind : RowDepth, ButtonFill);
            GameObject go = glass.gameObject;

            // Invisible, but what the editor's mouse and the Button respond to.
            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            hit.canvasRenderer.cullTransparentMesh = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = hit;
            button.onClick.AddListener(() => onClick());
            var glassButton = go.AddComponent<GlassButton>();
            glassButton.Init(ButtonFill);

            label = Text(go.transform, "Label", fontSize, _primary, TextAlignmentOptions.Center,
                         new Vector2(-sizeMm.x * 0.5f, fontSize * 0.7f), sizeMm.x, bold);
            return glassButton;
        }

        private TextMeshProUGUI Text(Transform parent, string name, float fontSize, Color colour, TextAlignmentOptions align,
                                     Vector2 topLeft, float width, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
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
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

#if UNITY_EDITOR
        // Play mode, with this component selected: fill the card with sample
        // data. PianoGlass/Preview/Results Card renders the same samples to
        // PNGs without entering play mode.
        [ContextMenu("Sample: 8 bars")]
        private void Sample8() => Show(ResultsSamples.Summary(ResultsSamples.EightBars, HasSongs));
        [ContextMenu("Sample: 32 bars")]
        private void Sample32() => Show(ResultsSamples.Summary(ResultsSamples.ThirtyTwoBars, HasSongs));
        [ContextMenu("Sample: 97 bars")]
        private void Sample97() => Show(ResultsSamples.Summary(ResultsSamples.NinetySevenBars, HasSongs));
        [ContextMenu("Sample: perfect, other hand next (rule d)")]
        private void SampleD() => Show(ResultsSamples.Summary(ResultsSamples.PerfectOtherHand, HasSongs));
        [ContextMenu("Sample: perfect, mastered (rule e)")]
        private void SampleE() => Show(ResultsSamples.Summary(ResultsSamples.PerfectMastered, HasSongs));

        /// <summary>For the song check: the same button a player presses.</summary>
        public void PressSongsForCheck() => _songs.Press();
#endif
    }
}
