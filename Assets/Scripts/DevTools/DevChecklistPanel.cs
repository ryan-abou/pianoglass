#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PianoGlass.Piano;
using PianoGlass.UI;

namespace PianoGlass.DevTools
{
    /// <summary>
    /// Development builds only: the post-build headset test checklist on
    /// Liquid Glass, so the tester reads the next step in the headset instead
    /// of memorising it. Compiled out of release builds.
    ///
    /// The list is a text file on the headset (format in HeadsetChecklist),
    /// pushed with adb and re-read whenever the panel opens or the file
    /// changes, so it never needs a rebuild:
    ///   adb push checklist.md /sdcard/Android/data/com.ryanabourjeili.pianoglass/files/checklist.md
    ///
    /// Each row has a Pass and a Fail button (2026-11-14, Ryan; it was one
    /// tick per row), pressed like any glass button: a poke, a pinch from
    /// afar, or the right controller ray + right trigger. Pressing the same
    /// one again clears it. Marks are saved in checklist_ticks.json, so they
    /// survive a relaunch, and every one is logged with "[Checklist]", which
    /// puts it on the logcat timeline.
    ///
    /// Built in code, never in the scene, borrowing the song picker's glass,
    /// tokens and font. Opens in front of the head, where it then stays put
    /// in the room; DevOverlayController shows and hides it.
    /// Canvas units are millimetres (scale 0.001).
    /// </summary>
    public sealed class DevChecklistPanel : MonoBehaviour
    {
        public const string FileName = "checklist.md";
        public const string TicksFileName = "checklist_ticks.json";

        // ---- Layout, mm ------------------------------------------------------
        private const float Width = 560f, Inner = 520f, Pad = 20f;
        /// <summary>Shortest row; rows grow with their wrapped text.</summary>
        private const float RowPlain = 50f, RowGap = 6f;
        /// <summary>The Pass and Fail pills at a row's right end.</summary>
        private const float MarkW = 72f, MarkH = 38f, MarkGap = 8f, MarkRight = 10f;
        private const float CountWidth = 200f;
        private const float PanelDepth = 0.002f, RowDepth = 0.0014f, DotDepth = 0.001f;
        private const int OrderPanel = -6, OrderRow = -5, OrderDot = -3;

        /// <summary>Where it opens, from the head: ahead, a little left and
        /// down, so it does not cover the middle of the keyboard.</summary>
        private const float Ahead = 0.55f, Leftward = 0.14f, Below = 0.10f;

        private static readonly Color RowFill = new Color(0.55f, 0.60f, 0.66f, 0.16f);
        private static readonly Color DotOff = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color DotPass = new Color(0.66f, 0.78f, 0.49f, 0.95f);
        private static readonly Color DotFail = new Color(0.93f, 0.45f, 0.42f, 0.95f);
        private static readonly Color PassOn = new Color(0.50f, 0.74f, 0.42f, 0.60f);
        private static readonly Color FailOn = new Color(0.90f, 0.40f, 0.38f, 0.60f);

        [Serializable]
        private class TickFile
        {
            /// <summary>Passed items (the name is from when there were only ticks).</summary>
            public List<string> keys = new List<string>();
            public List<string> failed = new List<string>();
        }

        private sealed class RowView
        {
            public GlassPanel Dot;
            public TextMeshProUGUI Text, Detail;
            public GlassButton Pass, Fail;
        }

        private TMP_FontAsset _font;
        private Material _textMaterial, _glassMaterial;
        private GlassTokens _tokens;
        private Color _primary, _secondary;

        private string _path, _ticksPath;
        private DateTime _loadedWrite;
        private float _nextFileCheck;
        private HeadsetChecklist _list;
        private TextMeshProUGUI _title, _count;
        private Transform _rowsRoot;
        private readonly List<RowView> _rows = new List<RowView>();

        public bool IsShown => gameObject.activeSelf;

        /// <summary>Built hidden. Null if the scene has no song picker to
        /// borrow the glass from.</summary>
        public static DevChecklistPanel Create(SongPicker styleSource)
        {
            if (styleSource == null) return null;
            var go = new GameObject(nameof(DevChecklistPanel), typeof(RectTransform));
            go.SetActive(false);
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.transform.localScale = Vector3.one * 0.001f;

            var panel = go.AddComponent<DevChecklistPanel>();
            panel.Build(styleSource.Style);
            // Pinch to move it (#5). Where it was put is saved on the keys like
            // every panel, so it opens there again; until then, in front of you.
            GrabbablePanel.Add(go, "Checklist", FindAnyObjectByType<KeyboardCalibration>());
            return panel;
        }

