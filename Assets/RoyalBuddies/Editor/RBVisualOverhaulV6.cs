#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RoyalBuddies.EditorTools
{
    // Visual-only pass. Adds no collider, nav obstacle, runtime behaviour or gameplay light.
    
    public static class RBVisualOverhaulV6
    {
        const string ScenePath="Assets/RoyalBuddies/Scenes/RB_Battle.unity";
        const string RootName="V6_VisualOverhaul";
        const string Key="RoyalBuddies.VisualOverhaulV67CleanHarborCliff.Done";
        static readonly string Fjord="Assets/RoyalBuddies/Environment/Fjord/Materials/";
        
        static void Once(){if(SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,true);Apply();}
        [MenuItem("Royal Buddies/Fjord/Apply clean curved harbour cliff V6.7")]
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene().path==ScenePath?SceneManager.GetActiveScene():EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var arena=GameObject.Find("RB_Arena");var env=GameObject.Find("VikingEnvironment");
            if(!arena||!env){Debug.LogError("[RoyalBuddies] V6: RB_Arena/VikingEnvironment missing");return;}
            var meadow=AssetDatabase.LoadAssetAtPath<Material>(Fjord+"Fjord_527048.mat");
            var rock=AssetDatabase.LoadAssetAtPath<Material>(Fjord+"Fjord_637275.mat");
            var sea=AssetDatabase.LoadAssetAtPath<Material>(Fjord+"Fjord_Sea.mat");
            if(!meadow||!rock||!sea){Debug.LogError("[RoyalBuddies] V6: fjord materials missing");return;}

            // Preserve gameplay geometry; visual surfaces alone share the continuous world-space grass/water shaders.
            foreach(var r in arena.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(r.name=="TurfEnemy"||r.name=="TurfPlayer") r.sharedMaterial=meadow;
                if(r.name=="Water") r.sharedMaterial=sea;
            }
            // Remove obsolete decorative stream remains, never gameplay bridges or harbour piers.
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).ToArray())
            {
                if(!t)continue;
                if(t.name.StartsWith("Brook_"))Object.DestroyImmediate(t.gameObject);
                else if(t.name=="town_planks") {var p=t.position;if(Mathf.Abs(Mathf.Abs(p.x)-5.6f)<.4f&&Mathf.Abs(Mathf.Abs(p.z)-6.2f)<.5f)Object.DestroyImmediate(t.gameObject);}
            }

            // V6.2: normalize only the pale green Fjord surface materials outside the gameplay arena.
            // This targets the light grass caps visible on coastal rocks without touching Viking trees/props.
            foreach(var r in env.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats=r.sharedMaterials; bool changed=false;
                for(int mi=0;mi<mats.Length;mi++)
                {
                    var m=mats[mi]; if(!m||m==meadow) continue;
                    string mp=AssetDatabase.GetAssetPath(m);
                    if(!mp.StartsWith(Fjord,StringComparison.Ordinal)) continue;
                    Color c=Color.gray;
                    if(m.HasProperty("_BaseColor")) c=m.GetColor("_BaseColor"); else if(m.HasProperty("_Color")) c=m.color;
                    // Deliberately narrow: only conspicuously light green/grey-green ground caps.
                    if(c.g>.48f && c.g>c.r+.035f && c.g>c.b+.02f) { mats[mi]=meadow; changed=true; }
                }
                if(changed) r.sharedMaterials=mats;
            }

            var old=env.transform.Find(RootName);if(old)Object.DestroyImmediate(old.gameObject);
            var old4=env.transform.Find("V4_CoastPolish");if(old4)Object.DestroyImmediate(old4.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(env.transform,false);root.gameObject.layer=2;
            // V6.1: remove the failed harbour mountain entirely. The original V5 harbour,
            // pier, boats and houses remain untouched and fully visible.
            var islands=Group(root,"Coastal islets V6.1");
            Vector3[] pts={new(-15.9f,-2.0f,-20),new(-16.5f,-2.1f,-15),new(-16.3f,-2.1f,-10),new(16.0f,-2.0f,-19),new(16.4f,-2.1f,-13),new(16.2f,-2.0f,-7),new(16.5f,-2.0f,9),new(16.1f,-2.0f,15),new(15.7f,-1.9f,21),new(-15.8f,-2.0f,8),new(-16.1f,-2.0f,12)};
            for(int i=0;i<pts.Length;i++)
            {
                float h=1.2f+(i%3)*.35f;
                Rock(islands,pts[i],h,i*37f);
                // Small cap uses exactly the same meadow material as the main island.
                var capPos=pts[i]+new Vector3(0,h*.47f,0);
                Lip(islands,capPos,new Vector3(.9f+(i%2)*.22f,.055f,.8f+((i+1)%2)*.2f),meadow,i*29f);
                // One or two irregular Nordic pines; never aligned, never on gameplay.
                string tree=(i%3==0)?"nature_tree_pineTallA":((i%3==1)?"nature_tree_pineDefaultB":"nature_tree_pineRoundA");
                AddProp(islands,tree,pts[i].x+((i%2==0)?.28f:-.22f),capPos.y+.08f,pts[i].z+((i%3)-1)*.22f,.95f+(i%4)*.13f,i*61f);
                if(i%4==1) AddProp(islands,"nature_grass_large",pts[i].x-.25f,capPos.y+.05f,pts[i].z+.28f,.28f,i*43f);
            }

            // V6.5: one independent continuous harbour cliff patch only.
            // This replaces the failed V6.3/V6.4 strip made from separate wall segments.
            // The existing island contour mesh is never modified.
            Vector3[] coastLine={
                // Dense, gently curved spline-like contour: one continuous cliff, no repeated blocks.
                new(-14.78f,-0.035f,15.55f), new(-14.84f,-0.025f,16.55f),
                new(-14.91f,-0.020f,17.55f), new(-14.97f,-0.018f,18.55f),
                new(-15.03f,-0.015f,19.55f), new(-15.08f,-0.012f,20.55f),
                new(-15.12f,-0.010f,21.55f), new(-15.14f,-0.010f,22.55f),
                new(-15.15f,-0.012f,23.55f), new(-15.13f,-0.015f,24.55f),
                new(-15.09f,-0.018f,25.55f), new(-15.02f,-0.022f,26.55f),
                new(-14.91f,-0.030f,27.55f), new(-14.78f,-0.040f,28.10f)
            };
            CreateHarborCliffPatch(root, coastLine, -3.05f, rock);

            var blend=Group(root,"Grass edge breakup");
            // Very sparse grass/bush accents cross the visual seam without forming a hiding wall.
            AddProp(blend,"nature_grass_large",-11.7f,.02f,-17,0.55f,20);AddProp(blend,"nature_grass_large",11.7f,.02f,-13,0.5f,110);
            AddProp(blend,"nature_plant_bush",-12.0f,.02f,10.5f,0.55f,15);AddProp(blend,"nature_grass_large",12.0f,.02f,16.0f,0.48f,70);
            AddProp(blend,"nature_grass_large",-11.8f,.02f,15.0f,0.45f,145);AddProp(blend,"nature_plant_bush",11.9f,.02f,8.5f,0.45f,230);

            var foam=Group(root,"Shore foam accents");
            var foamMat=MakeFoam();
            // Short broken strips around harbour rock base, slightly above sea plane.
            Foam(foam,new Vector3(-16.1f,-2.25f,18.5f),new Vector3(.18f,.02f,2.0f),foamMat,12);
            Foam(foam,new Vector3(-16.5f,-2.25f,24.5f),new Vector3(.16f,.02f,2.4f),foamMat,-8);
            Foam(foam,new Vector3(-15.6f,-2.25f,27.2f),new Vector3(.14f,.02f,1.5f),foamMat,20);

            foreach(var c in root.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)){r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("[RoyalBuddies] V6.7 clean curved HarborCliffPatch applied: failed harbour mountain removed, original port preserved, coastal islets use main-island grass and irregular Nordic pines. Gameplay geometry/colliders/nav/camera/UI untouched.");
        }
        static Transform Group(Transform p,string n){var g=new GameObject(n).transform;g.SetParent(p,false);return g;}
        static void Rock(Transform p,Vector3 pos,float h,float yaw)
        {
            string[] names={"nature_rock_largeA","nature_rock_largeB","nature_rock_tallB"};
            var asset=names.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RoyalBuddies/Models/Viking/"+n+".glb")).FirstOrDefault(a=>a);
            if(!asset)return;var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,p);g.name="Layered coastal rock";g.transform.position=pos;g.transform.rotation=Quaternion.Euler(0,yaw,0);
            var rs=g.GetComponentsInChildren<Renderer>();if(rs.Length>0){Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);float s=h/Mathf.Max(.01f,b.size.y);g.transform.localScale=Vector3.one*s;}
        }
        static void AddProp(Transform p,string name,float x,float y,float z,float h,float yaw)
        {
            var a=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RoyalBuddies/Models/Viking/"+name+".glb");if(!a)return;var g=(GameObject)PrefabUtility.InstantiatePrefab(a,p);g.name="Coastal "+name;g.transform.position=new Vector3(x,y,z);g.transform.rotation=Quaternion.Euler(0,yaw,0);
            var rs=g.GetComponentsInChildren<Renderer>();if(rs.Length>0){Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);g.transform.localScale=Vector3.one*(h/Mathf.Max(.01f,b.size.y));}
        }
        static void Lip(Transform p,Vector3 pos,Vector3 scale,Material m,float yaw){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Grassy cliff lip";g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.transform.localRotation=Quaternion.Euler(0,yaw,0);g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());}
        static void CliffWall(Transform p,Vector3 a,Vector3 b,float bottomY,Material m,int variant)
        {
            // A faceted trapezoidal prism generated in the scene. Top edge follows the terrain; all volume is below it.
            Vector3 dir=(b-a).normalized; Vector3 outward=new Vector3(-dir.z,0,dir.x);
            float topInset=.08f, bottomOut=.72f + (variant%2)*.16f;
            Vector3 at=a+outward*topInset, bt=b+outward*topInset;
            Vector3 ab=a+outward*bottomOut; ab.y=bottomY;
            Vector3 bb=b+outward*(bottomOut+.10f); bb.y=bottomY-.08f*(variant%3);
            // inner lower edge gives believable thickness below the island without covering the plateau.
            Vector3 ai=a-outward*.55f; ai.y=bottomY; Vector3 bi=b-outward*.55f; bi.y=bottomY;
            var mesh=new Mesh(); mesh.name="Generated continuous harbour cliff";
            mesh.vertices=new[]{at,bt,bb,ab,a,b,bi,ai};
            mesh.triangles=new[]{0,1,2,0,2,3, 4,7,6,4,6,5, 0,3,7,0,7,4, 1,5,6,1,6,2, 3,2,6,3,6,7, 0,4,5,0,5,1};
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var g=new GameObject("Continuous faceted cliff segment"); g.transform.SetParent(p,false);
            var mf=g.AddComponent<MeshFilter>(); mf.sharedMesh=mesh; var mr=g.AddComponent<MeshRenderer>(); mr.sharedMaterial=m;
        }
        static void CreateHarborCliffPatch(Transform parent, Vector3[] line, float bottomY, Material material)
        {
            // V6.7: ONE continuous curved mesh. Dense contour points make the harbour transition read as
            // a genuine continuation of the island rim rather than duplicated panels or rock blocks.
            int n=line.Length;
            var v=new Vector3[n*4];
            for(int i=0;i<n;i++)
            {
                Vector3 prev=line[Mathf.Max(0,i-1)], next=line[Mathf.Min(n-1,i+1)];
                Vector3 dir=(next-prev).normalized;
                Vector3 outward=new Vector3(-dir.z,0,dir.x).normalized;
                float t=i/(float)(n-1);
                // Small deterministic geological variation only at the lower face; top stays clean under turf.
                float wave=.055f*Mathf.Sin(i*1.71f)+.025f*Mathf.Sin(i*.63f);
                float flare=.72f + .10f*Mathf.Sin(t*Mathf.PI) + wave;
                float depth=bottomY - .05f*Mathf.Sin(i*1.13f);
                Vector3 topOuter=line[i]+outward*.055f;
                Vector3 topInner=line[i]-outward*.34f;
                Vector3 botOuter=line[i]+outward*flare; botOuter.y=depth;
                Vector3 botInner=line[i]-outward*.48f; botInner.y=depth-.035f;
                v[i*4+0]=topOuter; v[i*4+1]=topInner; v[i*4+2]=botOuter; v[i*4+3]=botInner;
            }
            var tris=new System.Collections.Generic.List<int>((n-1)*24+12);
            for(int i=0;i<n-1;i++)
            {
                int a=i*4,b=(i+1)*4;
                tris.AddRange(new[]{a,b,b+2, a,b+2,a+2});          // visible sea face
                tris.AddRange(new[]{a+1,a+3,b+3, a+1,b+3,b+1});  // hidden inner face
                tris.AddRange(new[]{a+1,b+1,b, a+1,b,a});        // top lip under grass
                tris.AddRange(new[]{a+2,b+2,b+3, a+2,b+3,a+3});  // bottom thickness
            }
            tris.AddRange(new[]{0,2,3,0,3,1});
            int e=(n-1)*4; tris.AddRange(new[]{e,e+1,e+3,e,e+3,e+2});
            var mesh=new Mesh { name="HarborCliffPatch_CleanCurved_Mesh" };
            mesh.vertices=v; mesh.triangles=tris.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go=new GameObject("HarborCliffPatch_CleanCurved");
            go.transform.SetParent(parent,false); go.layer=2;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mr=go.AddComponent<MeshRenderer>(); mr.sharedMaterial=material;
            mr.shadowCastingMode=ShadowCastingMode.On; mr.receiveShadows=true;
        }

        static Material MakeFoam(){string path=Fjord+"Fjord_V6_Foam.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",new Color(.62f,.78f,.76f,.72f));m.SetFloat("_Smoothness",.35f);AssetDatabase.CreateAsset(m,path);return m;}
        static void Foam(Transform p,Vector3 pos,Vector3 scale,Material m,float yaw){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Broken shore foam";g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.transform.localRotation=Quaternion.Euler(0,yaw,0);g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());}
    }
}
#endif

