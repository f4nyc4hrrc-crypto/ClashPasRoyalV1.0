// Pelouse de l'arène : port du shader Godot (world → grass_shader), bruit de valeur sur l'UV.
Shader "RoyalBuddies/Grass"
{
    Properties
    {
        _Dark("Dark", Color) = (0.235, 0.390, 0.205, 1)
        _Light("Light", Color) = (0.390, 0.570, 0.300, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Dark;
                half4 _Light;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float value_noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1.0, 0.0)), f.x),
                            lerp(hash21(i + float2(0.0, 1.0)), hash21(i + float2(1.0, 1.0)), f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(v.normalOS);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = vn.normalWS;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // IMPORTANT: sample the grass from WORLD XZ, not each mesh's local UVs.
                // TurfPlayer/TurfEnemy and the Fjord are separate meshes; local UV sampling
                // restarted the noise at every mesh boundary and created the visible rectangular seam.
                // World-space sampling gives every grass surface one continuous pattern.
                float2 p = i.positionWS.xz * float2(0.43, 0.43);
                float n = value_noise(p) * 0.62 + value_noise(p * 2.35) * 0.25 + value_noise(p * 5.1) * 0.13;
                half3 c = lerp(_Dark.rgb, _Light.rgb, smoothstep(0.18, 0.86, n));
                float fine = (hash21(floor(i.positionWS.xz * float2(19.2, 19.2))) - 0.5) * 0.026;

                SurfaceData s = (SurfaceData)0;
                s.albedo = c + fine;
                s.metallic = 0;
                s.smoothness = 0.08;
                s.occlusion = 1;
                s.alpha = 1;
                s.normalTS = half3(0, 0, 1);

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.normalWS = normalize(i.normalWS);
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.bakedGI = SampleSH(d.normalWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);
                return UniversalFragmentPBR(d, s);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_TARGET { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
