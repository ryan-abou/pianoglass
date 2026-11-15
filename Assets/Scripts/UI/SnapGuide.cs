using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// Shows what a held panel is snapped to (Ryan, 2026-11-15: the snap felt
    /// uncertain with nothing to see). Faint lines in the panel's own plane,
    /// a little bigger than it:
    ///   - a frame, whenever anything is snapped;
    ///   - up-and-down lines, while it stands straight up (no tip back or forward);
    ///   - side-to-side lines, while it is square to the keys (no turn left or right).
    /// Both together: a grid, the flat upright plane facing you square on.
    /// Drawn with the hand ray's material; built by PanelGrabber.
    /// </summary>
    public sealed class SnapGuide : MonoBehaviour
    {
        /// <summary>How much bigger than the panel, and how far behind its face (mm).</summary>
        private const float Grow = 1.12f, BehindMm = 4f;
        private const float Width = 0.0012f;

        private LineRenderer _frame;
        private readonly LineRenderer[] _uprights = new LineRenderer[3];
        private readonly LineRenderer[] _squares = new LineRenderer[3];

        public static SnapGuide Create(Transform parent, LineRenderer template)
        {
            if (template == null || template.sharedMaterial == null) return null;
            var go = new GameObject(nameof(SnapGuide));
            go.transform.SetParent(parent, false);
            SnapGuide g = go.AddComponent<SnapGuide>();
            g._frame = g.Line("Frame", template, 4, true);
            for (int i = 0; i < 3; i++)
            {
                g._uprights[i] = g.Line($"Upright {i}", template, 2, false);
                g._squares[i] = g.Line($"Square {i}", template, 2, false);
            }
            g.Show(null, default);
            return g;
        }

        private LineRenderer Line(string name, LineRenderer template, int points, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            LineRenderer l = go.AddComponent<LineRenderer>();
            l.sharedMaterial = template.sharedMaterial;
            l.useWorldSpace = true;
            l.positionCount = points;
            l.loop = loop;
            l.startWidth = l.endWidth = Width;
            l.startColor = l.endColor = template.startColor;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            return l;
        }

        /// <summary>Every frame: <paramref name="panel"/> null hides it.</summary>
        public void Show(GrabbablePanel panel, PanelPose.Snap snap)
        {
            bool any = panel != null && (snap.Upright || snap.Square);
            _frame.enabled = any;
            for (int i = 0; i < 3; i++)
            {
                _uprights[i].enabled = any && snap.Upright;
                _squares[i].enabled = any && snap.Square;
            }
            if (!any) return;

            Rect r = panel.Outline;
            Vector2 c = r.center, h = r.size * 0.5f * Grow;
            Transform t = panel.transform;
            Vector3 P(float x, float y) => t.TransformPoint(new Vector3(c.x + x, c.y + y, BehindMm));

            _frame.SetPosition(0, P(-h.x, -h.y));
            _frame.SetPosition(1, P(-h.x, h.y));
            _frame.SetPosition(2, P(h.x, h.y));
            _frame.SetPosition(3, P(h.x, -h.y));
            for (int i = 0; i < 3; i++)
            {
                float f = (i + 1) / 4f * 2f - 1f; // -0.5, 0, 0.5
                _uprights[i].SetPosition(0, P(h.x * f, -h.y));
                _uprights[i].SetPosition(1, P(h.x * f, h.y));
                _squares[i].SetPosition(0, P(-h.x, h.y * f));
                _squares[i].SetPosition(1, P(h.x, h.y * f));
            }
        }
    }
}
