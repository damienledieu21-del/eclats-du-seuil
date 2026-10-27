// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : « Les attaques aussi des ennemis sont à changer c'est à dire la charge rouge du
// sanglier ou les rond des autres personnages ») : annonce d'une attaque au sol, dans le style « Fragments du Seuil ». Une forme pleine
// et translucide (TelegraphShapes) : bord lumineux, front qui avance de l'origine jusqu'au bout au moment de l'impact (la partie atteinte
// se remplit), chevrons qui filent vers l'avant (bande, cône) ou ondes qui s'éloignent (cercle) ; ambre, qui rougit quand l'impact
// approche, rouge vif et pulsé pour une attaque imparable. Réglages par annonce (MaterialPropertyBlock) : _Progress, _Kind, _Unblockable,
// _Linger. Affichage seulement.
// Sommets : uv = (avancée 0 à 1, place sur la largeur -1 à 1), uv2 = (distance au bord en mètres, négative dehors ; portée en mètres).
Shader "EdS/Annonce"
{
    Properties
    {
        _Color ("Ambre", Color) = (1, 0.72, 0.28, 1)
        _Hot ("Rouge de l'impact", Color) = (1, 0.27, 0.16, 1)
        _Danger ("Imparable", Color) = (1, 0.12, 0.32, 1)
        _Progress ("Avancée vers l'impact", Range(0, 1)) = 0
        _Kind ("Forme (0 cercle, 1 bande ou cône)", Float) = 0
        _Unblockable ("Imparable", Float) = 0
        _Linger ("Zone qui reste", Float) = 0
        _Opacity ("Opacité", Range(0, 1)) = 1
    }

    HLSLINCLUDE
    // Couleur et opacité d'un point de l'annonce (commun aux deux rendus).
    float4 AnnonceColor(float2 uv, float2 uv2, float time, float4 amber, float4 hot, float4 danger, float progress, float kind,
        float unblockable, float linger, float opacity)
    {
        float t = uv.x, side = uv.y, edge = uv2.x, reach = max(uv2.y, 0.05);
        clip(edge + 0.001);
        float rim = 1.0 - smoothstep(0.0, 0.1, edge);              // filet du bord (10 cm)
        float glow = 1.0 - smoothstep(0.0, 0.6, edge);             // lueur sous le bord
        float reached = 1.0 - smoothstep(progress - 0.004, progress + 0.004, t);
        float front = exp(-abs(t - progress) * reach / 0.09) * step(0.002, progress) * (1.0 - linger);
        float d = t * reach;
        float pattern;
        if (kind < 0.5)
        {
            float wave = frac(d / 1.2 - time * 0.8);
            pattern = smoothstep(0.0, 0.06, wave) * (1.0 - smoothstep(0.06, 0.22, wave));
        }
        else
        {
            float chevron = frac(d / 1.1 - abs(side) * 0.45 - time * 1.3);
            pattern = smoothstep(0.0, 0.05, chevron) * (1.0 - smoothstep(0.05, 0.2, chevron));
        }
        float heat = saturate(progress * progress);
        float pulse = 0.5 + 0.5 * sin(time * (unblockable > 0.5 ? 14.0 : 7.0));
        float3 color = lerp(amber.rgb, hot.rgb, heat);
        color = lerp(color, danger.rgb, step(0.5, unblockable));
        float alpha = 0.07 + 0.22 * reached + 0.16 * pattern * (1.0 - 0.6 * reached) + 0.85 * rim + 0.18 * glow + 0.9 * front;
        alpha += linger * (0.12 + 0.18 * pulse) + unblockable * 0.1 * pulse * rim;
        float3 light = color + (front + 0.45 * rim) * float3(1.0, 0.92, 0.8) * 0.6;
        return float4(light, saturate(alpha) * opacity);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Annonce"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Hot;
                float4 _Danger;
                float _Progress;
                float _Kind;
                float _Unblockable;
                float _Linger;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.uv2 = input.uv2;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return AnnonceColor(input.uv, input.uv2, _Time.y, _Color, _Hot, _Danger, _Progress, _Kind, _Unblockable, _Linger, _Opacity);
            }
            ENDHLSL
        }
    }

    // Rendu intégré de Unity (projet sans URP) : même dessin.
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            float4 _Color;
            float4 _Hot;
            float4 _Danger;
            float _Progress;
            float _Kind;
            float _Unblockable;
            float _Linger;
            float _Opacity;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.pos = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.uv2 = input.uv2;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return AnnonceColor(input.uv, input.uv2, _Time.y, _Color, _Hot, _Danger, _Progress, _Kind, _Unblockable, _Linger, _Opacity);
            }
            ENDHLSL
        }
    }
}
