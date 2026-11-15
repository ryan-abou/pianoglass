using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// Shows what a held panel is snapped to (Ryan, 2026-11-15: the snap felt
    /// uncertain with nothing to see). While it is snapped (straight up, square
    /// to the keys, or both), a very faint square much bigger than the panel
    /// lies in the panel's own plane, with a hairline edge: the flat plane it
    /// is locked into. A little brighter when both are snapped. The first
    /// version drew grid lines; Ryan asked for a plane instead (2026-11-15).
    /// Uses the key overlay's see-through material (an opaque one would show
    /// a solid grey sheet); built by PanelGrabber.
    /// </summary>
    public sealed class SnapGuide : MonoBehaviour
    {
        /// <summary>How much bigger than the panel (times its size, plus mm
        /// all round), and how far behind its face (mm).</summary>
        private const float Grow = 2.5f, MarginMm = 150f, BehindMm = 4f;
        /// <summary>See-through amounts: the square, and its edge.</summary>
        private const float FillAlpha = 0.05f, BothFillAlpha = 0.08f, EdgeAlpha = 0.18f;
        private const float EdgeWidth = 0.0008f;
        private static readonly Color Tint = new Color(0.85f, 0.9f, 0.95f, 1f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Transform _fill;
        private MeshRenderer _fillRenderer;
        private LineRenderer _edge;
        private MaterialPropertyBlock _block;
        private bool _both;

        public static SnapGuide Create(Transform parent, Material seeThrough)
        {
            if (seeThrough == null)
            {
                Debug.LogWarning("[Panels] no see-through material: the snap guide is off");
                return null;
            }
            var go = new GameObject(nameof(SnapGuide));
            go.transform.SetParent(parent, false);
            SnapGuide g = go.AddComponent<SnapGuide>();
            g._block = new MaterialPropertyBlock();

            // The square: a quad, which faces -Z, the player's side of a panel.
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Plane";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(go.transform, false);
            g._fill = quad.transform;
            g._fillRenderer = quad.GetComponent<MeshRenderer>();
            g._fillRenderer.sharedMaterial = seeThrough;
            g._fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g._fillRenderer.receiveShadows = false;

            var edge = new GameObject("Edge");
            edge.transform.SetParent(go.transform, false);
            g._edge = edge.AddComponent<LineRenderer>();
            g._edge.sharedMaterial = seeThrough;
            g._edge.useWorldSpace = true;
            g._edge.loop = true;
            g._edge.positionCount = 4;
            g._edge.startWidth = g._edge.endWidth = EdgeWidth;
            g._edge.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g._edge.receiveShadows = false;
            g.Paint(g._edge, EdgeAlpha);
            g.Paint(g._fillRenderer, FillAlpha);

            g.Show(null, default);
            return g;
        }

        private void Paint(Renderer r, float alpha)
        {
            Color c = Tint;
            c.a = alpha;
            _block.SetColor(BaseColorId, c);
            _block.SetColor(ColorId, c);
            r.SetPropertyBlock(_block);
        }

        /// <summary>Every frame: <paramref name="panel"/> null hides it.</summary>
        public void Show(GrabbablePanel panel, PanelPose.Snap snap)
        {
            bool any = panel != null && (snap.Upright || snap.Square);
            _fillRenderer.enabled = any;
            _edge.enabled = any;
            if (!any) return;

            bool both = snap.Upright && snap.Square;
            if (both != _both)
            {
                _both = both;
                Paint(_fillRenderer, both ? BothFillAlpha : FillAlpha);
            }

            Rect r = panel.Outline;
            Vector2 c = r.center, h = r.size * 0.5f * Grow + new Vector2(MarginMm, MarginMm);
            Transform t = panel.transform;
            Vector3 P(float x, float y) => t.TransformPoint(new Vector3(c.x + x, c.y + y, BehindMm));

            _fill.SetPositionAndRotation(P(0f, 0f), t.rotation);
            _fill.localScale = new Vector3(t.TransformVector(new Vector3(2f * h.x, 0f, 0f)).magnitude,
                                           t.TransformVector(new Vector3(0f, 2f * h.y, 0f)).magnitude, 1f);

            _edge.SetPosition(0, P(-h.x, -h.y));
            _edge.SetPosition(1, P(-h.x, h.y));
            _edge.SetPosition(2, P(h.x, h.y));
            _edge.SetPosition(3, P(h.x, -h.y));
        }
    }
}
