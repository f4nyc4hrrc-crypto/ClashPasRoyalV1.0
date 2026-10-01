Shader "RoyalBuddies/NordicFoam" {
Properties { _BaseColor("Foam tint",Color)=(.82,.94,.92,1) }
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"} Pass {
Tags {"LightMode"="UniversalForward"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
HLSLPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _BaseColor;
CBUFFER_END
struct A {float4 p:POSITION;half4 c:COLOR;};struct V {float4 p:SV_POSITION;float2 w:TEXCOORD0;half4 c:COLOR;};
V vert(A a){V o;float3 w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(w);o.w=w.xz;o.c=a.c;return o;}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
half4 frag(V i):SV_Target {float n=noise(i.w*14+float2(_Time.y*.04,0));float a=.74*smoothstep(.18,.64,n);return half4(_BaseColor.rgb*i.c.rgb,a);}
ENDHLSL
} } }
