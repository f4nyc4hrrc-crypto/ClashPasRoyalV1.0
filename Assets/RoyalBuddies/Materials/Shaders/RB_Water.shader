// Eau de la rivière : port du shader Godot (setup_water_material) — bruit organique discret, sans bandes répétitives.
Shader "RoyalBuddies/Water"
{
    Properties
    {
        _Deep("Deep", Color) = (0.055, 0.40, 0.62, 1)
        _Shallow("Shallow", Color) = (0.08, 0.62, 0.78, 1)
        _Glint("Glint", Color) = (0.52, 0.88, 0.96, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep;
                half4 _Shallow;
                half4 _Glint;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * 0.10;
                float n1 = noise2(i.uv * 3.2 + float2(t, -t * 0.45));
                float n2 = noise2(i.uv * 6.0 + float2(-t * 0.55, t * 0.30));
                float n = n1 * 0.72 + n2 * 0.28;
                half3 col = lerp(_Deep.rgb, _Shallow.rgb, 0.24 + n * 0.22);
                float sparkle = smoothstep(0.80, 0.94, n);
                col = lerp(col, _Glint.rgb, sparkle * 0.16);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
