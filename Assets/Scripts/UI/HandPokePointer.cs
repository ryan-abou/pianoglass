using System.Collections.Generic;
using UnityEngine;
using PianoGlass.Piano;

namespace PianoGlass.UI
{
    /// <summary>
    /// Presses GlassButtons with a tracked index fingertip (#5), so every
    /// button works without controllers. For each tracked hand, the nearest
    /// button the fingertip is over goes through a PokeDetector: hover when
    /// close in front, one press as the fingertip pushes through the face.
    /// The controller ray (OvrUiPointer) keeps working alongside.
    ///
    /// Canvas units are millimetres (scale 0.001), so a button's local z is
    /// mm too; a canvas is read from behind its +Z, so "in front" is -z.
    /// Adds itself after the scene loads; no scene change.
    /// </summary>
    public sealed class HandPokePointer : MonoBehaviour
    {
        /// <summary>Outline margin, mm: a fingertip is wider than the bone point.</summary>
        private const float EdgeMm = 6f;

        private readonly Dictionary<GlassButton, PokeDetector>[] _detectors =
            { new Dictionary<GlassButton, PokeDetector>(), new Dictionary<GlassButton, PokeDetector>() };
        private readonly GlassButton[] _hovered = new GlassButton[2];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add()
        {
            if (Application.isEditor) return;
            var go = new GameObject(nameof(HandPokePointer));
            DontDestroyOnLoad(go);
            go.AddComponent<HandPokePointer>();
        }

        private void LateUpdate()
        {
            TrackedHands hands = TrackedHands.Instance;
            for (int h = 0; h < 2; h++)
            {
                if (hands == null || !hands.TryIndexTip(h, out Vector3 tip))
                {
                    Forget(h);
                    continue;
                }
                Poke(h, tip);
            }
        }

        private void Poke(int h, Vector3 tip)
        {
            // The nearest button whose outline the fingertip is over and that it is near.
            GlassButton best = null;
            float bestDepth = 0f;
            foreach (GlassButton b in GlassButton.All)
            {
                if (!b.IsPressable) continue;
                var rect = (RectTransform)b.transform;
                Vector3 local = rect.InverseTransformPoint(tip);
                Rect r = rect.rect;
                bool over = local.x >= r.xMin - EdgeMm && local.x <= r.xMax + EdgeMm &&
                            local.y >= r.yMin - EdgeMm && local.y <= r.yMax + EdgeMm;
                float depth = -local.z;
                if (!over || depth > PokeDetector.HoverMm * 2f || depth < -PokeDetector.BehindMm) continue;
                if (best == null || Mathf.Abs(depth) < Mathf.Abs(bestDepth))
                {
                    best = b;
                    bestDepth = depth;
                }
            }

            Dictionary<GlassButton, PokeDetector> detectors = _detectors[h];
            // Buttons the fingertip has left start over.
            if (detectors.Count > 0)
            {
                _gone.Clear();
                foreach (KeyValuePair<GlassButton, PokeDetector> d in detectors)
                    if (d.Key != best) _gone.Add(d.Key);
                foreach (GlassButton b in _gone)
                {
                    detectors[b].Update(PokeDetector.ReleaseMm * 2f, false);
                    detectors.Remove(b);
                }
            }

            bool hover = false;
            if (best != null)
            {
                if (!detectors.TryGetValue(best, out PokeDetector p)) detectors[best] = p = new PokeDetector();
                if (p.Update(bestDepth, true))
                {
                    best.Press();
                    Debug.Log($"[Hands] poked '{best.name}'");
                }
                hover = p.Hovered;
            }
            SetHover(h, hover ? best : null);
        }

        private readonly List<GlassButton> _gone = new List<GlassButton>();

        private void Forget(int h)
        {
            _detectors[h].Clear();
            SetHover(h, null);
        }

        private void SetHover(int h, GlassButton b)
        {
            if (_hovered[h] == b) return;
            if (_hovered[h] != null && _hovered[1 - h] != _hovered[h]) _hovered[h].SetHovered(false);
            _hovered[h] = b;
            if (b != null) b.SetHovered(true);
        }
    }
}
