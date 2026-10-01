#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoyalBuddies.EditorTools
{
    
    public static class RBVisualPolishV4
    {
        const string ScenePath="Assets/RoyalBuddies/Scenes/RB_Battle.unity";
        const string MeadowPath="Assets/RoyalBuddies/Environment/Fjord/Materials/Fjord_527048.mat";
        const string SeaPath="Assets/RoyalBuddies/Environment/Fjord/Materials/Fjord_Sea.mat";
        const string RockPath="Assets/RoyalBuddies/Environment/Fjord/Materials/Fjord_637275.mat";
        const string Key="RoyalBuddies.VisualPolishV4.Done";
        
        static void Once(){if(SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,true);Apply();}
        [MenuItem("Royal Buddies/Fjord/Apply visual polish V4")]
        public static void Apply()
        {
            var meadow=AssetDatabase.LoadAssetAtPath<Material>(MeadowPath);
            var sea=AssetDatabase.LoadAssetAtPath<Material>(SeaPath);
            var rock=AssetDatabase.LoadAssetAtPath<Material>(RockPath);
            if(!meadow||!sea||!rock){Debug.LogError("[RoyalBuddies] V4 materials missing");return;}
            var scene=SceneManager.GetActiveScene().path==ScenePath?SceneManager.GetActiveScene():EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var arena=GameObject.Find("RB_Arena"); var env=GameObject.Find("VikingEnvironment");
            if(!arena||!env){Debug.LogError("[RoyalBuddies] V4 arena/environment missing");return;}

            // One grass material on both playable halves. This changes visuals only.
            foreach(var r in arena.GetComponentsInChildren<MeshRenderer>(true))
                if(r.name=="TurfEnemy"||r.name=="TurfPlayer"){r.sharedMaterial=meadow;EditorUtility.SetDirty(r);}

            // Remove the four obsolete stream crossings only. Harbour docks and gameplay bridges are untouched.
            int crossings=0;
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(!t||t.name!="town_planks")continue;
                Vector3 p=t.position;
                if(Mathf.Abs(Mathf.Abs(p.x)-5.6f)<.35f && Mathf.Abs(Mathf.Abs(p.z)-6.2f)<.45f){Object.DestroyImmediate(t.gameObject);crossings++;}
            }
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(t&&t.name.StartsWith("Brook_"))Object.DestroyImmediate(t.gameObject);

            // River and sea now share the same water material/palette.
            foreach(var r in arena.GetComponentsInChildren<MeshRenderer>(true)) if(r.name=="Water"){r.sharedMaterial=sea;EditorUtility.SetDirty(r);}

            // Non-gameplay visual cliff infill beside the west harbour. No collider/nav component.
            var old=env.transform.Find("V4_CoastPolish"); if(old)Object.DestroyImmediate(old.gameObject);
            var polish=new GameObject("V4_CoastPolish");polish.transform.SetParent(env.transform,false);polish.layer=2;
            AddCliff(polish.transform,new Vector3(-12.05f,-1.20f,21.8f),new Vector3(1.35f,2.35f,5.0f),new Vector3(0,8,7),rock);
            AddCliff(polish.transform,new Vector3(-13.05f,-1.55f,24.5f),new Vector3(1.65f,2.9f,3.3f),new Vector3(0,-12,-5),rock);
            AddCliff(polish.transform,new Vector3(-12.55f,-1.55f,18.9f),new Vector3(1.25f,2.8f,2.5f),new Vector3(0,16,4),rock);

            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("[RoyalBuddies] V4 applied: unified grass/water, removed "+crossings+" obsolete stream crossings, added harbour cliff infill. Gameplay bridges/colliders/navigation untouched.");
        }
        static void AddCliff(Transform p,Vector3 pos,Vector3 scale,Vector3 rot,Material mat)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Layered cliff infill";g.layer=2;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.transform.localEulerAngles=rot;
            var c=g.GetComponent<Collider>();if(c)Object.DestroyImmediate(c);var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
#endif

