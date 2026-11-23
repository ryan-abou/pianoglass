using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Makes one button on the real controller light up (#42): Meta's
    /// controller model is lined up with the real one but not drawn, and only
    /// the button's own part of it glows and pulses, with a soft halo, right
    /// on top of the real button. (Round 13 had the whole model as a
    /// see-through ghost; Ryan wanted just the button.) Round 15: the real
    /// controller is only a camera picture, so it can't hide the glow when
    /// the button faces away; the glow fades out by itself then
    /// (<see cref="FacingFade"/>), and the halo is smaller and on the
    /// button's top.
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
        /// <summary>Round 15: was 3.4 cm, three times the button, and a big
        /// soft glow with no edge looked like light from behind the controller.</summary>
        private const float HaloMetres = 0.022f;
        public const float PulseHz = 1.1f;

        /// <summary>The glow is full while the button faces the eyes within
        /// this many degrees, and gone past the second: nothing real blocks
        /// it, so turned away it would show through the controller (round 14).</summary>
        public const float FullFacingDegrees = 60f, GoneFacingDegrees = 85f;
        /// <summary>A triangle belongs to the button when all three corners
        /// follow its bone at least this much.</summary>
        private const float ButtonWeight = 0.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private SkinnedMeshRenderer _cap;
        private Transform _halo;
        private Material _capMaterial, _haloMaterial;
        /// <summary>The model's own renderers, kept switched off.</summary>
        private readonly List<Renderer> _model = new List<Renderer>();

        /// <summary>Which bone it lit, for the log.</summary>
        public string BoneName { get; private set; }

        /// <summary>Lights the first of <paramref name="boneNames"/> found
        /// in an active part of <paramref name="modelRoot"/>. Null if none is.</summary>
        /// <param name="translucent">An unlit, alpha-blended material
        /// (KeyOverlayTranslucent): copied for the button and the halo.</param>
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
        /// <summary>Which way the button's top faces, and how far its top is
        /// above <see cref="_capCentre"/>, in the same space. Zero: unknown.</summary>
        private Vector3 _capNormal;
        private float _capLift;

        private void Build(SkinnedMeshRenderer source, int bone, Material translucent, Mesh cap)
        {
            _bone = source.bones[bone];

            // The model itself is not drawn: only the button glows.
            Transform model = source.transform.parent != null ? source.transform.parent : source.transform;
            _model.AddRange(model.GetComponentsInChildren<Renderer>(true));
            HideModel();

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
            if (cap.isReadable) (_capNormal, _capLift) = Facing(cap, _capCentre);
        }

        /// <summary>The way the button's top faces (its triangles' normals,
        /// by area: the top outweighs the sides, which mostly cancel) and the
        /// height of its top above <paramref name="centre"/> along that way.</summary>
        public static (Vector3 normal, float lift) Facing(Mesh cap, Vector3 centre)
        {
            Vector3[] v = cap.vertices;
            Vector3[] n = cap.normals;
            int[] t = cap.GetTriangles(0);
            Vector3 sum = Vector3.zero;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                float area = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).magnitude * 0.5f;
                Vector3 normal = n != null && n.Length == v.Length
                    ? n[t[i]] + n[t[i + 1]] + n[t[i + 2]]
                    : Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                sum += normal.normalized * area;
            }
            if (sum.sqrMagnitude < 1e-12f) return (Vector3.zero, 0f);
            Vector3 up = sum.normalized;
            float lift = 0f;
            foreach (int i in t) lift = Mathf.Max(lift, Vector3.Dot(v[i] - centre, up));
            return (up, lift);
        }

        /// <summary>How much glow shows when the button's top is this
        /// cosine away from pointing at the eyes: 1 facing them, 0 turned away.</summary>
        public static float FacingFade(float cosine) =>
            Mathf.InverseLerp(Mathf.Cos(GoneFacingDegrees * Mathf.Deg2Rad),
                              Mathf.Cos(FullFacingDegrees * Mathf.Deg2Rad), cosine);

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

        private void LateUpdate()
        {
            HideModel(); // in case Meta's helper switches it back on
            Pulse(Time.unscaledTime);
        }

        private void HideModel()
        {
            foreach (Renderer r in _model)
                if (r != null && r.enabled) r.enabled = false;
        }

        /// <summary>Glow strength at <paramref name="time"/> seconds, and the halo on the eye.</summary>
        public void Pulse(float time)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(time * PulseHz * Mathf.PI * 2f);

            Vector3 centre = _capSpace != null ? _capSpace.TransformPoint(_capCentre)
                           : _bone != null ? _bone.position : transform.position;
            Camera eye = Camera.main;
            Vector3 toEye = eye != null ? (eye.transform.position - centre).normalized : Vector3.up;

            // Turned away, it fades out: there is no controller in front of it to hide it.
            float face = 1f;
            Vector3 top = centre;
            if (_capSpace != null && _capNormal != Vector3.zero)
            {
                Vector3 up = _capSpace.TransformDirection(_capNormal).normalized;
                face = FacingFade(Vector3.Dot(up, toEye));
                top = centre + _capSpace.TransformVector(_capNormal * _capLift);
            }

            // More solid than before (round 15): a see-through glow read as behind the plastic.
            Color cap = Color.Lerp(Glow, Color.white, 0.35f * k);
            cap.a = Mathf.Lerp(0.85f, 1f, k) * face;
            _capMaterial.SetColor(BaseColorId, cap);
            Color halo = Glow;
            halo.a = Mathf.Lerp(0.25f, 0.5f, k) * face;
            _haloMaterial.SetColor(BaseColorId, halo);

            // On the button's top, not in the middle of it.
            _halo.SetPositionAndRotation(top + toEye * 0.002f, Quaternion.LookRotation(-toEye, Vector3.up));
        }

        private void OnDestroy()
        {
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
                pixels[y * size + x] = new Color(1f, 1f, 1f, a * a * a); // tighter than a * a
            }
            _radial.SetPixels(pixels);
            _radial.Apply();
            return _radial;
        }
    }
}
