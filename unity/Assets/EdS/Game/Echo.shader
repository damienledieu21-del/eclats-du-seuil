// Sous-lot V2.9.1 (demande de Damien du 7 novembre 2026 : « échos fantômes ») : le rendu spectral des échos (bêtes revenues, échos de la
// nuit et des failles, loup blanc, Écho de la Rupture). Écrit pour le jeu, sans code tiers. Pour URP seulement (le jeu garde l'ancien
// rendu pâle des échos, V2.1, si ce shader manque ou n'est pas pris en charge : EchoGhosts).
//
// - contour lumineux (fresnel), plus vif la nuit et près d'une cicatrice (_Glow, réglé par le jeu) ;
// - corps transparent (_Solidity : plus solide quand l'écho est fort, en pleine vie, la nuit), grain repris de la texture du modèle ;
// - léger scintillement et bandes de lumière qui montent, corps qui ondule à peine (_Wobble) ;
// - dissolution (_Dissolve de 0 à 1) : le haut part le premier, bords qui brûlent (_EdgeColor), en braises ; à l'apparition, à l'envers ;
// - une première passe n'écrit que la profondeur (« SRPDefaultUnlit », dessinée par URP avant « UniversalForward ») : on ne voit pas les
//   pattes ni l'intérieur à travers le corps, la transparence reste propre ;
// - sans ombre (pas de passe ShadowCaster), sans éclairage : léger pour le téléphone.
Shader "EdS/Echo"
{
    Properties
    {
        _BaseMap ("Texture du modèle", 2D) = "white" {}
        _Tint ("Teinte du corps", Color) = (0.62, 0.74, 0.9, 1)
        [HDR] _RimColor ("Contour", Color) = (0.6, 0.8, 1.3, 1)
        [HDR] _EdgeColor ("Braises de la dissolution", Color) = (1.2, 1.4, 1.8, 1)
        _RimPower ("Finesse du contour", Range(0.5, 8)) = 2.4
        _Solidity ("Solidité", Range(0, 1)) = 0.55
        _Glow ("Éclat", Range(0, 4)) = 1
        _Flicker ("Scintillement", Range(0, 1)) = 0.5
        _Dissolve ("Dissolution", Range(0, 1)) = 0
        _EdgeWidth ("Largeur des braises", Range(0.01, 0.3)) = 0.08
        _Height ("Hauteur du corps (m)", Float) = 1.2
        _NoiseScale ("Grain de la dissolution", Float) = 3.5
        _Wobble ("Ondulation (m)", Float) = 0.012
        _Seed ("Graine", Float) = 0
        _Cutoff ("Découpe de la texture", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _Tint;
            float4 _RimColor;
            float4 _EdgeColor;
            float _RimPower;
            float _Solidity;
            float _Glow;
            float _Flicker;
            float _Dissolve;
            float _EdgeWidth;
            float _Height;
            float _NoiseScale;
            float _Wobble;
            float _Seed;
            float _Cutoff;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            float3 originWS : TEXCOORD3;
        };

        float EchoHash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        // Bruit de valeur en 3D (huit tirages), de 0 à 1.
        float EchoNoise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            float a = lerp(EchoHash(i + float3(0, 0, 0)), EchoHash(i + float3(1, 0, 0)), f.x);
            float b = lerp(EchoHash(i + float3(0, 1, 0)), EchoHash(i + float3(1, 1, 0)), f.x);
            float c = lerp(EchoHash(i + float3(0, 0, 1)), EchoHash(i + float3(1, 0, 1)), f.x);
            float d = lerp(EchoHash(i + float3(0, 1, 1)), EchoHash(i + float3(1, 1, 1)), f.x);
            return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
        }

        Varyings EchoVert(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            // Le corps ondule à peine (même calcul dans les deux passes : la profondeur écrite reste celle du dessin).
            positionWS += normalWS * _Wobble * sin(_Time.y * 3.0 + positionWS.y * 7.0 + _Seed);
            output.positionWS = positionWS;
            output.normalWS = normalWS;
            output.originWS = TransformObjectToWorld(float3(0, 0, 0));
            output.positionCS = TransformWorldToHClip(positionWS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            return output;
        }

        // Champ de la dissolution : le bas du corps tient le plus longtemps (le haut part en braises le premier). Rend aussi l'alpha de la
        // texture, pour la découpe des poils et des feuilles.
        float EchoField(Varyings input, out float4 texel)
        {
            texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            clip(texel.a - _Cutoff);
            float3 local = input.positionWS - input.originWS;
            float h = saturate(local.y / max(0.1, _Height));
            float n = EchoNoise(local * _NoiseScale + float3(0, -_Time.y * 0.6, _Seed));
            float v = 0.35 * (1.0 - h) + 0.65 * n;
            float margin = v - _Dissolve * 1.05;
            clip(margin + 0.0001);
            return margin;
        }
        ENDHLSL

        Pass
        {
            Name "EchoDepth"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex EchoVert
            #pragma fragment DepthFrag

            half4 DepthFrag(Varyings input) : SV_Target
            {
                float4 texel;
                EchoField(input, texel);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Echo"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex EchoVert
            #pragma fragment EchoFrag

            half4 EchoFrag(Varyings input) : SV_Target
            {
                float4 texel;
                float margin = EchoField(input, texel);
                float t = _Time.y;
                float3 n = normalize(input.normalWS);
                float3 v = normalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(n, v)), _RimPower);
                // Scintillement : deux battements lents qui se croisent (jamais au même rythme d'un écho à l'autre : graine).
                float flicker = 1.0 - _Flicker * 0.35 * (0.5 + 0.5 * sin(t * 11.0 + _Seed * 7.1)) * (0.5 + 0.5 * sin(t * 3.7 + _Seed * 3.3));
                // Fines bandes de lumière qui montent le long du corps.
                float band = smoothstep(0.9, 1.0, frac(input.positionWS.y * 1.6 - t * 0.5 + _Seed)) * 0.3;
                // Braises : la lisière de la dissolution brûle (seulement pendant qu'il se forme ou se défait).
                float edge = (1.0 - saturate(margin / _EdgeWidth)) * step(0.001, _Dissolve);
                float luminance = dot(texel.rgb, float3(0.3, 0.59, 0.11));
                float alpha = saturate(_Solidity * flicker);
                float3 body = _Tint.rgb * (0.35 + 0.9 * luminance);
                float3 color = body * alpha
                    + _RimColor.rgb * fresnel * _Glow * (0.6 + 0.4 * flicker)
                    + _RimColor.rgb * band * _Glow * alpha
                    + _EdgeColor.rgb * edge * 3.0;
                return half4(color, saturate(alpha + fresnel * 0.25));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
