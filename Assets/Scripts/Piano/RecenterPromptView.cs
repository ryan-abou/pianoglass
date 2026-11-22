using TMPro;
using UnityEngine;
using PianoGlass.UI;

namespace PianoGlass.Piano
{
    /// <summary>
    /// The "hold the Meta button" banner for #17: a big, see-through Liquid
    /// Glass pill in the middle of the player's view, gliding after the head
    /// wherever they look, until the recenter that lines the keys up.
    ///
    /// Not on the calibration panel: that panel follows the keyboard, and
    /// before the recenter the keyboard is the thing that is a piano width off,
    /// so the panel was too (2026-11-14). Built in code and shown by
    /// LaunchResync; glass, tokens and font are the song picker's, so nothing
    /// is looked up. Canvas units are millimetres (scale 0.001).
    /// </summary>
    public sealed class RecenterPromptView : MonoBehaviour
    {
        /// <summary>Metres in front of the eyes: inside arm's reach would be
        /// tiring to read, and nearer than the keys would cover them.</summary>
        private const float Distance = 0.9f;
        /// <summary>A little below the line of sight, where reading is easiest.</summary>
        private const float Below = 0.06f;
        /// <summary>How fast it catches up with the gaze, per second: quick
        /// enough to stay in view, slow enough not to feel glued to the face.</summary>
        private const float Follow = 5f;

        private static readonly Vector2 Size = new Vector2(700f, 170f); // three short lines (#42)

        private Transform _head;
        private bool _placed;
        private TextMeshProUGUI _label;

        /// <summary>Built hidden. Null if the scene has no song picker.</summary>
        /// <param name="size">Canvas millimetres; square makes a circle (the
        /// resume countdown, round 13). The banner's size if left out.</param>
        public static RecenterPromptView Create(SongPicker styleSource, string text, Vector2? size = null)
        {
            if (styleSource == null) return null;
            (TMP_FontAsset font, Material textMaterial, Material glassMaterial, GlassTokens tokens) = styleSource.Style;

            var go = new GameObject(nameof(RecenterPromptView), typeof(RectTransform));
            go.SetActive(false);
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50; // over the other glass when they overlap
            go.transform.localScale = Vector3.one * 0.001f;
            ((RectTransform)go.transform).sizeDelta = size ?? Size;

            var glass = go.AddComponent<GlassPanel>();
            glass.Init(glassMaterial, tokens, GlassStyle.Pill, new Vector2(0.01f, 0.01f), 0.002f);

            var label = new GameObject("Text", typeof(RectTransform));
            var rt = (RectTransform)label.transform;
            rt.SetParent(go.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = label.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (textMaterial != null) t.fontSharedMaterial = textMaterial;
            t.text = text;
            t.fontSize = 30f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            Color c = tokens != null ? tokens.textPrimary : GlassTokens.DefaultTextPrimary;
            c.a = 0.92f;
            t.color = c;
            t.raycastTarget = false;

            var view = go.AddComponent<RecenterPromptView>();
            view._label = t;
            return view;
        }

        public void Show()
        {
            _placed = false;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public bool Shown => gameObject.activeSelf;

        /// <summary>New words, and their size (the resume countdown's big number, #44).</summary>
        public void SetText(string text, float fontSize)
        {
            if (_label == null) return;
            _label.text = text;
            _label.fontSize = fontSize;
        }

        private void LateUpdate()
        {
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (_head == null) return;

            Vector3 target = _head.position + _head.forward * Distance - Vector3.up * Below;
            if (!_placed)
            {
                transform.position = target;
                _placed = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-Follow * Time.unscaledDeltaTime);
                transform.position = Vector3.Lerp(transform.position, target, k);
            }

            // Upright and facing the eyes; a canvas is read from behind its +Z.
            Vector3 away = transform.position - _head.position;
            if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }
    }
}
