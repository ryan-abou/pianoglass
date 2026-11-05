using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PianoGlass.UI
{
    /// <summary>
    /// A pill button on Liquid Glass: a real uGUI Button over a GlassPanel.
    /// Hover brightens it and a press pushes it back, whichever input drives
    /// it: OvrUiPointer on the headset (Phase 4 replaces that with a hand
    /// poke) or the mouse in the editor's Game view.
    /// </summary>
    [RequireComponent(typeof(GlassPanel), typeof(Button))]
    public class GlassButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private const float HoverLift = 0.10f;
        /// <summary>How long a press stays pushed in, seconds.</summary>
        private const float PressSeconds = 0.12f;
        /// <summary>How far a press pushes the button back, canvas units (mm).</summary>
        private const float PressDepth = 1.5f;

        private static readonly List<GlassButton> Active = new List<GlassButton>();

        /// <summary>Every enabled button, for the pointer to test.</summary>
        public static IReadOnlyList<GlassButton> All => Active;

        private GlassPanel _glass;
        private Button _button;
        private Canvas _canvas;
        private CanvasGroup[] _groups;
        private Color _fill;
        private bool _hovered;
        private float _pressedUntil = -1f;
        private Vector3 _restPosition;

        public Button Button => _button;

        /// <summary>For buttons built in code: straight after AddComponent.</summary>
        public void Init(Color fill)
        {
            _glass = GetComponent<GlassPanel>();
            _button = GetComponent<Button>();
            _button.transition = Selectable.Transition.None;
            _canvas = GetComponentInParent<Canvas>(true);
            _groups = GetComponentsInParent<CanvasGroup>(true);
            _restPosition = transform.localPosition;
            SetFill(fill);
        }

        public void SetFill(Color fill)
        {
            _fill = fill;
            Apply();
        }

        public void SetHovered(bool hovered)
        {
            if (_hovered == hovered) return;
            _hovered = hovered;
            Apply();
        }

        /// <summary>Whether a pointer can press it right now: built, shown,
        /// and not inside a closed panel.</summary>
        public bool IsPressable
        {
            get
            {
                if (_button == null || !isActiveAndEnabled || !_button.IsInteractable()) return false;
                if (_canvas == null || !_canvas.enabled) return false;
                foreach (CanvasGroup g in _groups)
                    if (g != null && (!g.blocksRaycasts || g.alpha <= 0f)) return false;
                return true;
            }
        }

        /// <summary>Where a ray crosses this button's rect, or false.</summary>
        public bool TryHit(Ray ray, out float distance)
        {
            distance = 0f;
            var rect = (RectTransform)transform;
            var plane = new Plane(rect.forward, rect.position);
            if (!plane.Raycast(ray, out distance) || distance <= 0f) return false;
            Vector3 local = rect.InverseTransformPoint(ray.GetPoint(distance));
            return rect.rect.Contains(new Vector2(local.x, local.y));
        }

        /// <summary>The pointer pressed it.</summary>
        public void Press()
        {
            ShowPress();
            _button.onClick.Invoke();
        }

        private void ShowPress()
        {
            _pressedUntil = Time.unscaledTime + PressSeconds;
            transform.localPosition = _restPosition + new Vector3(0f, 0f, PressDepth);
        }

        private void Update()
        {
            if (_pressedUntil < 0f || Time.unscaledTime < _pressedUntil) return;
            _pressedUntil = -1f;
            transform.localPosition = _restPosition;
        }

        private void Apply()
        {
            if (_glass == null) return;
            Color c = _fill;
            if (_hovered) c = new Color(Mathf.Lerp(c.r, 1f, 0.15f), Mathf.Lerp(c.g, 1f, 0.15f),
                                        Mathf.Lerp(c.b, 1f, 0.15f), Mathf.Clamp01(c.a + HoverLift));
            _glass.SetTint(c);
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable()
        {
            Active.Remove(this);
            _hovered = false;
            Apply();
            if (_pressedUntil >= 0f)
            {
                _pressedUntil = -1f;
                transform.localPosition = _restPosition;
            }
        }

        // Editor mouse, through the EventSystem. The Button itself raises onClick.
        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);
        public void OnPointerClick(PointerEventData eventData) => ShowPress();
    }
}
