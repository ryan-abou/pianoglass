using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PianoGlass.Piano;
using PianoGlass.Song;
using PianoGlass.Transport;

namespace PianoGlass.UI
{
    /// <summary>
    /// The song list on Liquid Glass: a Left | Right switch, one row per song,
    /// and five rows to a page. Shows at launch once the keys are placed, and
    /// from the results card's Songs button. It hides while a song plays, and
    /// steps aside for calibration instructions the same way the results card
    /// does. SceneSetup makes the Canvas and its wiring; the rest is built
    /// here. Canvas units are millimetres (scale 0.001).
    ///
    /// Pressed with the controller ray (OvrUiPointer), or the mouse in the
    /// editor (EditorUiPointer).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class SongPicker : MonoBehaviour
    {
        [SerializeField] private CurrentSong current;
        [SerializeField] private SongTransport transport;
        [SerializeField] private KeyboardCalibration calibration;
        [SerializeField] private ControllerTipCalibration tip;
        [SerializeField] private GlassTokens tokens;
        [Tooltip("Assigned by SceneSetup, never looked up at runtime.")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Material textMaterial;
        [SerializeField] private Material glassMaterial;

        // ---- Layout, mm from the panel's centre ----------------------------
        private static readonly Vector2 Size = new Vector2(520f, 440f);
        private const float HeaderY = 182f, RowsTop = 130f, RowPitch = 58f, RowHeight = 52f, FooterY = -180f;
        private const float Inner = 480f, Left = -240f;
        private const float PillHeight = 40f;

        private const float PanelDepth = 0.002f, RowDepth = 0.0014f, PillDepth = 0.001f;
        private const int OrderPanel = -6, OrderRow = -5, OrderPill = -3;

        private static readonly Color ButtonFill = new Color(0.60f, 0.65f, 0.70f, 0.22f);
        private static readonly Color RowFill = new Color(0.55f, 0.60f, 0.66f, 0.16f);
        private static readonly Color CurrentFill = new Color(1f, 1f, 1f, 0.26f);
        private static readonly Color GreyedFill = new Color(0.40f, 0.42f, 0.45f, 0.10f);

        private sealed class RowView
        {
            public GlassButton Button;
            public TextMeshProUGUI Title, Detail;
        }

        private Canvas _canvas;
        private bool _built, _wantOpen = true, _shown;
        private Color _primary, _secondary;
        private readonly RowView[] _rows = new RowView[SongLibrary.RowsPerPage];
        private GlassButton _leftHand, _rightHand, _previous, _next;
        private TextMeshProUGUI _pageText;
        private PracticeHand _hand = PracticeHand.Right;
        private int _page;

        /// <summary>On show right now.</summary>
        public bool IsShowing => _shown;

        private void Awake()
        {
            if (current == null) current = FindAnyObjectByType<CurrentSong>();
            if (transport == null) transport = FindAnyObjectByType<SongTransport>();
            if (calibration == null) calibration = FindAnyObjectByType<KeyboardCalibration>();
            if (tip == null) tip = FindAnyObjectByType<ControllerTipCalibration>();
            if (font == null || glassMaterial == null)
                Debug.LogError("[Songs] picker font or glass material missing -- run PianoGlass/Scene/Configure Songs.");

            Build();
            SetVisible(false);
        }

        /// <summary>Show the list. It waits if calibration needs the player
        /// first. The results card can come up while the last note is still
        /// sounding, so this stops the clock, or the list would close again
        /// because the song counts as playing.</summary>
        public void Open()
        {
            _wantOpen = true;
            if (transport != null) transport.Pause();
        }

        /// <summary>Hide the list, keeping the current song.</summary>
        public void Close() => _wantOpen = false;

        private bool SongPlaying
        {
            get
            {
                if (transport == null) return false;
                SongTransport.State state = transport.CurrentState;
                return state == SongTransport.State.CountingIn || state == SongTransport.State.Playing;
            }
        }

        private bool CalibrationNeedsPlayer =>
            (calibration != null && calibration.NeedsPlayer) || (tip != null && tip.IsBusy);

        private void Update()
        {
            if (SongPlaying) _wantOpen = false;

            bool show = _wantOpen && !CalibrationNeedsPlayer;
            if (show == _shown) return;

            if (show) Refresh();
            SetVisible(show);
        }

        private void SetVisible(bool on)
        {
            _shown = on;
            if (_canvas != null) _canvas.enabled = on;
            // Glass quads are renderers of their own, not part of the Canvas.
            foreach (GlassPanel g in GetComponentsInChildren<GlassPanel>(true)) g.enabled = on;
        }

        /// <summary>Reads the Songs folder again and lands on the current song.</summary>
        private void Refresh()
        {
            if (current == null) return;
            current.RefreshLibrary();
            if (current.Hand != PracticeHand.All) _hand = current.Hand;

            SongRow currentRow = current.Row != null ? current.Library.Find(current.Row.Key) : null;
            _page = currentRow != null ? current.Library.PageOf(currentRow) : 0;
            Fill();
        }

        private void Fill()
        {
            SongLibrary library = current.Library;
            _page = Mathf.Clamp(_page, 0, library.PageCount - 1);

            for (int i = 0; i < _rows.Length; i++)
            {
                int index = _page * SongLibrary.RowsPerPage + i;
                RowView view = _rows[i];
                bool used = index < library.Rows.Count;
                view.Button.gameObject.SetActive(used);
                if (!used) continue;

                SongRow row = library.Rows[index];
                bool isCurrent = current.Row != null && current.Row.Key == row.Key;
                view.Title.text = row.Title;
                view.Title.color = row.CanLoad ? _primary : _secondary;
                view.Detail.text = row.Detail;
                view.Button.Button.interactable = row.CanLoad;
                view.Button.SetFill(!row.CanLoad ? GreyedFill : isCurrent ? CurrentFill : RowFill);
            }

            bool paged = library.PageCount > 1;
            _previous.gameObject.SetActive(paged);
            _next.gameObject.SetActive(paged);
            _pageText.gameObject.SetActive(paged);
            if (paged)
            {
                _pageText.text = $"{_page + 1} of {library.PageCount}";
                _previous.Button.interactable = _page > 0;
                _next.Button.interactable = _page < library.PageCount - 1;
            }

            FillSwitch();
        }

        private void FillSwitch()
        {
            NotePalette palette = current.Palette;
            _leftHand.SetFill(_hand == PracticeHand.Left ? Selected(palette.leftHand) : ButtonFill);
            _rightHand.SetFill(_hand == PracticeHand.Right ? Selected(palette.rightHand) : ButtonFill);
        }

        private static Color Selected(Color hand) => new Color(hand.r, hand.g, hand.b, 0.62f);

        private void OnHand(PracticeHand hand)
        {
            _hand = hand;
            FillSwitch();
        }

        private void OnRow(int slot)
        {
            int index = _page * SongLibrary.RowsPerPage + slot;
            if (index >= current.Library.Rows.Count) return;
            if (current.Pick(current.Library.Rows[index], _hand)) Close();
        }

        private void OnPage(int direction)
        {
            _page += direction;
            Fill();
        }

        // ------------------------------------------------------------------
        // Building
        // ------------------------------------------------------------------

        private void Build()
        {
            if (_built) return;
            _built = true;
            _canvas = GetComponent<Canvas>();
            _primary = tokens != null ? tokens.textPrimary : GlassTokens.DefaultTextPrimary;
            _secondary = tokens != null ? tokens.textSecondary : GlassTokens.DefaultTextSecondary;
            ((RectTransform)transform).sizeDelta = Size;
            if (TryGetComponent(out GlassPanel glass)) glass.SetSortingOrder(OrderPanel);

            Transform root = transform;
            Text(root, "Title", 26f, _primary, TextAlignmentOptions.TopLeft, new Vector2(Left, HeaderY + 17f), 200f, true)
                .text = "Songs";

            const float switchWidth = 92f;
            _leftHand = Pill(root, "Left", new Vector2(Inner * 0.5f - switchWidth * 1.5f - 6f, HeaderY),
                             new Vector2(switchWidth, PillHeight), "Left", () => OnHand(PracticeHand.Left));
            _rightHand = Pill(root, "Right", new Vector2(Inner * 0.5f - switchWidth * 0.5f, HeaderY),
                              new Vector2(switchWidth, PillHeight), "Right", () => OnHand(PracticeHand.Right));

            for (int i = 0; i < _rows.Length; i++) _rows[i] = BuildRow(root, i);

            const float pageWidth = 88f, closeWidth = 104f;
            _previous = Pill(root, "Previous", new Vector2(Left + pageWidth * 0.5f, FooterY),
                             new Vector2(pageWidth, PillHeight), "Prev", () => OnPage(-1));
            _next = Pill(root, "Next", new Vector2(Left + pageWidth * 1.5f + 96f, FooterY),
                         new Vector2(pageWidth, PillHeight), "Next", () => OnPage(+1));
            _pageText = Text(root, "Page", 15f, _secondary, TextAlignmentOptions.Center,
                             new Vector2(Left + pageWidth, FooterY + 11f), 96f);
            Pill(root, "Close", new Vector2(Inner * 0.5f - closeWidth * 0.5f, FooterY),
                 new Vector2(closeWidth, PillHeight), "Close", Close);
        }

        private RowView BuildRow(Transform root, int slot)
        {
            float y = RowsTop - slot * RowPitch;
            GlassPanel glass = Glass(root, $"Row_{slot}", GlassStyle.Row, new Vector2(0f, y), new Vector2(Inner, RowHeight), RowDepth);
            GlassButton button = MakeButton(glass, RowFill, () => OnRow(slot));

            var view = new RowView { Button = button };
            view.Title = Text(glass.transform, "Title", 20f, _primary, TextAlignmentOptions.TopLeft,
                              new Vector2(-Inner * 0.5f + 16f, RowHeight * 0.5f - 6f), Inner - 32f, true);
            view.Title.overflowMode = TextOverflowModes.Ellipsis;
            view.Detail = Text(glass.transform, "Detail", 14f, _secondary, TextAlignmentOptions.TopLeft,
                               new Vector2(-Inner * 0.5f + 16f, RowHeight * 0.5f - 31f), Inner - 32f);
            view.Detail.overflowMode = TextOverflowModes.Ellipsis;
            return view;
        }

        private GlassButton Pill(Transform parent, string name, Vector2 centre, Vector2 size, string label, Action onClick)
        {
            GlassPanel glass = Glass(parent, name, GlassStyle.Pill, centre, size, PillDepth);
            glass.SetSortingOrder(OrderPill);
            Text(glass.transform, "Label", 18f, _primary, TextAlignmentOptions.Center,
                 new Vector2(-size.x * 0.5f, 12.6f), size.x, true).text = label;
            return MakeButton(glass, ButtonFill, onClick);
        }

        private static GlassButton MakeButton(GlassPanel glass, Color fill, Action onClick)
        {
            var button = glass.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            var glassButton = glass.gameObject.AddComponent<GlassButton>();
            glassButton.Init(fill);
            return glassButton;
        }

        private GlassPanel Glass(Transform parent, string name, GlassStyle style, Vector2 centre, Vector2 size, float depth)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = centre;
            var glass = go.AddComponent<GlassPanel>();
            glass.Init(glassMaterial, tokens, style, Vector2.zero, depth);
            glass.SetSortingOrder(OrderRow);
            return glass;
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
        // For the song check: the same buttons a player presses.

        public IReadOnlyList<GlassButton> RowButtonsForCheck()
        {
            var list = new List<GlassButton>();
            foreach (RowView row in _rows) list.Add(row.Button);
            return list;
        }

        public void PressHandForCheck(PracticeHand hand) => (hand == PracticeHand.Left ? _leftHand : _rightHand).Press();

        /// <summary>Goes to the song's page and presses its row. False if
        /// there is no such song or it can't be pressed.</summary>
        public bool PressSongForCheck(string titlePart)
        {
            IReadOnlyList<SongRow> rows = current.Library.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Title.IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) < 0) continue;
                _page = i / SongLibrary.RowsPerPage;
                Fill();
                GlassButton button = _rows[i % SongLibrary.RowsPerPage].Button;
                if (!button.Button.interactable) return false;
                button.Press();
                return true;
            }
            return false;
        }
#endif
    }
}
