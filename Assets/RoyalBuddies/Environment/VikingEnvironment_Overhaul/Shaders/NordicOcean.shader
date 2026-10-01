Shader "RoyalBuddies/NordicOcean" {
Properties { _Deep("Deep",Color)=(.025,.19,.29,1) _Shallow("Shallow",Color)=(.08,.43,.48,1) }
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"} Pass { Tags {"LightMode"="UniversalForward"}
HLSLPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _Deep;half4 _Shallow;
CBUFFER_END
struct A {float4 p:POSITION;};struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;};
V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);return o;}
float h(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(h(a),h(a+float2(1,0)),f.x),lerp(h(a+float2(0,1)),h(a+1),f.x),f.y);}
half4 frag(V i):SV_Target {float2 p=i.w.xz;float t=_Time.y*.12;float n=noise(p*.65+float2(t,-t*.4));float m=noise(p*1.8+float2(-t*.7,t));float shore=exp(-max(abs(p.x)-16,0)*.20)*exp(-max(abs(p.y-4)-34,0)*.14);float rip=pow(saturate(1-abs(sin(p.x*2.2+p.y*3.4+n*6+t*3))),16);half3 col=lerp(_Deep.rgb,_Shallow.rgb,.12+shore*.5+n*.14);col+=half3(.32,.52,.53)*rip*.13*m;return half4(col,1);}
ENDHLSL
} } }
