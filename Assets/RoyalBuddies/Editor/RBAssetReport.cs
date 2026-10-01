using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RoyalBuddies.EditorTools
{
    /// <summary>
    /// Écrit un rapport texte sur les modèles importés (hiérarchie, matériaux, clips, vertex colors).
    /// Lancement : Unity -batchmode -executeMethod RoyalBuddies.EditorTools.RBAssetReport.Run
    /// </summary>
    public static class RBAssetReport
    {
        public static void Run()
        {
            AssetDatabase.Refresh();
            var sb = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/RoyalBuddies/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".glb")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                sb.AppendLine("=== " + path + " main=" + (go != null ? go.name : "NULL"));
                if (go == null) continue;
                var animator = go.GetComponentInChildren<Animator>(true);
                sb.AppendLine("  Animator: " + (animator != null ? animator.gameObject.name + " (root=" + (animator.gameObject == go) + ")" : "none"));
                var anim = go.GetComponentInChildren<Animation>(true);
                sb.AppendLine("  LegacyAnimation: " + (anim != null));
                var b = new Bounds(); bool init = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds);
                }
                sb.AppendLine("  Bounds size=" + b.size + " center=" + b.center);
                var mats = new HashSet<Material>();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mesh = (r as SkinnedMeshRenderer) != null ? ((SkinnedMeshRenderer)r).sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    string colors = mesh != null && mesh.colors32 != null && mesh.colors32.Length > 0 ? "COLOR_0" : "nocolor";
                    if (mesh != null && mesh.colors32.Length > 0 && mesh.colors32[0].r != 255) colors += "(" + mesh.colors32[0] + ")";
                    if (r.sharedMaterials != null) foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
                    sb.AppendLine("  R " + Path(r.transform, go.transform) + " " + r.GetType().Name + " " + colors + " mats=" + r.sharedMaterials.Length + " active=" + r.gameObject.activeSelf);
                    if (sb.Length > 400000) break;
                }
                foreach (var m in mats)
                    sb.AppendLine("  M " + m.name + " shader=" + m.shader.name + " color=" + (m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor").ToString() : "?"));
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (o is AnimationClip c && !c.name.StartsWith("__preview"))
                    {
                        if (c.name.Contains("_RB_FX") || c.name.Contains("Fireball_") && c.name.Contains("_RB_")) continue;
                        sb.AppendLine("  CLIP " + c.name + " len=" + c.length.ToString("0.###") + " legacy=" + c.legacy + " loop=" + c.isLooping + " fps=" + c.frameRate + " bindings=" + AnimationUtility.GetCurveBindings(c).Length);
                    }
                }
                // Squelette / nœuds : liste des 60 premiers noms
                int n = 0;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (n++ < 70) sb.AppendLine("  N " + Path(t, go.transform) + (t.gameObject.activeSelf ? "" : " [inactive]"));
                }
            }
            string outPath = "Logs/rb_asset_report.txt";
            Directory.CreateDirectory("Logs");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("RBAssetReport écrit : " + outPath + " (" + sb.Length + " car.)");
        }

        static string Path(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
