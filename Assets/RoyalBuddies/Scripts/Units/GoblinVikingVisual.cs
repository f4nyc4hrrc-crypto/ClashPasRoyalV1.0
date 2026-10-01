using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoyalBuddies
{
    // Baked Blender skeleton and poses. Shared mesh/clip data; GPU skinning, no runtime import or reflection.
    public sealed class GoblinVikingVisual : MonoBehaviour
    {
        struct Pose { public Vector3 position, scale; public Quaternion rotation; }
        sealed class Clip { public float duration; public Pose[][] frames; }
        sealed class Model { public Mesh mesh; public string[] names; public int[] parents; public Pose[] rest; public Dictionary<string,Clip> clips; }
        static Model cached;
        static Material sharedMaterial;
        Transform[] bones;
        Pose[] blendFrom;
        Model model;
        Clip current;
        string currentName;
        bool moving, dead;
        float elapsed, speed = 1f, playback = 1f, blendTime, blendLength;

        void Awake()
        {
            model = cached ?? (cached = Load());
            bones = new Transform[model.names.Length]; blendFrom = new Pose[bones.Length];
            for (int i=0; i<bones.Length; i++) bones[i] = new GameObject(model.names[i]).transform;
            for (int i=0; i<bones.Length; i++) { bones[i].SetParent(model.parents[i]<0 ? transform : bones[model.parents[i]],false); Apply(i,model.rest[i]); }
            var body = new GameObject("GoblinV2_SkinnedBody"); body.transform.SetParent(transform,false);
            var renderer = body.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh=model.mesh; renderer.bones=bones; renderer.rootBone=bones[0];
            renderer.localBounds=new Bounds(new Vector3(0,.50f,0),new Vector3(2.7f,2.6f,2.7f));
            renderer.quality=SkinQuality.Bone4; renderer.updateWhenOffscreen=false; renderer.shadowCastingMode=ShadowCastingMode.On;
            if (!sharedMaterial) sharedMaterial=Resources.Load<Material>("GoblinViking/GoblinVertexColor");
            if (!sharedMaterial) throw new InvalidDataException("Goblin vertex-color material missing");
            renderer.sharedMaterial=sharedMaterial;
            Play("Idle",0);
        }
        static Vector3 V(BinaryReader r) { return new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()); }
        static Quaternion Q(BinaryReader r) { return new Quaternion(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()); }
        static string S(BinaryReader r) { int n=r.ReadInt32(); if(n<0||n>256)throw new InvalidDataException("Goblin bone/clip name invalid"); return Encoding.UTF8.GetString(r.ReadBytes(n)); }
        static Pose P(BinaryReader r) { return new Pose {position=V(r),rotation=Q(r),scale=V(r)}; }
        static Model Load()
        {
            var asset=Resources.Load<TextAsset>("GoblinViking/GoblinV2");
            if(!asset)throw new InvalidDataException("GoblinV2 baked model missing");
            using(var r=new BinaryReader(new MemoryStream(asset.bytes))) {
                if(r.ReadInt32()!=0x52424732)throw new InvalidDataException("GoblinV2 format mismatch");
                int vc=r.ReadInt32(), ic=r.ReadInt32(), bc=r.ReadInt32(), cc=r.ReadInt32();
                if(vc<1||vc>100000||bc<1||bc>128||ic<3||ic>600000||cc<1||cc>32)throw new InvalidDataException("GoblinV2 counts invalid");
                var vertices=new Vector3[vc]; var colors=new Color[vc]; var weights=new BoneWeight[vc];
                for(int i=0;i<vc;i++) {vertices[i]=V(r); colors[i]=new Color(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()); var w=new BoneWeight();
                    w.boneIndex0=r.ReadInt32();w.boneIndex1=r.ReadInt32();w.boneIndex2=r.ReadInt32();w.boneIndex3=r.ReadInt32();
                    w.weight0=r.ReadSingle();w.weight1=r.ReadSingle();w.weight2=r.ReadSingle();w.weight3=r.ReadSingle();weights[i]=w;}
                var indices=new int[ic];for(int i=0;i<ic;i++)indices[i]=r.ReadInt32();
                var m=new Model {names=new string[bc],parents=new int[bc],rest=new Pose[bc],clips=new Dictionary<string,Clip>()}; var bind=new Matrix4x4[bc];
                for(int i=0;i<bc;i++) {m.names[i]=S(r);m.parents[i]=r.ReadInt32();m.rest[i]=P(r);var matrix=new Matrix4x4();for(int row=0;row<4;row++)for(int col=0;col<4;col++)matrix[row,col]=r.ReadSingle();bind[i]=matrix;}
                for(int i=0;i<cc;i++){string name=S(r);float duration=r.ReadSingle();int frames=r.ReadInt32();var c=new Clip {duration=duration,frames=new Pose[frames][]};
                    for(int f=0;f<frames;f++){c.frames[f]=new Pose[bc];for(int b=0;b<bc;b++)c.frames[f][b]=P(r);}m.clips.Add(name,c);}
                var mesh=new Mesh {name="GoblinVikingV2_SharedMesh",indexFormat=vc>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=indices;mesh.boneWeights=weights;mesh.bindposes=bind;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.UploadMeshData(false);m.mesh=mesh;return m;
            }
        }
        void Apply(int i,Pose p) {bones[i].localPosition=p.position;bones[i].localRotation=p.rotation;bones[i].localScale=p.scale;}
        public float ClipLength(string state,float fallback) {return model!=null&&model.clips.TryGetValue(state,out var c)?c.duration:fallback;}
        public void SetSpeed(float value) {speed=Mathf.Max(0,value);}
        public void SetMoving(bool value) {moving=value;if(dead)return;if(currentName=="Idle"||currentName=="Run")Play(value?"Run":"Idle",.06f);}
        public void Play(string state,float blend,float attackInterval=0)
        {
            if(model==null || !model.clips.TryGetValue(state=="Move"?"Run":state,out var clip))return;
            state=state=="Move"?"Run":state;
            if(dead && state!="Death")return;
            if(currentName==state && (state=="Idle"||state=="Run"||state=="Death"))return;
            for(int i=0;i<bones.Length;i++)blendFrom[i]=new Pose {position=bones[i].localPosition,rotation=bones[i].localRotation,scale=bones[i].localScale};
            current=clip;currentName=state;elapsed=0;blendTime=0;blendLength=Mathf.Max(0,blend);dead=state=="Death";
            playback=attackInterval>0 ? clip.duration/Mathf.Max(.1f,attackInterval*.92f) : 1f;
        }
        void LateUpdate()
        {
            if(current==null)return;float dt=Time.deltaTime*speed;elapsed+=dt*playback;blendTime+=dt;
            bool loop=currentName=="Idle"||currentName=="Run";
            if(!loop&&!dead&&elapsed>=current.duration){Play(moving?"Run":"Idle",.06f);loop=true;}
            float t=loop?Mathf.Repeat(elapsed,current.duration):Mathf.Min(elapsed,current.duration);
            float frame=t/current.duration*(current.frames.Length-1);int a=Mathf.FloorToInt(frame),b=Mathf.Min(a+1,current.frames.Length-1);float f=frame-a;
            float mix=blendLength<=0?1:Mathf.Clamp01(blendTime/blendLength);
            for(int i=0;i<bones.Length;i++) {var pa=current.frames[a][i];var pb=current.frames[b][i];var p=new Pose {position=Vector3.Lerp(pa.position,pb.position,f),rotation=Quaternion.Slerp(pa.rotation,pb.rotation,f),scale=Vector3.Lerp(pa.scale,pb.scale,f)};
                if(mix<1){p.position=Vector3.Lerp(blendFrom[i].position,p.position,mix);p.rotation=Quaternion.Slerp(blendFrom[i].rotation,p.rotation,mix);p.scale=Vector3.Lerp(blendFrom[i].scale,p.scale,mix);}Apply(i,p);}
        }
    }
}