        /// <summary>Opens it; null <paramref name="head"/> leaves it where it
        /// was (back after recording mode).</summary>
        public void Show(Transform head)
        {
            // Active first: rows wrap, and their heights are measured as they are built.
            gameObject.SetActive(true);
            Reload();
            if (head != null) PlaceInFrontOf(head);
            Debug.Log($"[Checklist] shown: '{_list.Title}', {Summary()}");
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            Debug.Log("[Checklist] hidden");
        }

        private void Update()
        {
            // A new list pushed while the panel is open shows within a second.
            if (Time.unscaledTime < _nextFileCheck) return;
            _nextFileCheck = Time.unscaledTime + 1f;
            if (FileWriteTime() != _loadedWrite) Reload();
        }

        // ------------------------------------------------------------------
        // The list
        // ------------------------------------------------------------------

        private void Reload()
        {
            _loadedWrite = FileWriteTime();
            string text = null;
            try
            {
                if (File.Exists(_path)) text = File.ReadAllText(_path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Checklist] could not read {_path}: {e.Message}");
            }

            _list = HeadsetChecklist.Parse(text);
            TickFile saved = LoadTicks();
            _list.ApplyMarks(saved?.keys, saved?.failed);
            Debug.Log(text == null
                ? $"[Checklist] no checklist at {_path}"
                : $"[Checklist] loaded '{_list.Title}': {_list.Items.Count} items, {Summary()}" +
                  (_list.Overflow > 0 ? $", {_list.Overflow} more left out" : ""));
            Layout(text == null);
        }

        private void OnMark(int index, HeadsetChecklist.Mark button)
        {
            HeadsetChecklist.Mark now = _list.Press(index, button);
            SaveTicks();
            Paint();
            string what = now == HeadsetChecklist.Mark.Pass ? "PASS" : now == HeadsetChecklist.Mark.Fail ? "FAIL" : "cleared";
            Debug.Log($"[Checklist] {what} {index + 1}: {_list.Items[index].Text} ({Summary()})");
        }

        private string Summary() =>
            $"{_list.PassedCount} pass, {_list.FailedCount} fail, {_list.Items.Count - _list.MarkedCount} left";

        private DateTime FileWriteTime()
        {
            try { return File.Exists(_path) ? File.GetLastWriteTimeUtc(_path) : DateTime.MinValue; }
            catch (Exception) { return DateTime.MinValue; }
        }

