// DojoTech — URP, Quest-friendly base surface shader for environment / character bodies.
// Stylized flat/low-poly look that READS baked lightmaps (and light probes) so static dojo
// geometry can be lit cheaply by a bake instead of realtime lights. Main directional light is
// added as a quantized (banded) term for a cel-flat feel; optional derivative flat-shading
// gives a faceted low-poly silhouette. Single forward pass + shadow caster.
// Carries the same Single-Pass-Instanced VR boilerplate as SamuraiToon.
Shader "Ronin7/DojoTech"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (0.5,0.55,0.62,1)
        _ShadowTint("Shadow Tint", Color) = (0.18,0.20,0.30,1)
        // How strongly baked GI / probes contribute. 1 = physically-ish, lower = flatter.
        _GIBoost("Baked GI Boost", Range(0,2)) = 1.0
        // Quantization steps for the realtime main-light term. 1 = single flat band (most stylized).
        _Bands("Light Bands", Range(1,4)) = 2
        _LightStrength("Main Light Strength", Range(0,2)) = 0.6
        [Toggle(_FLATSHADING)] _FlatShading("Faceted Flat Shading", Float) = 0
        // Subtle self-illum panels (armor tech lines). Black by default = no glow.
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        _EmissionMap("Emission Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _FLATSHADING

            // Baked GI variants.
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            // Main-light realtime shadow receiving (combined as a hard step, see frag).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 shadowCoord: TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowTint;
                float  _GIBoost;
                float  _Bands;
                float  _LightStrength;
                float4 _EmissionColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = n.normalWS;
                OUT.shadowCoord = TransformWorldToShadowCoord(p.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUTPUT_LIGHTMAP_UV(IN.lightmapUV, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH(OUT.normalWS, OUT.vertexSH);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;

                // Face normal from derivatives gives a faceted low-poly read when enabled.
                #if defined(_FLATSHADING)
                    float3 N = normalize(cross(ddy(IN.positionWS), ddx(IN.positionWS)));
                #else
                    float3 N = normalize(IN.normalWS);
                #endif

                // Baked GI (lightmap or probe SH). This is the primary light source.
                half3 bakedGI = SAMPLE_GI(IN.lightmapUV, IN.vertexSH, N) * _GIBoost;

                // Realtime main light, quantized into flat bands and hard-stepped by its shadow.
                Light mainLight = GetMainLight();
                float shadowAtten = MainLightRealtimeShadow(IN.shadowCoord);
                float ndl = saturate(dot(N, mainLight.direction)) * step(0.5, shadowAtten);
                float bands = max(_Bands, 1.0);
                float banded = floor(ndl * bands) / bands;
                half3 directional = mainLight.color * banded * _LightStrength;

                half3 lighting = bakedGI + directional;
                // Floor the result with the shadow tint so unlit areas read stylized, not black.
                lighting = max(lighting, _ShadowTint.rgb);
                half3 color = albedo * lighting;

                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb * _EmissionColor.rgb;
                color += emission;

                return half4(color, tex.a * _BaseColor.a);
            }
            ENDHLSL
        }

        // Standard URP ShadowCaster — lets DojoTech geometry cast into the main-light shadow map.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowTint;
                float  _GIBoost;
                float  _Bands;
                float  _LightStrength;
                float4 _EmissionColor;
            CBUFFER_END

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
