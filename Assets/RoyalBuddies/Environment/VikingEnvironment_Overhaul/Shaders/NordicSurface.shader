Shader "RoyalBuddies/NordicSurface" {
Properties { _BaseColor("Tint",Color)=(.5,.55,.52,1) _Detail("Detail",Float)=1 _Smoothness("Roughness control",Range(0,1))=.15 }
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"} Pass { Tags {"LightMode"="UniversalForward"}
HLSLPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _BaseColor;float _Detail;float _Smoothness;
CBUFFER_END
struct A {float4 p:POSITION;float3 n:NORMAL;half4 c:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;half4 c:COLOR;};
V vert(A a){UNITY_SETUP_INSTANCE_ID(a);V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;return o;}
float hash(float3 p){return frac(sin(dot(p,float3(12.9898,78.233,39.425)))*43758.5453);}
half4 frag(V i):SV_Target {half3 n=normalize(i.n);float band=sin(i.w.y*15+sin(i.w.x*2.4)+sin(i.w.z*3.1));float grain=hash(floor(i.w*30))-.5;half3 col=_BaseColor.rgb*i.c.rgb*(1+_Detail*(band*.035+grain*.05));SurfaceData s=(SurfaceData)0;s.albedo=col;s.alpha=1;s.smoothness=_Smoothness;s.occlusion=1;s.normalTS=half3(0,0,1);InputData d=(InputData)0;d.positionWS=i.w;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.w);d.shadowCoord=TransformWorldToShadowCoord(i.w);d.bakedGI=SampleSH(n);d.shadowMask=1;return UniversalFragmentPBR(d,s);}
ENDHLSL
}
UsePass "Universal Render Pipeline/Lit/ShadowCaster"
UsePass "Universal Render Pipeline/Lit/DepthOnly"
} }
