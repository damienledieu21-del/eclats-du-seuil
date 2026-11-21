// V2.44.3 (décision de Damien du 20 novembre 2026 : toits enneigés par shader sur tous les bâtiments, sans crédit) : couche de neige posée
// par-dessus le décor (toits, rochers, sol, murets, objets), pour URP. Le jeu (SnowCover) double chaque modèle du décor d'un rendu de
// cette couche ; elle ne dessine que les faces tournées vers le haut, d'autant plus qu'il neige (neige de la plaine, ou du col au-dessus de
// sa limite), avec un bord irrégulier. Transparente, sans écriture de profondeur, légèrement avancée vers la caméra. Affichage seulement.
Shader "EdS/Neige"
{
    Properties
    {
        _SnowColor ("Couleur de la neige", Color) = (0.93, 0.94, 0.96, 1)
        _Slope ("Pente qui garde la neige (normale y)", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Neige"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SnowColor;
                half _Slope;
            CBUFFER_END

            // Posées par SeasonView (globales ; jamais posées, elles valent 0 et rien n'est dessiné).
            half _EdsSnow;
            float4 _EdsSnowPass;   // neige du col : quantité, limite (m), fondu (m)

            half EdsSnowAt(float y)
            {
                half colSnow = _EdsSnowPass.x * saturate((y - (_EdsSnowPass.y - _EdsSnowPass.z)) / max(_EdsSnowPass.z, 0.01));
                return max(_EdsSnow, colSnow);
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half snow = EdsSnowAt(input.positionWS.y);
                half3 normal = normalize(input.normalWS);
                // Faces tournées vers le haut : de la pente réglée à l'horizontale ; bord irrégulier (bruit), plus de neige au faîte.
                float2 p = input.positionWS.xz;
                half n = (half)(Noise(p * 0.7) * 0.6 + Noise(p * 3.1) * 0.4);
                half up = smoothstep(_Slope, _Slope + 0.3h, normal.y + (n - 0.5h) * 0.2h);
                half alpha = saturate(snow * 1.3h * up * (0.75h + 0.5h * n));
                clip(alpha - 0.01h);

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 light = SampleSH(normal) + sun.color * saturate(dot(normal, sun.direction)) * sun.shadowAttenuation;
                half3 color = _SnowColor.rgb * light;
                // Éclat discret des cristaux au soleil.
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                color += sun.color * pow(saturate(dot(normal, normalize(sun.direction + view))), 80.0h) * 0.25h * sun.shadowAttenuation;
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
