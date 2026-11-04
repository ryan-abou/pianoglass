using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// Puts a Liquid Glass backing behind a world-space Canvas (or any
    /// RectTransform in one): one quad, sized to the rect plus padding,
    /// a couple of millimetres behind it, drawn with LiquidGlass.shader.
    /// The look comes from GlassTokens, written into a property block, so
    /// the token asset is the only thing a tuning round edits.
    ///
    /// The quad is a child built at runtime, not saved into the scene: it
    /// follows the Canvas wherever CanvasCalibrationFollower puts it, and
    /// is rebuilt only when the rect or its scale changes. Material and
    /// tokens are assigned by SceneSetup (Configure Liquid Glass) -- never
    /// looked up at runtime.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GlassPanel : MonoBehaviour
    {
        [SerializeField] private Material glassMaterial;
        [SerializeField] private GlassTokens tokens;
        [Tooltip("Glass beyond the rect on each side, metres.")]
        [SerializeField] private Vector2 paddingMetres = new Vector2(0.012f, 0.012f);
        [Tooltip("How far behind the rect the glass sits, metres -- enough that " +
                 "the text never z-fights it, small enough to read as one surface.")]
        [SerializeField] private float depthBehindMetres = 0.002f;

        private RectTransform _rect;
        private Transform _quad;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private MaterialPropertyBlock _block;
        private Rect _builtRect;
        private float _builtScale = -1f;

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int TopGlowId = Shader.PropertyToID("_TopGlow");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
        private static readonly int RimBottomId = Shader.PropertyToID("_RimBottom");
        private static readonly int SheenColorId = Shader.PropertyToID("_SheenColor");
        private static readonly int SheenStrengthId = Shader.PropertyToID("_SheenStrength");
        private static readonly int SheenPowerId = Shader.PropertyToID("_SheenPower");
        private static readonly int HalfSizeId = Shader.PropertyToID("_HalfSize");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");

        public GlassTokens Tokens => tokens;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _block = new MaterialPropertyBlock();

            if (glassMaterial == null)
            {
                Debug.LogError($"[Glass] '{name}' has no glass material -- run PianoGlass/Scene/Configure Liquid Glass.");
                enabled = false;
                return;
            }

            var go = new GameObject("LiquidGlass") { hideFlags = HideFlags.DontSave };
            _quad = go.transform;
            _quad.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh = new Mesh { name = "LiquidGlass" };
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = glassMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            // Awake runs even on a disabled component (ResultsCard may have
            // hidden it first), and then OnEnable/OnDisable do not -- so the
            // quad starts out matching this component's own state.
            _renderer.enabled = enabled;
        }

        private void OnEnable()
        {
            if (_renderer != null) _renderer.enabled = true;
        }

        private void OnDisable()
        {
            if (_renderer != null) _renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_quad != null) Destroy(_quad.gameObject);
            if (_mesh != null) Destroy(_mesh);
        }

        private void LateUpdate()
        {
            Rect rect = _rect.rect;
            float scale = Mathf.Abs(transform.lossyScale.x);
            if (rect != _builtRect || !Mathf.Approximately(scale, _builtScale)) Rebuild(rect, scale);
        }

        private void Rebuild(Rect rect, float metresPerUnit)
        {
            _builtRect = rect;
            _builtScale = metresPerUnit;
            if (metresPerUnit <= 0f) return;

            Vector2 half = GlassShape.HalfSizeMetres(rect.size, metresPerUnit, paddingMetres);
            Vector2 quad = GlassShape.QuadHalfMetres(half);
            Vector2 quadUnits = quad / metresPerUnit;

            // Mesh in the Canvas's own units (the child inherits its 0.001
            // scale); uv0 in metres, which is what the shader's SDF works in.
            _mesh.Clear();
            _mesh.vertices = new[]
            {
                new Vector3(-quadUnits.x, -quadUnits.y, 0f), new Vector3(-quadUnits.x, quadUnits.y, 0f),
                new Vector3(quadUnits.x, quadUnits.y, 0f), new Vector3(quadUnits.x, -quadUnits.y, 0f),
            };
            _mesh.uv = new[]
            {
                new Vector2(-quad.x, -quad.y), new Vector2(-quad.x, quad.y),
                new Vector2(quad.x, quad.y), new Vector2(quad.x, -quad.y),
            };
            _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _mesh.RecalculateBounds();

            // +z is away from the viewer on a Canvas, so "behind" is +z.
            _quad.localPosition = new Vector3(rect.center.x, rect.center.y, depthBehindMetres / metresPerUnit);
            _quad.localRotation = Quaternion.identity;
            _quad.localScale = Vector3.one;

            Apply(half);
        }

        private void Apply(Vector2 half)
        {
            _renderer.GetPropertyBlock(_block);
            if (tokens != null)
            {
                _block.SetColor(TintId, tokens.tint);
                _block.SetFloat(TopGlowId, tokens.topGlow);
                _block.SetColor(RimColorId, tokens.rimColor);
                _block.SetFloat(RimWidthId, tokens.rimWidth);
                _block.SetFloat(RimBottomId, tokens.rimBottom);
                _block.SetColor(SheenColorId, tokens.sheenColor);
                _block.SetFloat(SheenStrengthId, tokens.sheenStrength);
                _block.SetFloat(SheenPowerId, tokens.sheenPower);
            }
            float radius = GlassShape.ClampRadius(tokens != null ? tokens.cornerRadius : 0.022f, half);
            _block.SetVector(HalfSizeId, new Vector4(half.x, half.y, 0f, 0f));
            _block.SetFloat(RadiusId, radius);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
