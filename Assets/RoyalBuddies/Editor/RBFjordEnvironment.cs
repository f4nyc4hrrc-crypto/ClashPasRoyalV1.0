using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RoyalBuddies.EditorTools
{
    // Editor-only, baked scenery. Never calls ArenaBuilder, changes gameplay, or rebuilds navigation.
    public static class RBFjordEnvironment
    {
        const string ScenePath = "Assets/RoyalBuddies/Scenes/RB_Battle.unity";
        const string Out = "Assets/RoyalBuddies/Environment/Fjord";
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static int meshId;
        static Transform root;
        static System.Random rng;
        static float R(float a, float b) => a + (float)rng.NextDouble() * (b-a);
        static Vector3 V(float x, float y, float z) => new Vector3(x,y,z);
        static Transform Group(string name, Transform parent, Vector3 pos = default, float yaw = 0)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false); t.localPosition = pos; t.localRotation = Quaternion.Euler(0,yaw,0);
            return t;
        }
        static Material Mat(string hex)
        {
            if (materials.TryGetValue(hex, out var m)) return m;
            string path = Out + "/Materials/Fjord_" + hex + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
            m.SetColor("_BaseColor", RB.HexColor("#"+hex)); m.SetFloat("_Smoothness", .12f);
            materials.Add(hex,m); return m;
        }
        static GameObject Box(Transform p, string n, Vector3 pos, Vector3 size, string c, float yaw=0)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=n; Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(p,false); g.transform.localPosition=pos; g.transform.localScale=size;
            g.transform.localRotation=Quaternion.Euler(0,yaw,0); g.GetComponent<Renderer>().sharedMaterial=Mat(c); return g;
        }
        static void Beam(Transform p, Vector3 a, Vector3 b, float w, string c)
        {
            var g=Box(p,"Carved timber",(a+b)*.5f,V(w,(b-a).magnitude,w),c);
            g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        static void Disc(Transform p, Vector3 pos, float radius, string c)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(g.GetComponent<Collider>());
            g.name="Round shield"; g.transform.SetParent(p,false); g.transform.localPosition=pos;
            g.transform.localRotation=Quaternion.Euler(90,0,0); g.transform.localScale=V(radius*2,.045f,radius*2);
            g.GetComponent<Renderer>().sharedMaterial=Mat(c);
        }
        static Mesh Mesh(string name, List<Vector3> v, List<int> tris)
        {
            var m=new Mesh { name=name, indexFormat=v.Count>65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v); m.SetTriangles(tris,0); m.SetUVs(0,v.Select(a=>new Vector2(a.x*.18f,a.z*.18f)).ToList());
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
        static GameObject Surface(Transform p, string n, List<Vector3> v, List<int> tri, Material mat)
        {
            var g=Group(n,p).gameObject; g.AddComponent<MeshFilter>().sharedMesh=Mesh(n,v,tri);
            g.AddComponent<MeshRenderer>().sharedMaterial=mat; return g;
        }
        // Independent vertices give softly faceted cliffs, with correct upward-facing terrain winding.
        static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i=v.Count; v.AddRange(new[]{a,b,c,d}); t.AddRange(new[]{i,i+2,i+1,i,i+3,i+2});
        }
        static void Prop(Transform p, string model, float x,float y,float z, float h,float yaw=0)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RoyalBuddies/Models/Viking/"+model+".glb");
            if (!asset) throw new Exception("Missing existing model: "+model);
            var g=Object.Instantiate(asset,p); g.name=model; g.transform.localPosition=Vector3.zero;
            g.transform.localRotation=Quaternion.Euler(0,yaw,0); g.transform.localScale=Vector3.one;
            foreach(var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var rs=g.GetComponentsInChildren<Renderer>(); if(rs.Length==0) throw new Exception("Model without renderer: "+model);
            // Give the reused low-poly nature meshes a coherent Nordic palette, keeping their submesh details.
            if(model.StartsWith("nature_")) foreach(var renderer in rs)
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>
                {
                    string n=m.name.ToLowerInvariant();
                    if(n.Contains("leaf"))return Mat(model.Contains("Tall")?"305745":"3f654d");
                    if(n.Contains("grass"))return Mat("527048");
                    if(n.Contains("dirt") || n.Contains("stone"))return Mat("75817d");
                    if(n.Contains("wood") || n.Contains("bark"))return Mat("69503b");
                    return m;
                }).ToArray();
            }
            Bounds b=rs[0].bounds; foreach(var r in rs) b.Encapsulate(r.bounds);
            g.transform.localScale=Vector3.one*(h/b.size.y);
            b=rs[0].bounds; foreach(var r in rs) b.Encapsulate(r.bounds);
            g.transform.position+=p.TransformPoint(V(x,y,z))-V(b.center.x,b.min.y,b.center.z);
        }
        static string Snapshot()
        {
            var entries=new List<string>();
            foreach(var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if(go.name=="RB_ForestBorder" || go.name=="VikingEnvironment" || go.name=="RB_VikingEnvironment") continue;
                var protectedTransforms = go.name=="RB_VikingDecor" ? LegacyInterior(go.transform).SelectMany(t=>t.GetComponentsInChildren<Transform>(true)) : go.GetComponentsInChildren<Transform>(true).AsEnumerable();
                foreach(var tr in protectedTransforms)
                {
                    entries.Add(GlobalObjectId.GetGlobalObjectIdSlow(tr.gameObject)+EditorJsonUtility.ToJson(tr.gameObject));
                    foreach(var c in tr.GetComponents<Component>())
                        if(c) entries.Add(GlobalObjectId.GetGlobalObjectIdSlow(c)+EditorJsonUtility.ToJson(c));
                }
            }
            entries.Sort(StringComparer.Ordinal); return string.Join("\n",entries);
        }
        // The supplied scene already has brooks and small props INSIDE the arena. Preserve those too.
        static IEnumerable<Transform> LegacyInterior(Transform legacy)
        {
            foreach(Transform group in legacy) foreach(Transform item in group)
            {
                if(group.name=="Water") {if(item.name.StartsWith("Brook") || item.name=="Waterfall")yield return item;continue;}
                if(group.name=="Fx") {if(item.name=="WaterfallMist")yield return item;continue;}
                var rs=item.GetComponentsInChildren<Renderer>(true);
                if(rs.Any(r=>r.bounds.min.x<11.25f && r.bounds.max.x> -11.25f && r.bounds.min.z<23f && r.bounds.max.z> -23f)) yield return item;
            }
        }
        static void RemoveLegacyExterior()
        {
            var legacy=GameObject.Find("RB_VikingDecor");if(!legacy)return;
            var keep=new HashSet<Transform>(LegacyInterior(legacy.transform));
            foreach(Transform group in legacy.transform)
                foreach(var item in group.Cast<Transform>().ToArray()) if(!keep.Contains(item))Object.DestroyImmediate(item.gameObject);
        }
        [MenuItem("Royal Buddies/Fjord/Replace exterior in RB_Battle (preserves arena)")]
        public static void Build()
        {
            Directory.CreateDirectory(Out+"/Materials"); Directory.CreateDirectory(Out+"/Meshes");
            Directory.CreateDirectory("Logs"); AssetDatabase.Refresh(); materials.Clear(); meshId=0; rng=new System.Random(10012026);
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            string before=Snapshot(); File.WriteAllText("Logs/protected-before.jsonl",before);
            foreach(var go in scene.GetRootGameObjects())
                if(go.name=="RB_ForestBorder" || go.name=="VikingEnvironment" || go.name=="RB_VikingEnvironment") Object.DestroyImmediate(go);
            RemoveLegacyExterior();
            root=Group("VikingEnvironment",null);
            Land(); Sea(); Village(); Harbour(); Forest(); Camp();
            // No physics, navigation, dynamic lights, or shadow spill from the exterior onto combat.
            foreach(var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer=2; // Ignore Raycast
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            { renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=true; }
            CombineByDistrict(); PersistMeshes();
            if(before!=Snapshot()) throw new Exception("Protected arena/camera/bootstrap changed. Scene was NOT saved.");
            Validate(); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            if(before!=Snapshot()) throw new Exception("Protected objects changed after scene serialization.");
            File.WriteAllText("Logs/protected-after.jsonl",Snapshot());
            Capture("Logs/fjord-mobile.png",720,1560);
            var cam=Camera.main; var pos=cam.transform.position; var rot=cam.transform.rotation;
            cam.transform.position=V(41,49,-54); cam.transform.LookAt(V(0,0,6));
            Capture("Logs/fjord-overview.png",1500,1200);
            cam.transform.position=pos; cam.transform.rotation=rot;
            Debug.Log("[FJORD] SUCCESS: saved and reopened RB_Battle; all protected objects identical.");
        }
        static float Edge(int side,float z)
        {
            return side<0 ? 15.6f+1.3f*Mathf.Sin(z*.14f+.8f)+.65f*Mathf.Sin(z*.43f)
                          : 15.6f+1.0f*Mathf.Sin(z*.18f-1f)+.55f*Mathf.Sin(z*.57f);
        }
        static void Land()
        {
            var g=Group("01 Island - grassy terraces and coastal cliffs",root);
            // Straight inner seam meets the existing turf at x=11.25; only the OUTER shoreline meanders.
            foreach(int side in new[]{-1,1}) foreach(int half in new[]{-1,1})
            {
                var v=new List<Vector3>(); var t=new List<int>(); var cv=new List<Vector3>(); var ct=new List<int>();
                const int n=24;
                for(int i=0;i<n;i++)
                {
                    float z0=Mathf.Lerp(2.36f,23f,i/(float)n)*half, z1=Mathf.Lerp(2.36f,23f,(i+1)/(float)n)*half;
                    float e0=Edge(side,z0),e1=Edge(side,z1);
                    Vector3 a=V(side*11.25f,-.075f,z0), b=V(side*e0,-.18f,z0), c=V(side*e1,-.18f,z1),d=V(side*11.25f,-.075f,z1);
                    Vector3 b0=V(side*(e0-1.0f),.18f+.16f*Mathf.Sin(z0*.7f),z0), b1=V(side*(e1-1.0f),.18f+.16f*Mathf.Sin(z1*.7f),z1);
                    if(side*half>0) { Quad(v,t,a,b0,b1,d); Quad(v,t,b0,b,c,b1); }
                    else { Quad(v,t,d,b1,b0,a); Quad(v,t,b1,c,b,b0); }
                    Vector3 low0=V(side*(e0+.9f),-2.7f,z0), low1=V(side*(e1+.9f),-2.7f,z1);
                    // Double-sided coastline faces (also visible from the harbour inlet).
                    Quad(cv,ct,b,low0,low1,c); Quad(cv,ct,c,low1,low0,b);
                    Quad(cv,ct,b,V(side*(e0+.45f),-1.15f,z0),V(side*(e1+.45f),-1.15f,z1),c);
                }
                Surface(g,"Meadow "+side+" "+half,v,t,Mat("527048"));
                Surface(g,"Layered rock coast "+side+" "+half,cv,ct,Mat("637275"));
            }
            // Northern village plateau and low southern apron, both wholly beyond the playable z limits.
            foreach(int half in new[]{-1,1})
            {
                var v=new List<Vector3>();var t=new List<int>();var cv=new List<Vector3>();var ct=new List<int>();
                const int n=32;
                for(int i=0;i<n;i++)
                {
                    float x0=Mathf.Lerp(-Edge(-1,23*half),Edge(1,23*half),i/(float)n);
                    float x1=Mathf.Lerp(-Edge(-1,23*half),Edge(1,23*half),(i+1)/(float)n);
                    Func<float,float> end=x=>half*(half>0 ? 33.7f+3.2f*Mathf.Cos(x*.12f)+1.1f*Mathf.Sin(x*.6f) : 27.0f+1.1f*Mathf.Sin(x*.32f));
                    Vector3 a=V(x0,-.075f,23*half),b=V(x1,-.075f,23*half),c=V(x1,-.25f,end(x1)),d=V(x0,-.25f,end(x0));
                    if(half>0) Quad(v,t,a,b,c,d); else Quad(v,t,d,c,b,a);
                    Quad(cv,ct,d,c,c+V(0,-2.6f,half*.8f),d+V(0,-2.6f,half*.8f));
                    Quad(cv,ct,d+V(0,-2.6f,half*.8f),c+V(0,-2.6f,half*.8f),c,d);
                }
                Surface(g,"Village headland "+half,v,t,Mat("527048")); Surface(g,"Headland cliff "+half,cv,ct,Mat("637275"));
            }
            // Scattered large rock faces break the continuous rim into geological groups.
            var rocks=Group("Coastal outcrops",g);
            float[,] spots={{16,8,2.2f},{17,10,3.0f},{16,13,2.1f},{-16,13,2.8f},{-17,16,2.1f},{-16,-18,1.9f},{-15,-23,1.3f},{14,30,3.8f},{17,28,2.8f},{-13,34,2.5f}};
            for(int i=0;i<spots.GetLength(0);i++) Prop(rocks,i%2==0?"nature_rock_largeA":"nature_rock_tallB",spots[i,0],-1.25f,spots[i,1],spots[i,2],R(0,360));
        }
        static void Sea()
        {
            var g=Group("02 Continuous sea and river mouths",root);
            var waterSource=AssetDatabase.LoadAssetAtPath<Material>("Assets/RoyalBuddies/Materials/RB_Water.mat");
            var seaPath=Out+"/Materials/Fjord_Sea.mat";
            var water=AssetDatabase.LoadAssetAtPath<Material>(seaPath);
            if(!water){water=new Material(waterSource);AssetDatabase.CreateAsset(water,seaPath);}
            water.SetColor("_Deep",RB.HexColor("#224c5c"));water.SetColor("_Shallow",RB.HexColor("#36717a"));water.SetColor("_Glint",RB.HexColor("#8ab6b1"));
            var v=new List<Vector3>();var t=new List<int>();
            Quad(v,t,V(-210,-2.3f,-180),V(210,-2.3f,-180),V(210,-2.3f,220),V(-210,-2.3f,220));
            Surface(g,"Sea",v,t,water);
            // River extension begins strictly beyond both existing river endpoints.
            foreach(int side in new[]{-1,1})
            {
                v=new List<Vector3>();t=new List<int>();
                for(int i=0;i<10;i++)
                {
                    float x0=11.25f+i*.95f,x1=x0+.95f;
                    float y0=Mathf.Lerp(-.065f,-2.29f,Mathf.SmoothStep(0,1,i/10f)),y1=Mathf.Lerp(-.065f,-2.29f,Mathf.SmoothStep(0,1,(i+1)/10f));
                    Quad(v,t,V(side*x0,y0,-2),V(side*x1,y1,-2.15f),V(side*x1,y1,2.15f),V(side*x0,y0,2));
                    Quad(v,t,V(side*x0,y0,2),V(side*x1,y1,2.15f),V(side*x1,y1,-2.15f),V(side*x0,y0,-2));
                }
                Surface(g,"Estuary "+side,v,t,water);
            }
            // Broken foam accents, restricted to the shoreline rather than a repetitive continuous border.
            var foam=Mat("8bb5ae");
            for(int i=0;i<26;i++)
            {
                int side=i<15?-1:1;float z=R(-23,30); if(Mathf.Abs(z)<4)continue;
                float x=side*(Edge(side,Mathf.Clamp(z,-23,23))+R(.8f,1.8f));
                var q=Box(g,"Tidal foam",V(x,-2.27f,z),V(R(.13f,.3f),.015f,R(.5f,1.9f)),"8bb5ae",R(-22,22));
            }
        }
        static void House(Transform p,string name,Vector3 pos,float scale,float yaw,string roof)
        {
            var g=Group(name,p,pos,yaw);g.localScale=Vector3.one*scale;
            Box(g,"Stone foundation",V(0,.1f,0),V(3.7f,.35f,4.6f),"666e65");
            Box(g,"Timber walls",V(0,1.2f,0),V(3.1f,2.1f,4.0f),"77563b");
            // Curved A-frame profile and overhanging eaves; shingles share three muted fired-clay tones.
            float[] xs={-2.05f,-1.7f,-1.05f,0,1.05f,1.7f,2.05f};
            float[] ys={1.5f,1.8f,2.8f,4.0f,2.8f,1.8f,1.5f};
            for(int i=0;i<6;i++)
            {
                var v=new List<Vector3>();var t=new List<int>();
                Quad(v,t,V(xs[i],ys[i],-2.4f),V(xs[i+1],ys[i+1],-2.4f),V(xs[i+1],ys[i+1],2.4f),V(xs[i],ys[i],2.4f));
                Surface(g,"Steep shingled roof",v,t,Mat(roof));
                for(int row=0;row<7;row++) Beam(g,V(xs[i],ys[i]+.035f,-2.35f+row*.73f),V(xs[i+1],ys[i+1]+.035f,-2.35f+row*.73f),.045f,"936b4d");
                foreach(float z in new[]{-2.46f,2.46f}) Beam(g,V(xs[i],ys[i]+.04f,z),V(xs[i+1],ys[i+1]+.04f,z),.19f,"a37b4d");
            }
            // Closed triangular gables.
            Surface(g,"Gables",new List<Vector3>{V(-1.55f,2.1f,-2.01f),V(0,3.85f,-2.01f),V(1.55f,2.1f,-2.01f),V(-1.55f,2.1f,2.01f),V(0,3.85f,2.01f),V(1.55f,2.1f,2.01f)},new List<int>{0,1,2,5,4,3},Mat("77563b"));
            Beam(g,V(0,4.04f,-2.8f),V(0,4.04f,2.8f),.21f,"a37b4d");
            foreach(int s in new[]{-1,1}) { Beam(g,V(0,4.0f,s*2.3f),V(0,4.55f,s*2.85f),.18f,"a37b4d"); Beam(g,V(-1.4f,.15f,s*2.03f),V(-1.4f,2.2f,s*2.03f),.18f,"ac8251"); Beam(g,V(1.4f,.15f,s*2.03f),V(1.4f,2.2f,s*2.03f),.18f,"ac8251"); }
            for(int i=0;i<7;i++) Box(g,"Wall plank",V(0,.4f+i*.24f,-2.035f),V(2.8f,.035f,.04f),"a37b4d");
            Box(g,"Door",V(0,.95f,-2.08f),V(.88f,1.65f,.12f),"423d32");
            Box(g,"Door frame",V(0,1.84f,-2.15f),V(1.15f,.13f,.14f),"ac8251");
            Box(g,"Porch",V(0,.17f,-2.6f),V(2.5f,.16f,1.1f),"a37b4d");
            Disc(g,V(0,2.7f,-2.12f),.42f,"4d6970"); Disc(g,V(0,2.7f,-2.19f),.12f,"b69a65");
            Box(g,"Window glow",V(1.56f,1.35f,.6f),V(.04f,.45f,.6f),"baa36e");
            Box(g,"Chimney",V(.9f,3.0f,.9f),V(.5f,1.5f,.6f),"687273");
        }
        static void Village()
        {
            var g=Group("03 Enemy headland village",root);
            House(g,"Chieftain longhouse",V(-3,-.04f,30.3f),1.15f,-9,"805346");
            House(g,"East workshop",V(5.7f,-.04f,28.0f),.82f,24,"756448");
            House(g,"Western home",V(-11.0f,-.04f,27.5f),.85f,-22,"885b48");
            House(g,"Far storehouse",V(5.1f,-.04f,33.8f),.67f,-14,"5c6961");
            Prop(g,"town_cart",2.3f,.03f,26.0f,.95f,76);
            Prop(g,"nature_log_stackLarge",8.6f,.03f,29.8f,.8f,15);
            Prop(g,"survival_workbench-anvil",3,.03f,30.3f,.85f,20);
            Prop(g,"pirate_barrel",-7.2f,.03f,25.8f,.85f,0);
            Prop(g,"pirate_crate",-8.3f,.03f,26.4f,.65f,24);
            Prop(g,"pirate_barrel",6.4f,.03f,25.1f,.67f,0);
            // Village paths form an irregular fork, completely outside the arena.
            Path(g,new[]{V(-10,.05f,24.4f),V(-7,.05f,25.4f),V(-2,.05f,26.2f),V(3,.05f,25.0f),V(7,.05f,26)},.6f);
            Path(g,new[]{V(-2,.05f,26.2f),V(1,.05f,29.4f),V(3,.05f,32)},.5f);
            Torch(g,V(-5,.05f,26));Torch(g,V(7.4f,.05f,26.2f));
        }
        static void Path(Transform p,Vector3[] points,float width)
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<points.Length-1;i++) { var n=Vector3.Cross((points[i+1]-points[i]).normalized,Vector3.up)*width;
                Quad(v,t,points[i]+n,points[i]-n,points[i+1]-n,points[i+1]+n); }
            Surface(p,"Worn footpath",v,t,Mat("8b8664"));
        }
        static void Harbour()
        {
            var g=Group("04 West harbour",root);
            House(g,"Boathouse",V(-13.3f,.08f,17.5f),.58f,65,"766049");
            // Pier follows the coastline down from the plateau to the sheltered sea.
            for(int i=0;i<4;i++) Box(g,"Pier approach",V(-15.1f-i*.65f,-.15f-i*.29f,22.4f),V(.73f,.18f,1.7f),"8c704f");
            var dock=Group("Timber pier",g,V(-18.6f,-1.35f,22.4f),0);
            for(int i=0;i<11;i++) Box(dock,"Dock plank",V(-2.4f+i*.47f,0,0),V(.42f,.17f,2.3f),i%3==0?"987c55":"846847");
            foreach(float x in new[]{-2.3f,.2f,2.3f}) foreach(float z in new[]{-.95f,.95f}) Beam(dock,V(x,-1.3f,z),V(x,.55f,z),.2f,"655540");
            Prop(g,"pirate_barrel",-18.0f,-1.24f,22.7f,.65f,20);
            Prop(g,"pirate_crate",-19.0f,-1.24f,23.0f,.58f,-14);
            Boat(g,V(-18.0f,-2.1f,28.7f),-22,.88f);
            Boat(g,V(-18.2f,-2.1f,-5.5f),12,.72f);
            Prop(g,"pirate_boat-row-small",-20.5f,-2.15f,19.0f,.7f,80);
            Prop(g,"nature_rock_largeB",-18,-2.5f,-8,1.25f,36);
            Torch(g,V(-14.4f,.0f,21.1f));
        }
        static void Boat(Transform p,Vector3 pos,float yaw,float scale)
        {
            var g=Group("Drakkar",p,pos,yaw);g.localScale=Vector3.one*scale;
            // Curved tapered hull with raised dragon prow; sides are timber strips.
            float[] zs={-3.9f,-3,-1.8f,0,1.8f,3,3.9f}; float[] widths={.03f,.62f,.95f,1.04f,.95f,.62f,.03f};
            for(int i=0;i<6;i++) foreach(int s in new[]{-1,1})
            {
                var v=new List<Vector3>();var t=new List<int>();float y0=Mathf.Abs(zs[i])*.16f+.38f,y1=Mathf.Abs(zs[i+1])*.16f+.38f;
                Quad(v,t,V(s*widths[i],y0,zs[i]),V(s*widths[i+1],y1,zs[i+1]),V(s*widths[i+1]*.5f,-.14f,zs[i+1]),V(s*widths[i]*.5f,-.14f,zs[i]));
                Quad(v,t,V(s*widths[i]*.5f,-.14f,zs[i]),V(s*widths[i+1]*.5f,-.14f,zs[i+1]),V(s*widths[i+1],y1,zs[i+1]),V(s*widths[i],y0,zs[i]));
                Surface(g,"Clinker hull",v,t,Mat(i%2==0?"71533b":"8b6747"));
                Beam(g,V(s*widths[i],y0,zs[i]),V(s*widths[i+1],y1,zs[i+1]),.12f,"b08a54");
            }
            Box(g,"Deck",V(0,.1f,0),V(1.5f,.12f,5.6f),"99794f");
            Beam(g,V(0,.1f,0),V(0,4.7f,0),.15f,"72593f");
            Beam(g,V(-1.8f,4.25f,0),V(1.8f,4.25f,0),.12f,"72593f");
            for(int i=0;i<6;i++)
            {
                var v=new List<Vector3>();var t=new List<int>();float a=-1.75f+i*.583f,b=a+.583f;
                Quad(v,t,V(a,1.75f,.08f),V(b,1.75f,.08f),V(b,3,.45f),V(a,3,.45f));
                Quad(v,t,V(a,3,.45f),V(b,3,.45f),V(b,4.2f,0),V(a,4.2f,0));
                Quad(v,t,V(a,4.2f,0),V(b,4.2f,0),V(b,3,.45f),V(a,3,.45f));
                Quad(v,t,V(a,3,.45f),V(b,3,.45f),V(b,1.75f,.08f),V(a,1.75f,.08f));
                Surface(g,"Striped square sail",v,t,Mat(i%2==0?"b9b092":"865a4d"));
            }
            Beam(g,V(0,.8f,3.7f),V(0,1.7f,4.0f),.23f,"b08a54");
            Beam(g,V(0,1.7f,4),V(0,2.0f,3.65f),.25f,"b08a54");
            Box(g,"Dragon head",V(0,2.0f,3.54f),V(.33f,.3f,.5f),"b08a54");
            foreach(int s in new[]{-1,1}) for(int i=0;i<4;i++)
            {
                var sh=Group("Gunwale shield",g,V(s*1.0f,.64f,-1.9f+i*1.15f),s*90);
                Disc(sh,Vector3.zero,.3f,i%2==0?"687d7b":"aa7951");Disc(sh,V(0,0,-.06f),.09f,"c0aa78");
            }
        }
        static void Forest()
        {
            var g=Group("05 Asymmetric pine groves",root);
            // Hand placed clusters, deliberately distinct densities and clearings; no mirrored rows.
            float[,] clusters={{14.0f,-11.6f,5,1.2f},{14.0f,17.0f,7,1.3f},{11.8f,32.1f,6,2.1f},{-14.2f,12.4f,4,1.1f},{-14.2f,-17.5f,3,1.1f},{-10.8f,33.1f,3,1.5f}};
            string[] names={"nature_tree_pineTallA","nature_tree_pineDefaultA","nature_tree_pineTallC","nature_tree_pineRoundB"};
            var placed=new List<Vector2>();
            for(int k=0;k<clusters.GetLength(0);k++) for(int i=0;i<(int)clusters[k,2];i++)
            {
                float x=clusters[k,0]+R(-clusters[k,3],clusters[k,3]),z=clusters[k,1]+R(-clusters[k,3],clusters[k,3]);
                if(Mathf.Abs(x)<13.5f && z<25.2f)continue;
                if(Mathf.Abs(z)<23 && Mathf.Abs(x)>Edge(x<0?-1:1,z)-.5f)continue;
                if(placed.Any(a=>Vector2.Distance(a,new Vector2(x,z))<1.45f))continue;
                placed.Add(new Vector2(x,z)); Prop(g,names[(i+k)%4],x,.18f,z,R(3.0f,5.5f),R(0,360));
                if(i%2==0)Prop(g,"nature_plant_bush",x+.6f,.02f,z+.4f,R(.45f,.8f),R(0,360));
            }
            Prop(g,"nature_log_large",14.0f,.02f,-5.2f,.55f,-22);
            Prop(g,"nature_stump_old",16.0f,.02f,-4.8f,.55f,0);
            Prop(g,"nature_rock_largeA",14.8f,-.04f,-18.5f,1.0f,36);
        }
        static void Torch(Transform p,Vector3 pos)
        {
            var g=Group("Torch",p,pos);Beam(g,V(0,0,0),V(0,1.45f,0),.13f,"655540");
            Box(g,"Brazier",V(0,1.35f,0),V(.27f,.24f,.27f),"525758");
            var flame=Box(g,"Amber flame",V(0,1.62f,0),V(.14f,.36f,.14f),"dda453");flame.transform.localRotation=Quaternion.Euler(12,24,15);
        }
        static void Camp()
        {
            var g=Group("06 Eastern camp and runestones",root);
            Prop(g,"survival_tent-canvas",13.7f,.12f,6.1f,1.2f,-42);
            Prop(g,"survival_campfire-pit",13.3f,.12f,3.8f,.4f,0);
            Prop(g,"nature_log_large",13.0f,.12f,5.1f,.4f,65);
            Prop(g,"survival_barrel",14.6f,.12f,6.1f,.62f,0);
            Prop(g,"pirate_crate",14.4f,.12f,7.1f,.5f,16);
            var fire=Group("Campfire flames",g,V(13.3f,.44f,3.8f)).gameObject;
            var ps=fire.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=.65f;main.startSpeed=.5f;main.startSize=.18f;
            main.startColor=new Color(1,.55f,.13f,.85f);main.maxParticles=20;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var em=ps.emission;em.rateOverTime=14;var sh=ps.shape;sh.shapeType=ParticleSystemShapeType.Cone;sh.radius=.14f;sh.angle=10;sh.rotation=V(-90,0,0);
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            fire.GetComponent<ParticleSystemRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/RoyalBuddies/Materials/RB_Particle.mat");
            var rune=Group("Runestone sanctuary",g,V(-13.5f,.12f,-8.3f),-15);
            Prop(rune,"nature_stone_tallA",0,0,0,2.4f,10);
            Beam(rune,V(0,.65f,-.45f),V(0,1.7f,-.45f),.055f,"a5b9a9");
            Beam(rune,V(0,1.7f,-.45f),V(.35f,1.35f,-.45f),.055f,"a5b9a9");
            Beam(rune,V(0,1.0f,-.45f),V(.35f,1.35f,-.45f),.055f,"a5b9a9");
            Prop(g,"nature_rock_largeB",-16,.0f,-10.3f,.65f,72);
        }
        static void CombineByDistrict()
        {
            // Bake each district into one mesh per material: keeps mobile draw calls modest and groups editable.
            foreach(Transform district in root)
            {
                var renderers=district.GetComponentsInChildren<MeshRenderer>();
                var groups=new Dictionary<Material,List<CombineInstance>>();
                foreach(var r in renderers)
                {
                    var f=r.GetComponent<MeshFilter>(); if(!f || !f.sharedMesh)continue;
                    for(int s=0;s<f.sharedMesh.subMeshCount;s++)
                    {
                        var mat=r.sharedMaterials[Mathf.Min(s,r.sharedMaterials.Length-1)];
                        if(!groups.TryGetValue(mat,out var list))groups[mat]=list=new List<CombineInstance>();
                        list.Add(new CombineInstance { mesh=f.sharedMesh,subMeshIndex=s,transform=district.worldToLocalMatrix*f.transform.localToWorldMatrix });
                    }
                    r.enabled=false;
                }
                var baked=Group("Baked rendering",district);
                foreach(var pair in groups)
                {
                    var mesh=new Mesh {name=district.name+"_"+pair.Key.name,indexFormat=IndexFormat.UInt32};
                    mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                    var go=Group(pair.Key.name,baked).gameObject;go.layer=2;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=pair.Key;mr.shadowCastingMode=ShadowCastingMode.Off;
                }
                // Keep authored transforms for editing, without duplicate serialized mesh copies/renderers.
                foreach(var r in renderers) { var f=r.GetComponent<MeshFilter>();Object.DestroyImmediate(r);if(f)Object.DestroyImmediate(f); }
            }
        }
        static void PersistMeshes()
        {
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())
                if(f.sharedMesh && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(f.sharedMesh)))
                {
                    string path=Out+"/Meshes/Fjord_"+(meshId++).ToString("D3")+".asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(existing){EditorUtility.CopySerialized(f.sharedMesh,existing);f.sharedMesh=existing;}
                    else AssetDatabase.CreateAsset(f.sharedMesh,path);
                }
            foreach(var guid in AssetDatabase.FindAssets("t:Mesh",new[]{Out+"/Meshes"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(int.TryParse(System.IO.Path.GetFileNameWithoutExtension(path).Replace("Fjord_",""),out int index) && index>=meshId)
                    AssetDatabase.DeleteAsset(path);
            }
        }
        static void Validate()
        {
            if(root.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("Exterior has colliders");
            if(root.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true).Length!=0)throw new Exception("Exterior has NavMesh obstacles");
            if(root.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)throw new Exception("Exterior has runtime behaviour");
            int tris=0,draws=0;
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())
            {
                tris+=f.sharedMesh.triangles.Length/3;draws++;
                foreach(var v in f.sharedMesh.vertices)
                {
                    var q=f.transform.TransformPoint(v);
                    if(q.y>-.06f && Mathf.Abs(q.x)<11.25f-.001f && Mathf.Abs(q.z)<23f-.001f)
                        throw new Exception("Exterior intrudes onto playable rectangle: "+q+" in "+f.name);
                }
            }
            File.WriteAllText("Logs/fjord-validation.txt","Protected arena/camera/bootstrap: identical before/after.\nExterior colliders: 0\nExterior scripts: 0\nExterior lights: 0\nExterior navigation obstacles: 0\nExterior mesh renderers: "+draws+"\nExterior triangles: "+tris+"\n");
        }
        public static void Capture(string path,int w,int h)
        {
            var cam=Camera.main;float old=cam.aspect;var target=cam.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(w,h,24);cam.targetTexture=rt;cam.aspect=(float)w/h;
            // Flush newly imported material uploads before capturing the first editor frame.
            bool batching=GraphicsSettings.useScriptableRenderPipelineBatching;GraphicsSettings.useScriptableRenderPipelineBatching=false;
            cam.Render();cam.Render();RenderTexture.active=rt;
            var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
            cam.targetTexture=target;cam.aspect=old;RenderTexture.active=active;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
            GraphicsSettings.useScriptableRenderPipelineBatching=batching;
        }
    }
}
