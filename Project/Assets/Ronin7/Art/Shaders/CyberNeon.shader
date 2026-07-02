// CyberNeon — URP, Quest-friendly emissive accent shader.
// Unlit core color + HDR emission (drives Bloom) + optional fresnel edge-glow and pulse.
// For swords, glowing signs, visors and cyborg armor seams. No lighting / no shadow sampling
// so neon stays bright against dark backgrounds and stays cheap (single forward pass).
// Mirrors SamuraiToon's Single-Pass-Instanced VR boilerplate so it renders correctly in stereo.
Shader "Ronin7/CyberNeon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Core Color", Color) = (0,0.05,0.08,1)
        // HDR so values >1 push through Bloom's threshold. This is the neon glow.
        [HDR] _EmissionColor("Emission Color", Color) = (0,4,6,1)
        _EmissionMap("Emission Map", 2D) = "white" {}
        // Fresnel edge-glow: brightens silhouette so thin blades / seams read at distance.
        [HDR] _FresnelColor("Fresnel Color", Color) = (0,2,3,1)
        _FresnelPower("Fresnel Power", Range(0.5,16)) = 3.0
        _FresnelStrength("Fresnel Strength", Range(0,4)) = 1.0
        // Optional emission pulse. Amount 0 (default) = steady glow, no _Time cost worth noting.
        _PulseSpeed("Pulse Speed", Range(0,12)) = 0.0
        _PulseAmount("Pulse Amount", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _FresnelColor;
                float  _FresnelPower;
                float  _FresnelStrength;
                float  _PulseSpeed;
                float  _PulseAmount;
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
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 core = tex.rgb * _BaseColor.rgb;

                // Steady emission, optionally pulsed. sin remapped to [1-amount, 1].
                half pulse = 1.0 - _PulseAmount * (0.5 - 0.5 * sin(_Time.y * _PulseSpeed));
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb
                                 * _EmissionColor.rgb * pulse;

                // Fresnel rim against camera — cheap edge glow on the silhouette.
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetCameraPositionWS() - IN.positionWS);
                float fres = pow(saturate(1.0 - saturate(dot(N, V))), _FresnelPower);
                half3 rim = _FresnelColor.rgb * fres * _FresnelStrength;

                half3 color = core + emission + rim;
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
