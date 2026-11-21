using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// Shows what a held panel is snapped to (Ryan, 2026-11-15: the snap felt
    /// uncertain with nothing to see). The panel's own glass edges glow: its
    /// two upright edges when it stands straight up, its top and bottom when
    /// it is square to the keys, so each snap lights the edges it
    /// straightened. A snap flashes bright, then settles; leaving the snap or
    /// letting go fades it out (SnapGlow). Grid lines, a big faint square and
    /// then light bars beside the panel came first; Ryan didn't like how any
    /// of them looked (2026-11-15, 2026-11-20, round 8 on 2026-11-21). Only
    /// the outer Panel glass glows, not the rows and buttons on it. Built by
    /// PanelGrabber.
    /// </summary>
    public sealed class SnapGuide : MonoBehaviour
    {
        private GrabbablePanel _panel;
        private float _uprightSince = -1f, _squareSince = -1f;
        // Brightness of the left, right, bottom and top edges.
        private float _left, _right, _bottom, _top;
        private readonly List<GlassPanel> _lit = new List<GlassPanel>();

        public static SnapGuide Create(Transform parent)
        {
            var go = new GameObject(nameof(SnapGuide));
            go.transform.SetParent(parent, false);
            return go.AddComponent<SnapGuide>();
        }

        /// <summary>Every frame: <paramref name="panel"/> null means nothing is held.</summary>
        public void Show(GrabbablePanel panel, PanelPose.Snap snap)
        {
            // A different panel picked up: the last one goes dark at once.
            if (panel != null && panel != _panel)
            {
                Clear();
                _panel = panel;
            }

            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            bool upright = panel != null && snap.Upright, square = panel != null && snap.Square;
            if (upright && _uprightSince < 0f) _uprightSince = now;
            if (!upright) _uprightSince = -1f;
            if (square && _squareSince < 0f) _squareSince = now;
            if (!square) _squareSince = -1f;

            float side = SnapGlow.Step(_left, upright, now - _uprightSince, dt);
            float ends = SnapGlow.Step(_bottom, square, now - _squareSince, dt);
            _left = _right = side;
            _bottom = _top = ends;
            if (_panel == null) return;

            if (side <= 0f && ends <= 0f)
            {
                Clear();
                return;
            }
            Paint(_panel);
        }

        private void Paint(GrabbablePanel panel)
        {
            Rect outline = panel.Outline;
            _lit.Clear();
            foreach (GlassPanel g in panel.Glass)
            {
                if (g == null || g.Style != GlassStyle.Panel) continue;
                if (!g.enabled)
                {
                    g.SetEdgeGlow(Vector4.zero);
                    continue;
                }
                Vector4 on = SnapGlow.Sides(panel.RectOf(g), outline, true, true);
                g.SetEdgeGlow(new Vector4(on.x * _left, on.y * _right, on.z * _bottom, on.w * _top));
                _lit.Add(g);
            }
        }

        private void Clear()
        {
            foreach (GlassPanel g in _lit)
                if (g != null) g.SetEdgeGlow(Vector4.zero);
            _lit.Clear();
            _left = _right = _bottom = _top = 0f;
            _panel = null;
        }

        private void OnDisable() => Clear();
    }
}
