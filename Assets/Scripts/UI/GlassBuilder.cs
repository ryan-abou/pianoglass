using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PianoGlass.UI
{
    /// <summary>
    /// Builds Liquid Glass panels in code that are not in the scene (song
    /// panel, settings), from the song picker's glass, tokens and font, so
    /// nothing is looked up and everything matches the picker. Canvas units are
    /// millimetres (scale 0.001); positions are from the panel's centre.
    /// </summary>
    public sealed class GlassBuilder
    {
        public const float PanelDepth = 0.002f, RowDepth = 0.0014f, PillDepth = 0.001f;
        public const int OrderPanel = -6, OrderRow = -5, OrderPill = -3;

        public static readonly Color ButtonFill = new Color(0.60f, 0.65f, 0.70f, 0.22f);
        public static readonly Color GreyedFill = new Color(0.40f, 0.42f, 0.45f, 0.10f);
        public static readonly Color OnFill = new Color(0.40f, 0.68f, 0.95f, 0.55f);

        public readonly TMP_FontAsset Font;
        public readonly Material TextMaterial, GlassMaterial;
        public readonly GlassTokens Tokens;
        public readonly Color Primary, Secondary;

        public GlassBuilder(SongPicker style)
        {
            (Font, TextMaterial, GlassMaterial, Tokens) = style.Style;
            Primary = Tokens != null ? Tokens.textPrimary : GlassTokens.DefaultTextPrimary;
            Secondary = Tokens != null ? Tokens.textSecondary : GlassTokens.DefaultTextSecondary;
        }

        /// <summary>A world-space canvas with panel glass, built inactive.</summary>
        public RectTransform Canvas(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.transform.localScale = Vector3.one * 0.001f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            var glass = go.AddComponent<GlassPanel>();
            glass.Init(GlassMaterial, Tokens, GlassStyle.Panel, new Vector2(0.012f, 0.012f), PanelDepth);
            glass.SetSortingOrder(OrderPanel);
            return rt;
        }

        public GlassPanel Glass(Transform parent, string name, GlassStyle style, Vector2 centre, Vector2 size,
                                float depth, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = centre;
            var glass = go.AddComponent<GlassPanel>();
            glass.Init(GlassMaterial, Tokens, style, Vector2.zero, depth);
            glass.SetSortingOrder(order);
            return glass;
        }

        /// <summary>A pill button with a centred label; at least 3 cm tall,
        /// so a fingertip can hit it.</summary>
        public GlassButton Pill(Transform parent, string name, Vector2 centre, Vector2 size, string label,
                                Action onClick, out TextMeshProUGUI labelText, float fontSize = 17f)
        {
            GlassPanel glass = Glass(parent, name, GlassStyle.Pill, centre, size, PillDepth, OrderPill);
            labelText = Text(glass.transform, "Label", fontSize, Primary, TextAlignmentOptions.Center,
                             new Vector2(-size.x * 0.5f, fontSize * 0.7f), size.x, true);
            labelText.text = label;
            var button = glass.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            var glassButton = glass.gameObject.AddComponent<GlassButton>();
            glassButton.Init(ButtonFill);
            return glassButton;
        }

        public TextMeshProUGUI Text(Transform parent, string name, float fontSize, Color colour,
                                    TextAlignmentOptions align, Vector2 topLeft, float width, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, fontSize * 1.4f);
            rt.anchoredPosition = topLeft;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            if (TextMaterial != null) t.fontSharedMaterial = TextMaterial;
            t.fontSize = fontSize;
            t.color = colour;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        /// <summary>Usable or greyed, and lit when on.</summary>
        public static void Set(GlassButton button, bool usable, bool on = false)
        {
            button.Button.interactable = usable;
            button.SetFill(!usable ? GreyedFill : on ? OnFill : ButtonFill);
        }
    }
}