        private TickFile LoadTicks()
        {
            try
            {
                if (!File.Exists(_ticksPath)) return null;
                return JsonUtility.FromJson<TickFile>(File.ReadAllText(_ticksPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Checklist] marks not read ({e.Message}); starting unmarked");
                return null;
            }
        }

        private void SaveTicks()
        {
            var file = new TickFile { keys = _list.Keys(HeadsetChecklist.Mark.Pass), failed = _list.Keys(HeadsetChecklist.Mark.Fail) };
            try { File.WriteAllText(_ticksPath, JsonUtility.ToJson(file)); }
            catch (Exception e) { Debug.LogWarning($"[Checklist] marks not saved: {e.Message}"); }
        }

        // ------------------------------------------------------------------
        // Building
        // ------------------------------------------------------------------

        private void Build((TMP_FontAsset font, Material text, Material glass, GlassTokens tokens) style)
        {
            (_font, _textMaterial, _glassMaterial, _tokens) = style;
            _primary = _tokens != null ? _tokens.textPrimary : GlassTokens.DefaultTextPrimary;
            _secondary = _tokens != null ? _tokens.textSecondary : GlassTokens.DefaultTextSecondary;
            _path = Path.Combine(Application.persistentDataPath, FileName);
            _ticksPath = Path.Combine(Application.persistentDataPath, TicksFileName);

            var glass = gameObject.AddComponent<GlassPanel>();
            glass.Init(_glassMaterial, _tokens, GlassStyle.Panel, new Vector2(0.012f, 0.012f), PanelDepth);
            glass.SetSortingOrder(OrderPanel);

            _title = Text(transform, "Title", 22f, _primary, TextAlignmentOptions.TopLeft, Inner - CountWidth, true);
            _count = Text(transform, "Count", 16f, _secondary, TextAlignmentOptions.TopRight, CountWidth);
            _rowsRoot = new GameObject("Rows", typeof(RectTransform)).transform;
            _rowsRoot.SetParent(transform, false);
        }

        /// <summary>Rows for the current list, and the panel sized to fit.</summary>
        private void Layout(bool missing)
        {
            foreach (Transform child in _rowsRoot) Destroy(child.gameObject);
            _rows.Clear();

            const float header = 34f, hint = 22f;
            bool footer = missing || _list.Items.Count == 0 || _list.Overflow > 0;

            // Rows go down from the Rows root's origin; each one wraps its text
            // onto as many lines as it needs and is as tall as that (2026-11-14:
            // one line with an ellipsis cut tasks off in the headset).
            TextMeshProUGUI hintText = Text(_rowsRoot, "Hint", 13f, _secondary, TextAlignmentOptions.TopLeft, Inner);
            Place(hintText, -Inner * 0.5f, hint);
            hintText.text = "Pass or Fail each one: poke, pinch from afar, or ray + trigger.   Left grip + X: hide";

            float y = 0f;
            for (int i = 0; i < _list.Items.Count; i++)
            {
                _rows.Add(BuildRow(i, _list.Items[i], y, out float h));
                y -= h + RowGap;
            }

            if (footer)
            {
                TextMeshProUGUI note = Text(_rowsRoot, "Footer", 14f, _secondary, TextAlignmentOptions.TopLeft, Inner, wrap: true);
                note.text = missing ? "No checklist on the headset yet: push checklist.md with adb."
                          : _list.Items.Count == 0 ? "The checklist has no items (lines starting with \"- \")."
                          : $"{_list.Overflow} more not shown: keep checklists to 5-8 items.";
                Place(note, -Inner * 0.5f, y - 4f);
                y -= 8f + Fit(note, Inner);
            }

            float height = Pad + header + hint - y + Pad;
            ((RectTransform)transform).sizeDelta = new Vector2(Width, height);
            float top = height * 0.5f - Pad;
            Place(_title, -Inner * 0.5f, top);
            Place(_count, Inner * 0.5f - CountWidth, top);
            _title.text = _list.Title;
            ((RectTransform)_rowsRoot).anchoredPosition = new Vector2(0f, top - header - hint);
            Paint();
        }

        /// <summary>A row whose top edge is at <paramref name="top"/> (Rows root
        /// units), sized to its wrapped text; its height comes back out.</summary>
        private RowView BuildRow(int index, HeadsetChecklist.Item item, float top, out float height)
        {
            GlassPanel row = Glass(_rowsRoot, $"Row_{index}", GlassStyle.Row, Vector2.zero, new Vector2(Inner, RowPlain),
                                   RowDepth, OrderRow);
            row.SetTint(RowFill);

            const float padTop = 8f, padBottom = 9f, gap = 3f;
            float textLeft = -Inner * 0.5f + 40f;
            float textWidth = Inner - 40f - 12f - (MarkW * 2f + MarkGap + MarkRight);
            var view = new RowView
            {
                Text = Text(row.transform, "Text", 17f, _primary, TextAlignmentOptions.TopLeft, textWidth, true, wrap: true),
            };
            view.Text.text = $"{index + 1}. {item.Text}";
            float textHeight = Fit(view.Text, textWidth);
            float detailHeight = 0f;
            if (item.Detail.Length > 0)
            {
                view.Detail = Text(row.transform, "Detail", 13f, _secondary, TextAlignmentOptions.TopLeft, textWidth, wrap: true);
                view.Detail.text = item.Detail;
                detailHeight = gap + Fit(view.Detail, textWidth);
            }

            height = Mathf.Max(RowPlain, padTop + textHeight + detailHeight + padBottom);
            var rt = (RectTransform)row.transform;
            rt.sizeDelta = new Vector2(Inner, height);
            rt.anchoredPosition = new Vector2(0f, top - height * 0.5f);
            Place(view.Text, textLeft, height * 0.5f - padTop);
            if (view.Detail != null) Place(view.Detail, textLeft, height * 0.5f - padTop - textHeight - gap);

            // On the first line of the text.
            view.Dot = Glass(row.transform, "Dot", GlassStyle.Pill, new Vector2(-Inner * 0.5f + 20f, height * 0.5f - padTop - 11f),
                             new Vector2(16f, 16f), DotDepth, OrderDot);

            // Pass and Fail at the right end, level with the middle of the row.
            float failX = Inner * 0.5f - MarkRight - MarkW * 0.5f;
            view.Fail = MarkButton(row.transform, $"Fail_{index}", "Fail", new Vector2(failX, 0f),
                                   () => OnMark(index, HeadsetChecklist.Mark.Fail));
            view.Pass = MarkButton(row.transform, $"Pass_{index}", "Pass", new Vector2(failX - MarkW - MarkGap, 0f),
                                   () => OnMark(index, HeadsetChecklist.Mark.Pass));
            return view;
        }

        private GlassButton MarkButton(Transform parent, string name, string label, Vector2 centre, Action onClick)
        {
            GlassPanel glass = Glass(parent, name, GlassStyle.Pill, centre, new Vector2(MarkW, MarkH), DotDepth, OrderDot);
            TextMeshProUGUI t = Text(glass.transform, "Label", 16f, _primary, TextAlignmentOptions.Center, MarkW, true);
            Place(t, -MarkW * 0.5f, 16f * 0.7f);
            t.text = label;
            var button = glass.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            // After it is placed: the button keeps its rest position from Init.
            var glassButton = glass.gameObject.AddComponent<GlassButton>();
            glassButton.Init(GlassBuilder.ButtonFill);
            return glassButton;
        }

        /// <summary>Wrapped height of a text at <paramref name="width"/>, and its rect set to match.</summary>
        private static float Fit(TextMeshProUGUI t, float width)
        {
            float h = Mathf.Ceil(t.GetPreferredValues(t.text, width, 0f).y);
            ((RectTransform)t.transform).sizeDelta = new Vector2(width, h);
            return h;
        }

        /// <summary>Marked rows: a green or red dot, that button lit, dimmer text.</summary>
        private void Paint()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                HeadsetChecklist.Mark m = _list.Items[i].Mark;
                RowView r = _rows[i];
                r.Dot.SetTint(m == HeadsetChecklist.Mark.Pass ? DotPass : m == HeadsetChecklist.Mark.Fail ? DotFail : DotOff);
                r.Text.color = m == HeadsetChecklist.Mark.None ? _primary : _secondary;
                r.Pass.SetFill(m == HeadsetChecklist.Mark.Pass ? PassOn : GlassBuilder.ButtonFill);
                r.Fail.SetFill(m == HeadsetChecklist.Mark.Fail ? FailOn : GlassBuilder.ButtonFill);
            }
            _count.text = _list.Items.Count == 0 ? ""
                        : $"{_list.PassedCount} pass  {_list.FailedCount} fail  {_list.Items.Count - _list.MarkedCount} left";
        }

        private void PlaceInFrontOf(Transform head)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 position = head.position + forward * Ahead - right * Leftward - Vector3.up * Below;

            // A canvas is read from behind its +Z, so +Z points away from the head.
            Vector3 away = Vector3.ProjectOnPlane(position - head.position, Vector3.up);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(away.normalized, Vector3.up));
        }

        private GlassPanel Glass(Transform parent, string name, GlassStyle style, Vector2 centre, Vector2 size,
                                 float depth, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = centre;
            var glass = go.AddComponent<GlassPanel>();
            glass.Init(_glassMaterial, _tokens, style, Vector2.zero, depth);
            glass.SetSortingOrder(order);
            return glass;
        }

        private TextMeshProUGUI Text(Transform parent, string name, float size, Color colour,
                                     TextAlignmentOptions align, float width, bool bold = false, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, size * 1.4f);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            if (_textMaterial != null) t.fontSharedMaterial = _textMaterial;
            t.fontSize = size;
            t.color = colour;
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        /// <summary>Top-left corner of a text, mm from the panel's centre.</summary>
        private static void Place(TextMeshProUGUI t, float left, float top) =>
            ((RectTransform)t.transform).anchoredPosition = new Vector2(left, top);
    }
}
#endif
