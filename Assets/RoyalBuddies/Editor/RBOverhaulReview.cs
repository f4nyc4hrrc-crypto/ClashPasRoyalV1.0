#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
public static class RBOverhaulReview {
const string ScenePath="Assets/RoyalBuddies/Scenes/RB_Battle.unity";
[MenuItem("Royal Buddies/Nordic Overhaul/Verify saved scene and capture views")]
public static void VerifyAndCapture(){
EditorSceneManager.OpenScene(ScenePath);var root=GameObject.Find("VikingEnvironment_Overhaul");
if(!root)throw new System.Exception("Nordic environment root missing");
if(root.GetComponentsInChildren<Collider>(true).Length>0)throw new System.Exception("Scenery colliders detected");
foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))if(!f.sharedMesh)throw new System.Exception("Missing mesh: "+f.name);
foreach(var r in root.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(!m||!m.shader||ShaderUtil.ShaderHasError(m.shader))throw new System.Exception("Missing material or shader error: "+r.name);
var arena=GameObject.Find("RB_Arena");if(!arena)throw new System.Exception("Arena missing");
var towerPositions=new[]{new Vector3(-6.4f,0,15),new Vector3(6.4f,0,15),new Vector3(0,0,20),new Vector3(-6.4f,0,-15),new Vector3(6.4f,0,-15),new Vector3(0,0,-20)};
var towers=arena.GetComponentsInChildren<MonoBehaviour>(true).Where(t=>t && t.GetType().FullName=="RoyalBuddies.Tower").ToArray();if(towers.Length!=6)throw new System.Exception("Expected six gameplay towers");
foreach(var p in towerPositions)if(!towers.Any(t=>Vector3.Distance(t.transform.position,p)<.01f))throw new System.Exception("Tower moved: "+p);
var c=Camera.main;if(!c)throw new System.Exception("Gameplay camera missing");
Directory.CreateDirectory("OverhaulCaptures");Capture("OverhaulCaptures/01_mobile.png",720,1560);
var pos=c.transform.position;var rot=c.transform.rotation;var names=new[]{"02_overview","03_cliff","04_harbor","05_village","06_transition","07_shore"};
var eye=new[]{new Vector3(44,52,-60),new Vector3(25,6,-25),new Vector3(-30,8,17),new Vector3(15,13,19),new Vector3(22,5,-15),new Vector3(26,4,11)};
var target=new[]{new Vector3(0,0,5),new Vector3(13,-.6f,-23),new Vector3(-17,0,23),new Vector3(-1,1,29),new Vector3(14,0,-12),new Vector3(16,-1,15)};
try{for(int i=0;i<names.Length;i++){c.transform.position=eye[i];c.transform.LookAt(target[i]);Capture("OverhaulCaptures/"+names[i]+".png",1500,1100);}}finally{c.transform.position=pos;c.transform.rotation=rot;}
Debug.Log("Nordic overhaul: meshes/materials/shaders verified; six towers unchanged; captures written to OverhaulCaptures. Play Mode and device profiling remain separate checks.");
}
static void Capture(string path,int w,int h){var c=Camera.main;var rt=new RenderTexture(w,h,24);var old=c.targetTexture;var active=RenderTexture.active;float aspect=c.aspect;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);try{c.targetTexture=rt;c.aspect=(float)w/h;c.Render();c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}finally{c.targetTexture=old;c.aspect=aspect;RenderTexture.active=active;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);}}
}
#endif
