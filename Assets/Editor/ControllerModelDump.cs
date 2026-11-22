using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PianoGlass.EditorTools
{
    /// <summary>
    /// Writes out Meta's controller models from the Meta XR Core SDK: every
    /// asset that looks like a Touch or Touch Plus controller, and for the
    /// right Touch Plus model (Quest 3) each part's name, mesh, materials and
    /// position. The Meta button highlight (#42) lights one of those parts,
    /// so this is how its name and place are found without a headset.
    ///
    ///   Unity.exe -batchmode -quit -projectPath ... -buildTarget Android
    ///     -executeMethod PianoGlass.EditorTools.ControllerModelDump.Run
    ///
    /// Output: Logs/controller-models.txt.
    /// </summary>
    public static class ControllerModelDump
    {
        private const string Output = "Logs/controller-models.txt";

        public static void Run()
        {
            var sb = new StringBuilder();
            string[] searches = { "t:GameObject Controller", "t:Model TouchPlus", "t:Model Controller", "t:GameObject TouchPlus" };
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (string search in searches)
            {
                foreach (string guid in AssetDatabase.FindAssets(search))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!seen.Add(path)) continue;
                    if (!path.StartsWith("Packages/com.meta.xr")) continue;
                    sb.AppendLine(path);
                }
            }
            sb.AppendLine();

            foreach (string path in seen)
            {
                string lower = path.ToLowerInvariant();
                bool rightPlus = (lower.Contains("touchplus") || lower.Contains("touch_plus") || lower.Contains("quest3") ||
                                  lower.Contains("plus")) && (lower.Contains("right") || lower.Contains("_r"));
                bool prefab = lower.EndsWith("ovrcontrollerprefab.prefab");
                if (!rightPlus && !prefab) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                sb.AppendLine($"==== {path}");
                Dump(go.transform, 0, sb);
                sb.AppendLine();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllText(Output, sb.ToString());
            Debug.Log($"[ControllerModelDump] wrote {Output} ({seen.Count} assets)");
        }

        private static void Dump(Transform t, int depth, StringBuilder sb)
        {
            if (depth > 12) return;
            string pad = new string(' ', depth * 2);
            Vector3 p = t.localPosition * 1000f;
            string line = $"{pad}{t.name}  pos ({p.x:0.0}, {p.y:0.0}, {p.z:0.0}) mm  rot {t.localEulerAngles}  scale {t.localScale}";
            var smr = t.GetComponent<SkinnedMeshRenderer>();
            var mr = t.GetComponent<MeshRenderer>();
            var mf = t.GetComponent<MeshFilter>();
            Renderer r = smr != null ? (Renderer)smr : mr;
            if (r != null)
            {
                Mesh mesh = smr != null ? smr.sharedMesh : mf != null ? mf.sharedMesh : null;
                line += $"  [{r.GetType().Name} mesh={(mesh != null ? mesh.name : "-")}" +
                        $" sub={(mesh != null ? mesh.subMeshCount : 0)} bounds={r.bounds.center * 1000f}/{r.bounds.size * 1000f}";
                foreach (Material m in r.sharedMaterials)
                    line += $" mat={(m != null ? m.name + "(" + (m.shader != null ? m.shader.name : "-") + ")" : "-")}";
                line += "]";
            }
            foreach (Component c in t.GetComponents<Component>())
                if (c != null && !(c is Transform) && !(c is Renderer) && !(c is MeshFilter))
                    line += $" <{c.GetType().Name}>";
            sb.AppendLine(line);
            for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb);
        }
    }
}
