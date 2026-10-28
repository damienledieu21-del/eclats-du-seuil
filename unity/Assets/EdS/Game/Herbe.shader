// Essai réaliste 4 (demande de Damien du 27 octobre 2026 : « le maximum de réalisme ») : herbe en brins pour URP, dessinée par milliers
// de touffes (une seule géométrie répétée par la carte graphique, StyleGrass). Couleur du pied à la pointe, nuance différente d'une touffe
// à l'autre, vent qui couche les pointes, ombres et brume reçues. Pas de texture à fournir. Affichage seulement.
Shader "EdS/Herbe"
{
    Properties
    {
        _BaseColor ("Couleur du pied", Color) = (0.2, 0.28, 0.09, 1)
        _TipColor ("Couleur de la pointe", Color) = (0.52, 0.62, 0.27, 1)
        _DryColor ("Couleur des brins secs", Color) = (0.62, 0.56, 0.33, 1)
        _Dryness ("Part de brins secs", Range(0, 1)) = 0.18
        _DryPatches ("Plaques d'herbe sèche", Range(0, 1)) = 0.35
        _Sheen ("Reflet des brins", Range(0, 1)) = 0.25
        _FadeStart ("Début de l'abaissement au loin (m)", Float) = 32
        _FadeEnd ("Fin de l'herbe (m)", Float) = 45
        [HideInInspector] _Reglages ("Version des réglages (posée par le menu)", Float) = 0
        _Wind ("Force du vent", Range(0, 2)) = 0.6
        _WindSpeed ("Vitesse du vent", Range(0, 5)) = 1.6
        _Translucency ("Lumière à travers les brins", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _TipColor;
            half4 _DryColor;
            half _Dryness;
            half _DryPatches;
            half _Sheen;
            float _FadeStart;
            float _FadeEnd;
            float _Reglages;
            float _Wind;
            float _WindSpeed;
            half _Translucency;
        CBUFFER_END

        float Hash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        // Brin couché par le vent : rafales qui traversent le pré, plus un léger frémissement propre à chaque brin. Le même dans toutes
        // les passes, pour que la profondeur (occlusion ambiante, préparation de la profondeur) colle à l'image.
        float3 Bent(float3 positionOS, float2 uv, out float3 pivot)
        {
            pivot = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
            float3 positionWS = TransformObjectToWorld(positionOS);
            // Essai réaliste 5 : au loin, la touffe s'abaisse en douceur jusqu'à disparaître à la limite de dessin.
            float fade = saturate((_FadeEnd - distance(pivot, _WorldSpaceCameraPos)) / max(0.01, _FadeEnd - _FadeStart));
            positionWS = pivot + (positionWS - pivot) * float3(1.0, fade, 1.0);
            float h = min(uv.y, 1.0);   // les têtes de fleurs (uv.y = 2) bougent comme les pointes
            float t = _Time.y * _WindSpeed;
            float gust = sin(t + pivot.x * 0.21 + pivot.z * 0.17) * 0.5 + 0.5;
            float shiver = sin(t * 4.1 + uv.x * 6.2831 + pivot.x * 1.3) * 0.15;
            float bend = (gust * 0.8 + shiver) * _Wind * h * h * 0.25;
            positionWS.xz += float2(0.8, 0.6) * bend;
            positionWS.y -= bend * bend * 0.5;
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "Herbe"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;      // x : brin (0 à 1), y : hauteur sur le brin (0 au pied, 1 à la pointe)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 color : TEXCOORD2;
                half height : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pivot;
                float3 positionWS = Bent(input.positionOS.xyz, input.uv, pivot);
                float h = min(input.uv.y, 1.0);
                float tint = Hash(pivot.xz);
                half3 green = lerp(_BaseColor.rgb, _TipColor.rgb, h) * (0.85 + 0.3 * tint);
                // Essai réaliste 5 : des plaques plus sèches (bruit doux sur le pré), en plus des brins secs épars.
                float2 cell = floor(pivot.xz * 0.12);
                float2 f = frac(pivot.xz * 0.12);
                f = f * f * (3.0 - 2.0 * f);
                float patch = lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x), lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), f.x), f.y);
                green = lerp(green, lerp(green, _DryColor.rgb * lerp(0.7, 1.0, h), 0.55), saturate((patch - 0.55) * 3.0) * _DryPatches);
                half dry = step(1.0 - _Dryness, Hash(pivot.xz + input.uv.x * 17.0));
                output.color = lerp(green, _DryColor.rgb * lerp(0.6, 1.0, h), dry);
                // Têtes de fleurs : blanc, jaune, violet ou bleu selon la tige.
                if (input.uv.y > 1.5)
                {
                    float pick = Hash(pivot.xz * 3.1 + input.uv.x * 41.0);
                    output.color = pick < 0.4 ? half3(0.93, 0.92, 0.86) : pick < 0.7 ? half3(0.96, 0.8, 0.22) : pick < 0.88 ? half3(0.6, 0.42, 0.82) : half3(0.38, 0.52, 0.92);
                }
                output.height = h;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 normalWS = normalize(IS_FRONT_VFACE(facing, true, false) ? input.normalWS : -input.normalWS);
                // Les brins sont fins : leur normale penche vers le haut, pour une lumière douce comme celle d'un pré.
                normalWS = normalize(lerp(normalWS, half3(0, 1, 0), 0.6h));
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half diffuse = saturate(dot(normalWS, sun.direction)) * 0.8h + 0.2h;
                half back = pow(saturate(dot(-view, sun.direction)), 3.0h) * _Translucency * input.height;
                half3 light = sun.color * sun.shadowAttenuation * (diffuse + back) + SampleSH(normalWS);
                // Essai réaliste 5 : léger reflet du soleil sur les brins ; le pied des touffes, à l'ombre des autres brins, plus sombre
                // (moins noir qu'avant).
                half3 halfDir = normalize(sun.direction + view);
                half sheen = pow(saturate(dot(normalWS, halfDir)), 24.0h) * _Sheen * input.height;
                half3 color = input.color * light * lerp(0.62h, 1.0h, input.height) + sun.color * sun.shadowAttenuation * sheen * 0.35h;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Profondeur seule (préparation de la profondeur d'URP) : sans elle, l'herbe disparaîtrait quand URP prépare la profondeur.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pivot;
                return TransformWorldToHClip(Bent(input.positionOS.xyz, input.uv, pivot));
            }

            half Frag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        // Profondeur et normales (occlusion ambiante de l'écran).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pivot;
                output.positionCS = TransformWorldToHClip(Bent(input.positionOS.xyz, input.uv, pivot));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 normalWS = normalize(IS_FRONT_VFACE(facing, true, false) ? input.normalWS : -input.normalWS);
                normalWS = normalize(lerp(normalWS, float3(0, 1, 0), 0.6));
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    return half4(PackFloat2To888(remappedOctNormalWS), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
