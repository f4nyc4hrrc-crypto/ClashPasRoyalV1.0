// Eau courante (ruisseaux et cascades) : traînées qui défilent dans le sens du courant + écume sur les bords.
// UV attendus : u = travers (0 → 1), v = distance le long du courant (en mètres). Opaque, léger pour mobile.
Shader "RoyalBuddies/FlowWater"
{
    Properties
    {
        _Deep("Deep", Color) = (0.07, 0.42, 0.62, 1)
        _Shallow("Shallow", Color) = (0.20, 0.68, 0.84, 1)
        _Foam("Foam", Color) = (0.88, 0.97, 1.0, 1)
        _Speed("Speed (m/s)", Float) = 0.9
        _StreakScale("Streak Scale", Float) = 1.6
        _EdgeFoam("Edge Foam Width", Range(0, 0.5)) = 0.14
        _FoamAmount("Foam Amount", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

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
                half4 _Foam;
                float _Speed;
                float _StreakScale;
                float _EdgeFoam;
                float _FoamAmount;
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
                float t = _Time.y * _Speed;
                // Traînées étirées dans le sens du courant (v), qui défilent vers l'aval.
                float2 p = float2(i.uv.x * 3.0, (i.uv.y - t) * _StreakScale);
                float n1 = noise2(float2(p.x * 2.2, p.y * 0.55));
                float n2 = noise2(float2(p.x * 4.5 + 7.0, (i.uv.y - t * 1.35) * _StreakScale * 1.3));
                float n = n1 * 0.65 + n2 * 0.35;

                // Plus profond au centre, plus clair près des berges.
                float center = 1.0 - abs(i.uv.x * 2.0 - 1.0);
                half3 col = lerp(_Shallow.rgb, _Deep.rgb, saturate(center * 1.2) * 0.75);
                col = lerp(col, _Shallow.rgb * 1.08, n * 0.35);

                // Écume : bords + petites crêtes mobiles.
                float edge = 1.0 - smoothstep(0.0, _EdgeFoam, min(i.uv.x, 1.0 - i.uv.x));
                float crest = smoothstep(0.72, 0.9, n);
                float foam = saturate(edge * 0.85 + crest * _FoamAmount);
                col = lerp(col, _Foam.rgb, foam);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
