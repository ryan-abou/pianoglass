using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Makes one button on Meta's controller model light up (#42), the way
    /// VR tutorials point at a button: the controller is drawn over the real
    /// one as a see-through ghost, and the button's own part of the model
    /// glows and pulses, with a soft halo around it.
    ///
    /// Meta's models are one skinned mesh, with a bone for each button
    /// (right_b_button_oculus on the Quest 3's Touch Plus). The button's part
    /// is the triangles that bone moves; they are copied into a second
    /// skinned mesh on the same bones, so the glow sits exactly on the button
    /// and moves with it. That copy is made in the editor (<see cref="BuildCap"/>,
    /// SceneSetup) and saved as an asset: a build can't read Meta's mesh. A
    /// model without a saved copy gets only the halo, on the button's bone.
    /// The same code makes the editor preview (MetaButtonPreview), so the
    /// picture is what ships.
    /// </summary>
    public sealed class ControllerButtonGlow : MonoBehaviour
    {
        /// <summary>The Meta button's bone: Touch Plus (Quest 3), Touch Pro,
        /// and the Quest 2's Touch, which calls it b_button03.</summary>
        public static readonly string[] MetaButtonBones = { "right_b_button_oculus", "b_button_oculus", "b_button03" };

        public static readonly Color Glow = new Color(0.45f, 0.88f, 1f, 1f);
        private static readonly Color Ghost = new Color(0.16f, 0.18f, 0.22f, 0.55f);
        private const float HaloMetres = 0.034f, PulseHz = 1.1f;
        /// <summary>A triangle belongs to the button when all three corners
        /// follow its bone at least this much.</summary>
        private const float ButtonWeight = 0.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private SkinnedMeshRenderer _cap;
        private Transform _halo;
        private Material _capMaterial, _haloMaterial;
        private readonly List<Renderer> _model = new List<Renderer>();
        private readonly List<Material[]> _modelWas = new List<Material[]>();
        private Material _ghostMaterial;

        /// <summary>Which bone it lit, for the log.</summary>
        public string BoneName { get; private set; }

        /// <summary>Lights the first of <paramref name="boneNames"/> found
        /// in an active part of <paramref name="modelRoot"/>. Null if none is.</summary>
        /// <param name="translucent">An unlit, alpha-blended material
        /// (KeyOverlayTranslucent): copied for the ghost, the button and the halo.</param>
        /// <param name="cap">The button's part, from <see cref="BuildCap"/>; used
        /// only if it was cut from this very mesh (same vertex count).</param>
        public static ControllerButtonGlow Add(GameObject modelRoot, Material translucent, Mesh cap, params string[] boneNames)
        {
            if (modelRoot == null || translucent == null) return null;
            foreach (string name in boneNames)
            {
                foreach (SkinnedMeshRenderer smr in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    if (smr.sharedMesh == null || smr.bones == null) continue;
                    int bone = System.Array.FindIndex(smr.bones, b => b != null && b.name == name);
                    if (bone < 0) continue;
                    var glow = modelRoot.AddComponent<ControllerButtonGlow>();
                    glow.BoneName = name;
                    glow.Build(smr, bone, translucent, cap);
                    return glow;
                }
            }
            return null;
        }

        /// <summary>The triangles <paramref name="bone"/> moves, as a mesh on the
        /// same vertices and bones. Editor only in practice: it reads the mesh,
        /// which a build of Meta's model can't.</summary>
        public static Mesh BuildCap(Mesh mesh, int bone, string name)
        {
            BoneWeight[] weights = mesh.boneWeights;
            var triangles = new List<int>();
            int[] all = mesh.triangles;
            for (int i = 0; i + 2 < all.Length; i += 3)
            {
                if (WeightOf(weights, all[i], bone) >= ButtonWeight &&
                    WeightOf(weights, all[i + 1], bone) >= ButtonWeight &&
                    WeightOf(weights, all[i + 2], bone) >= ButtonWeight)
                {
                    triangles.Add(all[i]);
                    triangles.Add(all[i + 1]);
                    triangles.Add(all[i + 2]);
                }
            }
            var cap = new Mesh { name = name, indexFormat = mesh.indexFormat };
            cap.vertices = mesh.vertices;
            cap.normals = mesh.normals;
            cap.boneWeights = weights;
            cap.bindposes = mesh.bindposes;
            cap.SetTriangles(triangles, 0);
            // Its bounds are the button's own, for placing the halo: the
            // renderer culls with the whole controller's (MakeCap).
            Vector3[] v = mesh.vertices;
            var tight = new Bounds(v[triangles.Count > 0 ? triangles[0] : 0], Vector3.zero);
            foreach (int i in triangles) tight.Encapsulate(v[i]);
            cap.bounds = tight;
            return cap;
        }

        private Transform _bone;
        /// <summary>Where the button is in the model mesh's own space, and that space.</summary>
        private Vector3 _capCentre;
        private Transform _capSpace;

        private void Build(SkinnedMeshRenderer source, int bone, Material translucent, Mesh cap)
        {
            _bone = source.bones[bone];

            // The ghost: every renderer of this model, see-through.
            _ghostMaterial = new Material(translucent) { name = "ControllerGhost" };
            _ghostMaterial.SetColor(BaseColorId, Ghost);
            Transform model = source.transform.parent != null ? source.transform.parent : source.transform;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(false))
            {
                _model.Add(r);
                _modelWas.Add(r.sharedMaterials);
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = _ghostMaterial;
                r.sharedMaterials = mats;
            }

            // The button: its saved part, or cut now if this mesh can be read (the editor).
            Mesh mesh = source.sharedMesh;
            if (cap == null || cap.vertexCount != mesh.vertexCount)
                cap = mesh.isReadable ? BuildCap(mesh, bone, "MetaButtonCap") : null;
            _capMaterial = new Material(translucent) { name = "MetaButtonGlow", renderQueue = translucent.renderQueue + 10 };
            if (cap != null) MakeCap(source, cap);
            else Debug.LogWarning($"[MetaButton] no saved part for '{mesh.name}': halo only");

            BuildHalo(translucent);
            Pulse(0f);
            Debug.Log($"[MetaButton] lighting '{BoneName}'" + (cap != null ? $": {cap.GetIndexCount(0) / 3} triangles" : ""));
        }

        private void MakeCap(SkinnedMeshRenderer source, Mesh cap)
        {
            var capGo = new GameObject("MetaButtonGlow");
            capGo.transform.SetParent(source.transform.parent, false);
            capGo.transform.localPosition = source.transform.localPosition;
            capGo.transform.localRotation = source.transform.localRotation;
            capGo.transform.localScale = source.transform.localScale;
            _cap = capGo.AddComponent<SkinnedMeshRenderer>();
            _cap.sharedMesh = cap;
            _cap.bones = source.bones;
            _cap.rootBone = source.rootBone;
            _cap.localBounds = source.localBounds;
            _cap.updateWhenOffscreen = true;
            _cap.sharedMaterial = _capMaterial;
            _capCentre = cap.bounds.center;
            _capSpace = source.transform;
        }

        private void BuildHalo(Material translucent)
        {
            // The halo: a soft round glow facing the eye.
            var haloGo = new GameObject("MetaButtonHalo");
            haloGo.transform.SetParent(transform, false);
            haloGo.AddComponent<MeshFilter>().sharedMesh = Quad();
            var haloRenderer = haloGo.AddComponent<MeshRenderer>();
            _haloMaterial = new Material(translucent) { name = "MetaButtonHalo", renderQueue = translucent.renderQueue + 5 };
            _haloMaterial.SetTexture(BaseMapId, RadialTexture());
            haloRenderer.sharedMaterial = _haloMaterial;
            haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _halo = haloGo.transform;
            _halo.localScale = Vector3.one * HaloMetres / Mathf.Max(1e-6f, transform.lossyScale.x);
        }

        private static float WeightOf(BoneWeight[] weights, int vertex, int bone)
        {
            if (weights == null || vertex >= weights.Length) return 0f;
            BoneWeight w = weights[vertex];
            float sum = 0f;
            if (w.boneIndex0 == bone) sum += w.weight0;
            if (w.boneIndex1 == bone) sum += w.weight1;
            if (w.boneIndex2 == bone) sum += w.weight2;
            if (w.boneIndex3 == bone) sum += w.weight3;
            return sum;
        }

        private void LateUpdate() => Pulse(Time.unscaledTime);

        /// <summary>Glow strength at <paramref name="time"/> seconds, and the halo on the eye.</summary>
        public void Pulse(float time)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(time * PulseHz * Mathf.PI * 2f);
            Color cap = Color.Lerp(Glow, Color.white, 0.35f * k);
            cap.a = Mathf.Lerp(0.75f, 1f, k);
            _capMaterial.SetColor(BaseColorId, cap);
            Color halo = Glow;
            halo.a = Mathf.Lerp(0.30f, 0.65f, k);
            _haloMaterial.SetColor(BaseColorId, halo);

            if (_bone == null) return;
            Vector3 centre = _capSpace != null ? _capSpace.TransformPoint(_capCentre) : _bone.position;
            Camera eye = Camera.main;
            Vector3 toEye = eye != null ? (eye.transform.position - centre).normalized : Vector3.up;
            _halo.SetPositionAndRotation(centre + toEye * 0.002f, Quaternion.LookRotation(-toEye, Vector3.up));
        }

        /// <summary>Puts the model's own materials back.</summary>
        private void OnDestroy()
        {
            for (int i = 0; i < _model.Count; i++)
                if (_model[i] != null) _model[i].sharedMaterials = _modelWas[i];
            if (_cap != null) Destroy(_cap.gameObject);
        }

        private static Mesh Quad()
        {
            var m = new Mesh { name = "HaloQuad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            return m;
        }

        private static Texture2D _radial;

        /// <summary>White, fading out from the middle: the halo's shape.</summary>
        private static Texture2D RadialTexture()
        {
            if (_radial != null) return _radial;
            const int size = 64;
            _radial = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HaloRadial" };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
            _radial.SetPixels(pixels);
            _radial.Apply();
            return _radial;
        }
    }
}
