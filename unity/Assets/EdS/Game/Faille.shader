// Sous-lot V2.9.1 (demande de Damien du 7 novembre 2026 : « effets visuels des failles ») : la déchirure lumineuse d'une faille du Seuil
// (V2.5), et la déchirure dormante d'une cicatrice (V2.6), sur un simple quad. Écrit pour le jeu, sans code tiers, pour URP seulement
// (sans lui, la faille garde ses formes simples de la V2.5 : WorldEventsView).
//
// - une fente verticale aux lèvres déchiquetées, qui ondulent et brûlent (_Hot), bord violet du Seuil (_Edge) ;
// - dedans, un vide violet sombre qui tourne, une ligne de lumière au cœur ;
// - autour, l'image de la scène tirée vers la fente (_Distort à 1 : sur ordinateur, qui garde l'image opaque de la scène ; à 0 sur
//   téléphone et pour les cicatrices : la lueur seule) ;
// - _Opening (0 à 1) ouvre la fente, _Intensity règle l'éclat (le battement de la lumière, donné par le jeu).
Shader "EdS/Faille"
{
    Properties
    {
        _Void ("Vide", Color) = (0.08, 0.02, 0.16, 1)
        [HDR] _Edge ("Bord", Color) = (1.4, 0.7, 2.4, 1)
        [HDR] _Hot ("Lèvres qui brûlent", Color) = (2.4, 1.3, 0.8, 1)
        _Opening ("Ouverture", Range(0, 1)) = 1
        _Intensity ("Éclat", Float) = 1
        _Distort ("Distorsion de la scène", Range(0, 1)) = 0
        _Seed ("Graine", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Faille"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Void;
                float4 _Edge;
                float4 _Hot;
                float _Opening;
                float _Intensity;
                float _Distort;
                float _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float RiftHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Bruit de valeur en 2D, de 0 à 1.
            float RiftNoise(float2 x)
            {
                float2 i = floor(x);
                float2 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float a = RiftHash(i), b = RiftHash(i + float2(1, 0)), c = RiftHash(i + float2(0, 1)), d = RiftHash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;   // x en travers de la fente, y le long
                float t = _Time.y;
                float profile = pow(saturate(1.0 - p.y * p.y), 0.75);   // la fente s'amincit à ses deux bouts
                float slow = RiftNoise(float2(p.y * 5.0 + _Seed, t * 0.8));
                float fast = RiftNoise(float2(p.y * 17.0 - t * 2.1, _Seed * 3.0 + t * 0.3));
                float width = _Opening * 0.22 * profile * (0.75 + 0.5 * slow);
                float x = p.x - (slow - 0.5) * 0.12 * profile;   // la fente serpente
                float d = abs(x) - width - (fast - 0.5) * 0.05 * profile;   // lèvres déchiquetées
                float open = step(0.0001, _Opening);
                float inside = (1.0 - smoothstep(-0.01, 0.01, d)) * open;
                float glow = exp(-max(d, 0.0) * 38.0) * profile * open;
                float burn = saturate(1.0 - abs(d) * 25.0) * (0.6 + 0.8 * fast) * profile * open;
                // Le vide qui tourne, la ligne de lumière au cœur.
                float r = saturate(abs(x) / max(width, 0.001));
                float swirl = RiftNoise(float2(r * 3.0 - t * 1.5, p.y * 4.0 + t * 0.7 + _Seed));
                float3 hollow = _Void.rgb * (0.6 + 0.8 * swirl) + _Edge.rgb * pow(1.0 - r, 6.0) * 0.8 + _Edge.rgb * r * r * r * 0.5;
                float3 color = (hollow * inside + _Edge.rgb * glow * 0.9 * (1.0 - inside) + _Hot.rgb * burn * 0.8) * _Intensity;
                float alpha = inside;
                // La scène autour, tirée vers la fente (seulement là où l'image opaque existe : _Distort à 1).
                if (_Distort > 0.5)
                {
                    float halo = exp(-max(d, 0.0) * 9.0) * profile * open * (1.0 - inside);
                    float2 screen = GetNormalizedScreenSpaceUV(input.positionCS);
                    float2 pull = float2(-sign(x) * 0.035, (fast - 0.5) * 0.01) * halo * _Opening;
                    float3 scene = SampleSceneColor(screen + pull);
                    color += scene * halo;
                    alpha = saturate(alpha + halo);
                }
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
