// GrittyCyber — URP, Quest-friendly "Gritty Cyber-Fantasy" hero shader.
// Real PBR lighting (metallic/smoothness/normal/occlusion) for worn steel & scuffed leather,
// PLUS an animated HDR emissive edge (scrolling mask + fresnel rim, optional pulse) that pushes
// through the existing Bloom threshold for neon circuitry / armor seams. One lit forward pass +
// ShadowCaster + DepthNormals (so PC SSAO sees the surface). Single-Pass-Instanced for VR stereo,
// mirroring the boilerplate in CyberNeon/SamuraiToon. Dynamic GI via SH (hero rigs aren't lightmapped).
Shader "Ronin7/GrittyCyber"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo (worn metal/leather)", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Tint", Color) = (1,1,1,1)
        _MetallicGlossMap("Metallic(R) Smoothness(A)", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 0.0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _BumpMap("Normal (scuffs/forge marks)", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0,2)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1.0
        // HDR so values >1 push through Bloom's threshold (0.9). This is the neon.
        [HDR] _EmissionColor("Neon Edge Color", Color) = (0,0,0,1)
        _EmissionMap("Edge Mask (R)", 2D) = "white" {}
        _EdgeFresnel("Edge Fresnel Power", Range(0.5,16)) = 4.0
        _EdgeSpeed("Energy Flow Speed", Range(0,8)) = 0.0
        _EdgePulse("Edge Pulse Amount", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float  _Metallic;
            float  _Smoothness;
            float  _BumpScale;
            float  _OcclusionStrength;
            float4 _EmissionColor;
            float  _EdgeFresnel;
            float  _EdgeSpeed;
            float  _EdgePulse;
        CBUFFER_END
        ENDHLSL

        // ---------- Forward lit ----------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);           SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MetallicGlossMap);  SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_BumpMap);           SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap);      SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);       SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3; // xyz dir, w sign
                float  fogCoord   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS   = n.normalWS;
                OUT.tangentWS  = float4(n.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord   = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                half4 mg     = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, IN.uv);
                half  occ    = LerpWhiteTo(SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, IN.uv).g, _OcclusionStrength);

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _BumpScale);
                float sgn = IN.tangentWS.w;
                float3 bitangentWS = sgn * cross(IN.normalWS, IN.tangentWS.xyz);
                half3x3 tbn = half3x3(IN.tangentWS.xyz, bitangentWS, IN.normalWS);
                half3 normalWS = normalize(mul(normalTS, tbn));

                // Animated neon edge: scrolling mask OR fresnel rim, optionally pulsed.
                float2 flowUV = IN.uv + float2(_Time.y * _EdgeSpeed, 0.0);
                half mask = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, flowUV).r;
                float3 V = normalize(GetCameraPositionWS() - IN.positionWS);
                half fres = pow(saturate(1.0 - saturate(dot(normalWS, V))), _EdgeFresnel);
                half pulse = 1.0 - _EdgePulse * (0.5 - 0.5 * sin(_Time.y * 6.0));
                half3 emission = _EmissionColor.rgb * max(mask, fres) * pulse;

                SurfaceData surface = (SurfaceData)0;
                surface.albedo     = albedo.rgb;
                surface.metallic   = mg.r * _Metallic;
                surface.smoothness = mg.a * _Smoothness;
                surface.normalTS   = normalTS;
                surface.occlusion  = occ;
                surface.emission   = emission;
                surface.alpha      = 1.0;

                InputData inputData = (InputData)0;
                inputData.positionWS  = IN.positionWS;
                inputData.normalWS    = normalWS;
                inputData.viewDirectionWS = V;
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord    = IN.fogCoord;
                inputData.bakedGI     = SampleSH(normalWS) * occ;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask  = half4(1,1,1,1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // ---------- Shadow caster ----------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct SAtt { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SVar { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            SVar shadowVert(SAtt IN)
            {
                SVar OUT = (SVar)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
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
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 shadowFrag(SVar IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ---------- Depth normals (feeds PC SSAO / depth-normals prepass) ----------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex dnVert
            #pragma fragment dnFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct DAtt { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DVar { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 tangentWS:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            DVar dnVert(DAtt IN)
            {
                DVar OUT = (DVar)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.positionCS = p.positionCS;
                OUT.normalWS   = n.normalWS;
                OUT.tangentWS  = float4(n.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 dnFrag(DVar IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _BumpScale);
                float sgn = IN.tangentWS.w;
                float3 bitangentWS = sgn * cross(IN.normalWS, IN.tangentWS.xyz);
                half3x3 tbn = half3x3(IN.tangentWS.xyz, bitangentWS, IN.normalWS);
                half3 normalWS = normalize(mul(normalTS, tbn));
                return half4(NormalizeNormalPerPixel(normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
