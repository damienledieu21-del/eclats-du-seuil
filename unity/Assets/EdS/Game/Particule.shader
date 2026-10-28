// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « impacts, étincelles et lueurs des compétences, en particules faites dans
// Unity ») : particule texturée (textures CC0 du « Particle Pack » de Kenney), teintée par la couleur de chaque particule (couleur de départ
// et fondu de vie du système de particules). Mélange réglé par le matériau : additif (étincelles, éclairs, lueurs : One sur la
// destination) ou fondu ordinaire (fumée, poussière ; texture prise en blanc, sa transparence seule). Sans éclairage ni ombre : léger pour
// le téléphone.
Shader "EdS/Particule"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensité", Float) = 1
        _White ("Couleur blanche (fondu)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Particule"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Intensity;
                float _White;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // Relecture de l'essai 7 : les textures de Kenney ont leur gris qui suit la transparence ; en fondu ordinaire (fumée,
                // poussière), seule la transparence compte, sinon les bords sont sombres.
                float3 rgb = lerp(t.rgb, float3(1, 1, 1), _White);
                return float4(rgb * input.color.rgb * _Intensity, t.a * input.color.a);
            }
            ENDHLSL
        }
    }

    // Rendu intégré de Unity (projet sans URP) : même dessin.
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Intensity;
            float _White;

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.pos = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 t = tex2D(_MainTex, input.uv);
                float3 rgb = lerp(t.rgb, float3(1, 1, 1), _White);
                return float4(rgb * input.color.rgb * _Intensity, t.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
