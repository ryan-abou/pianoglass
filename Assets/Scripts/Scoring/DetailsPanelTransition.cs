using System.Collections;
using UnityEngine;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// Opens and closes the results details panel. Animates only the
    /// RectTransform's position, scale and rotation and CanvasGroup alpha,
    /// with no layout groups and no blur, so it stays smooth on a Quest 2.
    /// A press mid-way starts from the current values rather than snapping.
    ///
    /// The panel's left edge is its pivot. Glass follows the CanvasGroups
    /// because GlassPanel reads them: a quad is not a Canvas graphic.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class DetailsPanelTransition : MonoBehaviour
    {
        [SerializeField] private AnimationCurve ease = new AnimationCurve(
            new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        [SerializeField] private float openSeconds = 0.28f;
        [SerializeField] private float closeSeconds = 0.18f;
        [Tooltip("Each section starts fading in this long after the previous one when opening.")]
        [SerializeField] private float sectionStagger = 0.05f;
        [SerializeField] private float closedScale = 0.92f;
        [Tooltip("Degrees about Y when open: the far edge comes toward the player.")]
        [SerializeField] private float openYaw = 10f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private CanvasGroup[] _sections = new CanvasGroup[0];
        private CanvasGroup _follower;
        private Vector2 _openPosition, _closedPosition;
        private Coroutine _running;

        public bool IsOpen { get; private set; }

        /// <param name="openPosition">anchoredPosition when open.</param>
        /// <param name="closedPosition">anchoredPosition when tucked behind the card.</param>
        /// <param name="sections">Fade in one after another when opening.</param>
        /// <param name="follower">Fades with the panel: the card's per-pill % labels.</param>
        public void Configure(Vector2 openPosition, Vector2 closedPosition, CanvasGroup[] sections, CanvasGroup follower)
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _openPosition = openPosition;
            _closedPosition = closedPosition;
            _sections = sections ?? new CanvasGroup[0];
            _follower = follower;
            SetInstant(false);
        }

        public void Toggle() => Set(!IsOpen);

        public void Set(bool open)
        {
            if (_rect == null) return;
            IsOpen = open;
            ApplyInteractable(open);
            if (_running != null) StopCoroutine(_running);
            _running = isActiveAndEnabled ? StartCoroutine(Run(open)) : null;
            if (_running == null) SetInstant(open);
        }

        /// <summary>No animation: hiding the card, and the layout previews.</summary>
        public void SetInstant(bool open)
        {
            if (_rect == null) return;
            if (_running != null) StopCoroutine(_running);
            _running = null;
            IsOpen = open;
            ApplyInteractable(open);
            ApplyPose(open ? _openPosition : _closedPosition, open ? 1f : closedScale, open ? openYaw : 0f);
            float alpha = open ? 1f : 0f;
            _group.alpha = alpha;
            if (_follower != null) _follower.alpha = alpha;
            foreach (CanvasGroup s in _sections) s.alpha = alpha;
        }

        private IEnumerator Run(bool open)
        {
            float duration = Mathf.Max(0.001f, open ? openSeconds : closeSeconds);
            Vector2 fromPosition = _rect.anchoredPosition;
            float fromScale = _rect.localScale.x;
            float fromYaw = Mathf.DeltaAngle(0f, _rect.localEulerAngles.y);
            float fromAlpha = _group.alpha;
            float fromFollower = _follower != null ? _follower.alpha : 0f;

            var fromSections = new float[_sections.Length];
            for (int i = 0; i < _sections.Length; i++) fromSections[i] = _sections[i].alpha;
            // The last section still has to finish with the panel.
            float sectionSeconds = Mathf.Max(0.01f, duration - sectionStagger * (_sections.Length - 1));

            Vector2 toPosition = open ? _openPosition : _closedPosition;
            float toScale = open ? 1f : closedScale;
            float toYaw = open ? openYaw : 0f;
            float toAlpha = open ? 1f : 0f;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = ease.Evaluate(t / duration);
                ApplyPose(Vector2.LerpUnclamped(fromPosition, toPosition, k),
                          Mathf.LerpUnclamped(fromScale, toScale, k),
                          Mathf.LerpUnclamped(fromYaw, toYaw, k));
                _group.alpha = Mathf.Lerp(fromAlpha, toAlpha, k);
                if (_follower != null) _follower.alpha = Mathf.Lerp(fromFollower, toAlpha, k);

                // Closing: the panel's alpha takes everything out together.
                if (open)
                {
                    for (int i = 0; i < _sections.Length; i++)
                    {
                        float st = Mathf.Clamp01((t - sectionStagger * i) / sectionSeconds);
                        _sections[i].alpha = Mathf.Lerp(fromSections[i], 1f, ease.Evaluate(st));
                    }
                }
                yield return null;
            }

            _running = null;
            SetInstant(open);
        }

        private void ApplyPose(Vector2 position, float scale, float yaw)
        {
            _rect.anchoredPosition = position;
            _rect.localScale = new Vector3(scale, scale, 1f);
            _rect.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void ApplyInteractable(bool open)
        {
            _group.blocksRaycasts = open;
            _group.interactable = open;
        }

        [ContextMenu("Open (test)")]
        private void TestOpen() => Set(true);

        [ContextMenu("Close (test)")]
        private void TestClose() => Set(false);
    }
}
