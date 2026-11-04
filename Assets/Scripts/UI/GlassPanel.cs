using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// Puts Liquid Glass behind a RectTransform in a world-space Canvas: one
    /// quad, sized to the rect plus padding, a little behind it, drawn with
    /// LiquidGlass.shader. <see cref="style"/> picks the layer -- a Panel, a
    /// Row nested inside one (iOS grouped style) or a Pill -- and the look
    /// comes from that layer of GlassTokens, written into a property block,
    /// so the token asset is the only thing a tuning round edits.
    ///
    /// Nested glass sits closer to the viewer than its parent (smaller
    /// depthBehindMetres), and all glass draws before the Transparent queue,
    /// so Canvas text always lands on top.
    ///
    /// The quad is a child built at runtime, not saved into the scene: it
    /// follows the Canvas wherever CanvasCalibrationFollower puts it, and
    /// is rebuilt only when the rect or its scale changes. Material and
    /// tokens are assigned by SceneSetup or by whoever adds the component at
    /// runtime (<see cref="Init"/>) -- never looked up.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GlassPanel : MonoBehaviour
    {
        [SerializeField] private Material glassMaterial;
        [SerializeField] private GlassTokens tokens;
        [SerializeField] private GlassStyle style = GlassStyle.Panel;
        [Tooltip("Glass beyond the rect on each side, metres.")]
        [SerializeField] private Vector2 paddingMetres = new Vector2(0.012f, 0.012f);
        [Tooltip("How far behind the rect the glass sits, metres -- enough that " +
                 "the text never z-fights it. Nested glass uses less than its parent.")]
        [SerializeField] private float depthBehindMetres = 0.002f;

        private RectTransform _rect;
        private Transform _quad;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private MaterialPropertyBlock _block;
        private Rect _builtRect;
        private float _builtScale = -1f;
        private Color? _tintOverride;

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int TopGlowId = Shader.PropertyToID("_TopGlow");
        private static readonly int BevelWidthId = Shader.PropertyToID("_BevelWidth");
        private static readonly int BevelLightId = Shader.PropertyToID("_BevelLight");
        private static readonly int BackLightId = Shader.PropertyToID("_BackLight");
        private static readonly int LightDirId = Shader.PropertyToID("_LightDir");
        private static readonly int ParallaxId = Shader.PropertyToID("_Parallax");
        private static readonly int HairlineColorId = Shader.PropertyToID("_HairlineColor");
        private static readonly int HairlineWidthId = Shader.PropertyToID("_HairlineWidth");
        private static readonly int SheenId = Shader.PropertyToID("_SheenStrength");
        private static readonly int FresnelId = Shader.PropertyToID("_FresnelStrength");
        private static readonly int ChromaId = Shader.PropertyToID("_ChromaStrength");
        private static readonly int HalfSizeId = Shader.PropertyToID("_HalfSize");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");

        public GlassTokens Tokens => tokens;

        /// <summary>For glass added from code (rows and cells a card builds):
        /// call straight after AddComponent, before the first frame.</summary>
        public void Init(Material material, GlassTokens glassTokens, GlassStyle glassStyle,
                         Vector2 padding, float depthBehind)
        {
            glassMaterial = material;
            tokens = glassTokens;
            style = glassStyle;
            paddingMetres = padding;
            depthBehindMetres = depthBehind;
            _builtScale = -1f;
            if (_renderer == null && glassMaterial != null) CreateQuad();
        }

        /// <summary>A solid colour instead of the layer's tint (alpha
        /// included) -- the heat strip's cells. Null returns to the token.</summary>
        public void SetTint(Color? tint)
        {
            _tintOverride = tint;
            _builtScale = -1f; // re-applied on the next LateUpdate
        }

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _block = new MaterialPropertyBlock();
            if (glassMaterial != null) CreateQuad();
        }

        private void CreateQuad()
        {
            if (_renderer != null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            if (_rect == null) _rect = (RectTransform)transform;

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
            if (_renderer == null) return;
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
            BuildQuad(_mesh, half, metresPerUnit);

            // +z is away from the viewer on a Canvas, so "behind" is +z.
            _quad.localPosition = new Vector3(rect.center.x, rect.center.y, depthBehindMetres / metresPerUnit);
            _quad.localRotation = Quaternion.identity;
            _quad.localScale = Vector3.one;

            _renderer.GetPropertyBlock(_block);
            Apply(_block, tokens, style, half, _tintOverride);
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>The quad for glass of half size <paramref name="half"/>
        /// (metres): vertices in local units (metres / metresPerUnit), uv0 in
        /// metres from the centre, which is what the shader's SDF works in.</summary>
        public static void BuildQuad(Mesh mesh, Vector2 half, float metresPerUnit)
        {
            Vector2 quad = GlassShape.QuadHalfMetres(half);
            Vector2 u = quad / metresPerUnit;
            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(-u.x, -u.y, 0f), new Vector3(-u.x, u.y, 0f),
                new Vector3(u.x, u.y, 0f), new Vector3(u.x, -u.y, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(-quad.x, -quad.y), new Vector2(-quad.x, quad.y),
                new Vector2(quad.x, quad.y), new Vector2(quad.x, -quad.y),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
        }

        /// <summary>Everything the shader needs for one surface, from the
        /// tokens (code defaults when null).</summary>
        public static void Apply(MaterialPropertyBlock block, GlassTokens tokens, GlassStyle style,
                                 Vector2 half, Color? tintOverride = null)
        {
            GlassTokens t = tokens != null ? tokens : Defaults;
            GlassLayer layer = t.Layer(style);
            block.SetColor(TintId, tintOverride ?? layer.tint);
            block.SetFloat(TopGlowId, layer.topGlow);
            block.SetFloat(BevelWidthId, layer.bevelWidth);
            block.SetFloat(BevelLightId, layer.bevelLight);
            block.SetFloat(BackLightId, t.backLight);
            Vector2 light = t.lightDirection.sqrMagnitude > 1e-6f ? t.lightDirection.normalized : Vector2.up;
            block.SetVector(LightDirId, new Vector4(light.x, light.y, 0f, 0f));
            block.SetFloat(ParallaxId, t.parallax);
            block.SetColor(HairlineColorId, t.hairlineColor);
            block.SetFloat(HairlineWidthId, t.hairlineWidth);
            // The diagonal sheen is one sweep across a big surface; on a row
            // of small pills it would repeat into stripes.
            float sheenScale = style == GlassStyle.Panel ? 1f : style == GlassStyle.Row ? 0.6f : 0.3f;
            block.SetFloat(SheenId, t.sheenStrength * sheenScale);
            block.SetFloat(FresnelId, t.fresnelStrength);
            // The rainbow fringe belongs to the big surfaces; small ones
            // would turn it into noise.
            block.SetFloat(ChromaId, style == GlassStyle.Panel ? t.chromaStrength : t.chromaStrength * 0.4f);
            block.SetVector(HalfSizeId, new Vector4(half.x, half.y, 0f, 0f));
            block.SetFloat(RadiusId, GlassShape.RadiusFor(style, layer.cornerRadius, half));
        }

        private static GlassTokens _defaults;
        private static GlassTokens Defaults
        {
            get
            {
                if (_defaults == null)
                {
                    _defaults = ScriptableObject.CreateInstance<GlassTokens>();
                    _defaults.hideFlags = HideFlags.HideAndDontSave;
                }
                return _defaults;
            }
        }
    }
}
