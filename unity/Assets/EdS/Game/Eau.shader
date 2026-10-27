// Essai réaliste (demande de Damien du 27 octobre 2026 : « l'eau n'est pas du tout réaliste ») : eau de la rivière pour URP.
// Transparente (on devine le lit creusé sous elle), elle reflète le vrai ciel (le panorama du catalogue) et, depuis l'essai 4, les objets
// alentour (sonde de reflets posée par le jeu), d'autant plus qu'on la regarde de biais, porte deux couches de vagues qui avancent dans deux directions, et renvoie l'éclat du soleil. Aucune texture à
// fournir : les vagues sont faites par le code (StyleWorld.Ripples) et le ciel vient du catalogue. Affichage seulement.
Shader "EdS/Eau"
{
    Properties
    {
        _Color ("Couleur de l'eau", Color) = (0.1, 0.24, 0.27, 1)
        _DeepColor ("Couleur du fond", Color) = (0.03, 0.09, 0.1, 1)
        _Ripples ("Vagues (faites par le code)", 2D) = "bump" {}
        _SkyTex ("Ciel (panorama du catalogue)", 2D) = "gray" {}
        _WaveSize ("Taille des vagues (m)", Range(0.5, 20)) = 3.5
        _WaveSpeed ("Vitesse des vagues", Range(0, 1)) = 0.06
        _WaveStrength ("Force des vagues", Range(0, 2)) = 0.55
        _Opacity ("Opacité vue de haut", Range(0, 1)) = 0.72
        _Reflection ("Reflet du ciel (minimum)", Range(0, 1)) = 0.12
        _SkyBoost ("Luminosité du reflet", Range(0, 3)) = 1.0
        _Sparkle ("Éclat du soleil", Range(0, 10)) = 3.0
        [HideInInspector] _ReflectionCube ("Reflets des objets (sonde, posée par le jeu)", Cube) = "" {}
        [HideInInspector] _ProbeWeight ("Part de la sonde", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Eau"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Ripples);
            SAMPLER(sampler_Ripples);
            TEXTURE2D(_SkyTex);
            SAMPLER(sampler_SkyTex);
            TEXTURECUBE(_ReflectionCube);
            SAMPLER(sampler_ReflectionCube);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _DeepColor;
                float4 _Ripples_ST;
                float4 _SkyTex_ST;
                float _WaveSize;
                float _WaveSpeed;
                half _WaveStrength;
                half _Opacity;
                half _Reflection;
                half _SkyBoost;
                half _Sparkle;
                half _ProbeWeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Pente des vagues : la carte faite par le code range x et y de la normale dans le rouge et le vert.
            half2 Slope(float2 uv)
            {
                half2 xy = SAMPLE_TEXTURE2D(_Ripples, sampler_Ripples, uv).rg * 2.0h - 1.0h;
                return xy;
            }

            // Coordonnées du panorama (comme le ciel « Skybox/Panoramic » de Unity, sans rotation).
            float2 Panorama(float3 direction)
            {
                float3 d = normalize(direction);
                float latitude = acos(clamp(d.y, -1.0, 1.0));
                float longitude = atan2(d.z, d.x);
                return float2(0.5, 1.0) - float2(longitude, latitude) * float2(0.5 / PI, 1.0 / PI);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float2 uv = input.positionWS.xz / max(0.1, _WaveSize);
                half2 slope = Slope(uv + float2(t, t * 0.6)) + Slope(uv * 1.73 + float2(-t * 0.7, t * 1.1)) * 0.6h;
                half3 normal = normalize(half3(slope.x * _WaveStrength, 1.0h, slope.y * _WaveStrength));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = saturate(dot(normal, view));
                half fresnel = _Reflection + (1.0h - _Reflection) * pow(1.0h - facing, 3.0h);

                // Reflet du ciel : direction réfléchie, gardée au-dessus de l'horizon.
                half3 reflected = reflect(-view, normal);
                reflected.y = abs(reflected.y);
                half3 sky = SAMPLE_TEXTURE2D_LOD(_SkyTex, sampler_SkyTex, Panorama(reflected), 0).rgb * _SkyBoost;
                // Essai réaliste 4 : la sonde de reflets prise au-dessus de la rivière ajoute les berges, les arbres et le moulin.
                half3 probe = SAMPLE_TEXTURECUBE_LOD(_ReflectionCube, sampler_ReflectionCube, reflected, 0).rgb * _SkyBoost;
                sky = lerp(sky, probe, _ProbeWeight);

                // Corps de l'eau : plus sombre vue de haut, éclairé par le soleil et la lumière d'ambiance.
                Light sun = GetMainLight();
                half3 ambient = SampleSH(half3(0.0h, 1.0h, 0.0h));
                half3 body = lerp(_DeepColor.rgb, _Color.rgb, 1.0h - facing * 0.6h);
                body *= ambient + sun.color * saturate(sun.direction.y) * 0.5h;

                half3 halfway = normalize(sun.direction + view);
                half sparkle = pow(saturate(dot(normal, halfway)), 300.0h) * _Sparkle;

                half3 color = lerp(body, sky, fresnel) + sun.color * sparkle;
                color = MixFog(color, input.fogFactor);
                half alpha = saturate(lerp(_Opacity, 1.0h, fresnel) + sparkle);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
