#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
    /// Each row is a GlassButton: the right controller ray + right trigger
    /// ticks it, exactly like the results card's buttons. Ticks are saved in
    /// checklist_ticks.json, so they survive a relaunch, and every tick is
    /// logged with "[Checklist]", which puts it on the logcat timeline.
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
        private const float Width = 460f, Inner = 420f, Pad = 20f;
        private const float RowPlain = 36f, RowWithDetail = 52f, RowGap = 6f;
        private const float PanelDepth = 0.002f, RowDepth = 0.0014f, DotDepth = 0.001f;
        private const int OrderPanel = -6, OrderRow = -5, OrderDot = -3;

        /// <summary>Where it opens, from the head: ahead, a little left and
        /// down, so it does not cover the middle of the keyboard.</summary>
        private const float Ahead = 0.55f, Leftward = 0.14f, Below = 0.10f;

        private static readonly Color RowFill = new Color(0.55f, 0.60f, 0.66f, 0.16f);
        private static readonly Color DotOff = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color DotOn = new Color(0.66f, 0.78f, 0.49f, 0.95f);

        [Serializable]
        private class TickFile
        {
            public List<string> keys = new List<string>();
        }

        private sealed class RowView
        {
            public GlassPanel Dot;
            public TextMeshProUGUI Text, Detail;
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
            return panel;
        }

        public void Show(Transform head)
        {
            Reload();
            if (head != null) PlaceInFrontOf(head);
            gameObject.SetActive(true);
            Debug.Log($"[Checklist] shown: '{_list.Title}', {_list.TickedCount}/{_list.Items.Count} ticked");
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
            _list.ApplyTicks(LoadTicks());
            Debug.Log(text == null
                ? $"[Checklist] no checklist at {_path}"
                : $"[Checklist] loaded '{_list.Title}': {_list.Items.Count} items, {_list.TickedCount} ticked" +
                  (_list.Overflow > 0 ? $", {_list.Overflow} more left out" : ""));
            Layout(text == null);
        }

        private void OnRow(int index)
        {
            bool on = _list.Toggle(index);
            SaveTicks();
            Paint();
            Debug.Log($"[Checklist] {(on ? "ticked" : "unticked")} {index + 1}: {_list.Items[index].Text} " +
                      $"({_list.TickedCount}/{_list.Items.Count} done)");
        }

        private DateTime FileWriteTime()
        {
            try { return File.Exists(_path) ? File.GetLastWriteTimeUtc(_path) : DateTime.MinValue; }
            catch (Exception) { return DateTime.MinValue; }
        }

        private List<string> LoadTicks()
        {
            try
            {
                if (!File.Exists(_ticksPath)) return null;
                return JsonUtility.FromJson<TickFile>(File.ReadAllText(_ticksPath))?.keys;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Checklist] ticks not read ({e.Message}); starting unticked");
                return null;
            }
        }

        private void SaveTicks()
        {
            try { File.WriteAllText(_ticksPath, JsonUtility.ToJson(new TickFile { keys = _list.TickedKeys() })); }
            catch (Exception e) { Debug.LogWarning($"[Checklist] ticks not saved: {e.Message}"); }
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

            _title = Text(transform, "Title", 22f, _primary, TextAlignmentOptions.TopLeft, Inner - 80f, true);
            _count = Text(transform, "Count", 18f, _secondary, TextAlignmentOptions.TopRight, 80f);
            _rowsRoot = new GameObject("Rows", typeof(RectTransform)).transform;
            _rowsRoot.SetParent(transform, false);
        }

        /// <summary>Rows for the current list, and the panel sized to fit.</summary>
        private void Layout(bool missing)
        {
            foreach (Transform child in _rowsRoot) Destroy(child.gameObject);
            _rows.Clear();

            const float header = 34f, hint = 22f;
            float rows = 0f;
            foreach (HeadsetChecklist.Item item in _list.Items)
                rows += (item.Detail.Length > 0 ? RowWithDetail : RowPlain) + RowGap;
            bool footer = missing || _list.Items.Count == 0 || _list.Overflow > 0;
            float height = Pad + header + hint + rows + (footer ? 26f : 0f) + Pad;
            ((RectTransform)transform).sizeDelta = new Vector2(Width, height);

            float top = height * 0.5f - Pad;
            Place(_title, -Inner * 0.5f, top);
            Place(_count, Inner * 0.5f - 80f, top);
            _title.text = _list.Title;

            TextMeshProUGUI hintText = Text(_rowsRoot, "Hint", 13f, _secondary, TextAlignmentOptions.TopLeft, Inner);
            Place(hintText, -Inner * 0.5f, top - header);
            hintText.text = "Ray + right trigger: tick     Left grip + X: hide";

            float y = top - header - hint;
            for (int i = 0; i < _list.Items.Count; i++)
            {
                HeadsetChecklist.Item item = _list.Items[i];
                float h = item.Detail.Length > 0 ? RowWithDetail : RowPlain;
                _rows.Add(BuildRow(i, item, y - h * 0.5f, h));
                y -= h + RowGap;
            }

            if (footer)
            {
                TextMeshProUGUI note = Text(_rowsRoot, "Footer", 14f, _secondary, TextAlignmentOptions.TopLeft, Inner);
                Place(note, -Inner * 0.5f, y - 4f);
                note.text = missing ? "No checklist on the headset yet: push checklist.md with adb."
                          : _list.Items.Count == 0 ? "The checklist has no items (lines starting with \"- \")."
                          : $"{_list.Overflow} more not shown: keep checklists to 5-8 items.";
            }
            Paint();
        }

        private RowView BuildRow(int index, HeadsetChecklist.Item item, float centreY, float height)
        {
            GlassPanel row = Glass(_rowsRoot, $"Row_{index}", GlassStyle.Row, new Vector2(0f, centreY),
                                   new Vector2(Inner, height), RowDepth, OrderRow);
            var button = row.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => OnRow(index));
            row.gameObject.AddComponent<GlassButton>().Init(RowFill);

            var view = new RowView
            {
                Dot = Glass(row.transform, "Dot", GlassStyle.Pill, new Vector2(-Inner * 0.5f + 20f, height * 0.5f - 18f),
                            new Vector2(16f, 16f), DotDepth, OrderDot),
            };
            float textLeft = -Inner * 0.5f + 40f, textWidth = Inner - 52f;
            view.Text = Text(row.transform, "Text", 17f, _primary, TextAlignmentOptions.TopLeft, textWidth, true);
            Place(view.Text, textLeft, height * 0.5f - 7f);
            view.Text.text = $"{index + 1}. {item.Text}";
            if (item.Detail.Length > 0)
            {
                view.Detail = Text(row.transform, "Detail", 13f, _secondary, TextAlignmentOptions.TopLeft, textWidth);
                Place(view.Detail, textLeft, height * 0.5f - 29f);
                view.Detail.text = item.Detail;
            }
            return view;
        }

        /// <summary>Ticked rows: a green dot and dimmer text.</summary>
        private void Paint()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                bool on = _list.Items[i].Ticked;
                _rows[i].Dot.SetTint(on ? DotOn : DotOff);
                _rows[i].Text.color = on ? _secondary : _primary;
            }
            _count.text = _list.Items.Count > 0 ? $"{_list.TickedCount}/{_list.Items.Count}" : "";
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
                                     TextAlignmentOptions align, float width, bool bold = false)
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
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
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
