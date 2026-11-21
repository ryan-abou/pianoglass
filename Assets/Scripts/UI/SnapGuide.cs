using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// Shows what a held panel is snapped to (Ryan, 2026-11-15: the snap felt
    /// uncertain with nothing to see). Soft light bars just outside the
    /// panel's edges: its two upright edges light up when it stands straight
    /// up, its top and bottom edges when it is square to the keys, so each
    /// snap lights the edges it straightened. A snap flashes them brighter
    /// for a moment, then they settle to faint. Grid lines, then a big faint
    /// square came first; Ryan didn't like how either looked (2026-11-15,
    /// 2026-11-20). Uses the key overlay's see-through material; built by
    /// PanelGrabber.
    /// </summary>
    public sealed class SnapGuide : MonoBehaviour
    {
        /// <summary>Mm outside the panel's outline, and kept clear of each
        /// corner so the bars read as edges, not a box.</summary>
        private const float GapMm = 7f, CornerMm = 18f;
        /// <summary>Metres wide.</summary>
        private const float Width = 0.003f;
        /// <summary>See-through amounts: settled, and the flash on snapping.</summary>
        private const float SettledAlpha = 0.7f, FlashAlpha = 1f;
        /// <summary>Seconds for the flash to fade.</summary>
        private const float FlashSeconds = 0.3f;
        // Twice as wide and more than twice as solid as at first: Ryan could
        // barely see it when looking for it (2026-11-21).
        private static readonly Color Tint = new Color(0.92f, 0.97f, 1f, 1f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Left and right (upright), bottom and top (square to the keys).
        private readonly LineRenderer[] _bars = new LineRenderer[4];
        private readonly float[] _alpha = new float[4];
        private MaterialPropertyBlock _block;
        private float _uprightSince = -1f, _squareSince = -1f;

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
            string[] names = { "Left", "Right", "Bottom", "Top" };
            for (int i = 0; i < 4; i++)
            {
                var bar = new GameObject(names[i]);
                bar.transform.SetParent(go.transform, false);
                LineRenderer line = bar.AddComponent<LineRenderer>();
                line.sharedMaterial = seeThrough;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = line.endWidth = Width;
                line.numCapVertices = 4; // rounded ends
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                g._bars[i] = line;
                g._alpha[i] = -1f;
            }
            return g;
        }

        /// <summary>Every frame: <paramref name="panel"/> null hides it.</summary>
        public void Show(GrabbablePanel panel, PanelPose.Snap snap)
        {
            float now = Time.unscaledTime;
            bool upright = panel != null && snap.Upright, square = panel != null && snap.Square;
            if (upright && _uprightSince < 0f) _uprightSince = now;
            if (!upright) _uprightSince = -1f;
            if (square && _squareSince < 0f) _squareSince = now;
            if (!square) _squareSince = -1f;

            for (int i = 0; i < 4; i++)
            {
                bool on = i < 2 ? upright : square;
                _bars[i].enabled = on;
                if (!on) continue;
                float since = now - (i < 2 ? _uprightSince : _squareSince);
                float flash = Mathf.Clamp01(1f - since / FlashSeconds);
                Paint(i, Mathf.Lerp(SettledAlpha, FlashAlpha, flash * flash));
            }
            if (!upright && !square) return;

            Rect r = panel.Outline;
            float x0 = r.xMin - GapMm, x1 = r.xMax + GapMm, y0 = r.yMin - GapMm, y1 = r.yMax + GapMm;
            Transform t = panel.transform;
            Vector3 P(float x, float y) => t.TransformPoint(new Vector3(x, y, 0f));
            Set(0, P(x0, y0 + CornerMm), P(x0, y1 - CornerMm));
            Set(1, P(x1, y0 + CornerMm), P(x1, y1 - CornerMm));
            Set(2, P(x0 + CornerMm, y0), P(x1 - CornerMm, y0));
            Set(3, P(x0 + CornerMm, y1), P(x1 - CornerMm, y1));
        }

        private void Set(int i, Vector3 a, Vector3 b)
        {
            if (!_bars[i].enabled) return;
            _bars[i].SetPosition(0, a);
            _bars[i].SetPosition(1, b);
        }

        private void Paint(int i, float alpha)
        {
            if (Mathf.Abs(alpha - _alpha[i]) < 0.005f) return;
            _alpha[i] = alpha;
            Color c = Tint;
            c.a = alpha;
            _block.SetColor(BaseColorId, c);
            _block.SetColor(ColorId, c);
            _bars[i].SetPropertyBlock(_block);
        }
    }
}
