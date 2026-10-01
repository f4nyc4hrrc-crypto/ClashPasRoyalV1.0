#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoyalBuddies.EditorTools
{
    
    public static class RBHarmoniseArenaV3
    {
        const string ScenePath = "Assets/RoyalBuddies/Scenes/RB_Battle.unity";
        const string MeadowMatPath = "Assets/RoyalBuddies/Environment/Fjord/Materials/Fjord_527048.mat";
        const string DoneKey = "RoyalBuddies.HarmoniseArenaV3.Done";

        

        [MenuItem("Royal Buddies/Fjord/Apply arena harmonisation V3")]
        public static void ApplyManually() { Apply(true); }

        static void ApplyOnce()
        {
            if (SessionState.GetBool(DoneKey, false)) return;
            SessionState.SetBool(DoneKey, true);
            Apply(false);
        }

        static void Apply(bool force)
        {
            var meadow = AssetDatabase.LoadAssetAtPath<Material>(MeadowMatPath);
            if (!meadow) { Debug.LogWarning("[RoyalBuddies] V3: matériau Fjord_527048 introuvable."); return; }

            var current = SceneManager.GetActiveScene();
            bool alreadyBattle = current.IsValid() && current.path == ScenePath;
            var scene = alreadyBattle ? current : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var arena = GameObject.Find("RB_Arena");
            if (!arena) { Debug.LogWarning("[RoyalBuddies] V3: RB_Arena introuvable."); return; }

            int turf = 0, brooks = 0, decorativeBridges = 0;
            foreach (var r in arena.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.gameObject.name == "TurfEnemy" || r.gameObject.name == "TurfPlayer")
                {
                    r.sharedMaterial = meadow;
                    EditorUtility.SetDirty(r);
                    turf++;
                }
            }

            // Supprime uniquement les ruisseaux décoratifs. La grande rivière centrale de RB_Arena reste intacte.
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (!t) continue;
                if (t.name.StartsWith("Brook_")) { Object.DestroyImmediate(t.gameObject); brooks++; }
            }

            // Supprime d'éventuels petits ponts décoratifs hors RB_Arena, jamais les deux ponts de gameplay.
            all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (!t || !t.name.ToLowerInvariant().Contains("bridge")) continue;
                if (t.IsChildOf(arena.transform)) continue;
                Object.DestroyImmediate(t.gameObject); decorativeBridges++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RoyalBuddies] Harmonisation V3 appliquée : {turf} sols de combat harmonisés, {brooks} ruisseaux décoratifs supprimés, {decorativeBridges} petits ponts décoratifs supprimés. Rivière et ponts centraux conservés.");
        }
    }
}
#endif

