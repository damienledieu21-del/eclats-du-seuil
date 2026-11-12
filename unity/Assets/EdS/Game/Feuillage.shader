// Essai réaliste 4 (demande de Damien du 27 octobre 2026 : « le maximum de réalisme ») : feuillages, arbres, arbustes et plantes pour URP,
// avec le vent. Éclairage physique d'URP (ombres reçues et projetées, occlusion ambiante de l'écran, brume), transparence découpée
// (feuilles, brins), vu des deux côtés, lumière qui traverse les feuilles à contre-jour. Le vent balance le modèle d'autant plus qu'on
// monte au-dessus de son pied, et fait frémir les feuilles. Affichage seulement.
Shader "EdS/Feuillage"
{
    Properties
    {
        _BaseMap ("Couleur", 2D) = "white" {}
        _BaseColor ("Teinte", Color) = (1, 1, 1, 1)
        _BumpMap ("Relief", 2D) = "bump" {}
        _BumpScale ("Force du relief", Float) = 1
        _Cutoff ("Seuil de transparence", Range(0, 1)) = 0.45
        _Smoothness ("Lissé", Range(0, 1)) = 0.15
        _Wind ("Force du vent", Range(0, 3)) = 0.3
        _WindSpeed ("Vitesse du vent", Range(0, 5)) = 1.2
        _Flutter ("Frémissement des feuilles", Range(0, 1)) = 1
        _Translucency ("Lumière à travers les feuilles", Range(0, 2)) = 0.6
        // V2.25.4 (arbres de l'atelier) : couleur de sommet des cartes × 1,1 (variation de teinte ; 0 : ignorée, comme avant), veines
        // lumineuses de l'arbre corrompu (noir par défaut : rien), faces éliminées réglables (_FaceCull, propre à ce shader pour ne pas
        // reprendre le _Cull d'URP d'une matière convertie : 0 aucune, comme avant ; 2 l'arrière, pour l'écorce).
        _VertexTint ("Couleur de sommet × 1,1", Range(0, 1)) = 0
        _GlowMap ("Émission", 2D) = "black" {}
        [HDR] _GlowColor ("Couleur d'émission", Color) = (0, 0, 0, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _FaceCull ("Faces éliminées", Float) = 0
        [HideInInspector] _Cull ("__cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_GlowMap);
        SAMPLER(sampler_GlowMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BumpMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Cutoff;
            half _Smoothness;
            float _Wind;
            float _WindSpeed;
            float _Flutter;
            half _Translucency;
            float _Cull;
            half _VertexTint;
            half4 _GlowColor;
        CBUFFER_END

        // Vent : balancement de tout le modèle (croît avec la hauteur au-dessus de son pied) et frémissement des feuilles.
        float3 Windy(float3 positionWS)
        {
            float3 pivot = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
            float h = max(0.0, positionWS.y - pivot.y);
            float phase = dot(pivot.xz, float2(0.131, 0.217));
            float t = _Time.y * _WindSpeed;
            float sway = sin(t + phase) * 0.6 + sin(t * 2.3 + phase * 1.7 + positionWS.x * 0.35) * 0.25;
            float bend = _Wind * sway * (h * 0.08 + h * h * 0.01);
            float flutter = sin(t * 7.0 + dot(positionWS, float3(1.7, 2.3, 1.1))) * 0.02 * _Flutter * saturate(h * 2.0);
            positionWS.xz += float2(0.8, 0.6) * bend + flutter;
            positionWS.y += flutter * 0.5;
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "Feuillage"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_FaceCull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // Rendu Forward+ (Unity 6.0 : _FORWARD_PLUS ; 6.1 et après : _CLUSTER_LIGHT_LOOP) : sans lui, le feu de la forge n'éclairerait
            // pas les feuillages.
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half3 tint : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = Windy(TransformObjectToWorld(input.positionOS.xyz));
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.tint = lerp(half3(1.0h, 1.0h, 1.0h), input.color.rgb * 1.1h, _VertexTint);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);
                albedo.rgb *= input.tint;

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 normalWS = normalize(input.normalWS);
                half3 tangentWS = normalize(input.tangentWS.xyz);
                half3 bitangentWS = input.tangentWS.w * cross(normalWS, tangentWS);
                normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(tangentWS, bitangentWS, normalWS)));
                // Vue de dos (feuille, brin) : la normale se retourne.
                normalWS = IS_FRONT_VFACE(facing, true, false) ? normalWS : -normalWS;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.alpha = 1.0h;
                surface.metallic = 0.0h;
                surface.smoothness = _Smoothness;
                surface.normalTS = normalTS;
                surface.occlusion = 1.0h;

                half4 color = UniversalFragmentPBR(inputData, surface);

                // Contre-jour : la lumière du soleil traverse les feuilles vues face au soleil.
                Light sun = GetMainLight(inputData.shadowCoord);
                half back = saturate(dot(-inputData.viewDirectionWS, sun.direction));
                color.rgb += albedo.rgb * sun.color * pow(back, 4.0h) * _Translucency * sun.shadowAttenuation;
                color.rgb += SAMPLE_TEXTURE2D(_GlowMap, sampler_GlowMap, input.uv).rgb * _GlowColor.rgb;

                color.rgb = MixFog(color.rgb, input.fogFactor);
                return half4(color.rgb, 1.0h);
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
            Cull [_FaceCull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

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
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = Windy(TransformObjectToWorld(input.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_FaceCull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformWorldToHClip(Windy(TransformObjectToWorld(input.positionOS.xyz)));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_FaceCull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

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
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformWorldToHClip(Windy(TransformObjectToWorld(input.positionOS.xyz)));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                float3 normalWS = normalize(IS_FRONT_VFACE(facing, true, false) ? input.normalWS : -input.normalWS);
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
