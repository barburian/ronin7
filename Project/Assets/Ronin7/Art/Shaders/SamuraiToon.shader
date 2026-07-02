// SamuraiToon — URP, Quest-friendly cel-shader.
// Unlit base albedo (texture * color * vertex color) + a hard 2-step ramp driven by main
// directional light dot product, plus an optional rim term. Single forward pass.
Shader "Ronin7/SamuraiToon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1,1,1,1)
        _ShadowTint("Shadow Tint", Color) = (0.55,0.58,0.7,1)
        _ShadowThreshold("Shadow Threshold", Range(-1,1)) = 0.0
        // Half-width of a smoothstep band around the threshold. 0 = the original hard
        // cel step (default, so existing materials are unchanged); >0 softens the terminator.
        _RampSmoothness("Ramp Smoothness", Range(0,1)) = 0.0
        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimPower("Rim Power", Range(0.5,16)) = 4.0
        _RimStrength("Rim Strength", Range(0,2)) = 0.0
        // Emission: black by default (no glow) so non-emissive materials are unaffected.
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

            // Main-light realtime shadows (+ cascades + soft variants). We sample shadow
            // attenuation but combine it as a hard cel-step (see frag) rather than
            // multiplying — preserves the 2-step ramp.
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
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 shadowCoord: TEXCOORD3;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowTint;
                float  _ShadowThreshold;
                float  _RampSmoothness;
                float4 _RimColor;
                float  _RimPower;
                float  _RimStrength;
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
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Albedo = texture * material color * vertex color (used by ships/enemies for panel demarcation).
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 albedo = tex * _BaseColor * IN.color;

                // Hard 2-step ramp from main directional light.
                Light mainLight = GetMainLight();
                float3 N = normalize(IN.normalWS);
                float NdotL = dot(N, mainLight.direction);
                // Hard cel-step for received shadows: any meaningfully shadowed pixel
                // is forced to the unlit side of the ramp. Avoids multiplying attenuation
                // into final color (would double-darken / break the 2-step look).
                float shadowAtten = MainLightRealtimeShadow(IN.shadowCoord);
                float effectiveNdotL = NdotL * step(0.5, shadowAtten);
                // Transition at threshold: 0 = full shadow tint, 1 = full lit. With
                // _RampSmoothness 0 the half-width collapses to ~1e-4 → effectively the
                // original hard cel step; larger values widen a smoothstep band.
                float halfW = max(_RampSmoothness, 1e-4);
                float litMask = smoothstep(_ShadowThreshold - halfW, _ShadowThreshold + halfW, effectiveNdotL);
                half3 lit = albedo.rgb * mainLight.color;
                half3 shadow = albedo.rgb * _ShadowTint.rgb;
                half3 color = lerp(shadow, lit, litMask);

                // Optional rim term (cheap fresnel against camera direction).
                if (_RimStrength > 0.0)
                {
                    float3 V = normalize(GetCameraPositionWS() - IN.positionWS);
                    float rim = pow(saturate(1.0 - saturate(dot(N, V))), _RimPower);
                    color += _RimColor.rgb * rim * _RimStrength;
                }

                // Emission (black by default → no-op). Map defaults to white, so a bare
                // _EmissionColor still glows uniformly without an authored map.
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb * _EmissionColor.rgb;
                color += emission;

                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        // Standard URP ShadowCaster pass — opaque only, no alpha clip.
        // Required so the toon-shaded geometry contributes to the main-light
        // shadow map (and to its own self-shadowing now that ForwardLit samples it).
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

            // Keep the per-material CBUFFER identical to ForwardLit so SRP batcher
            // stays compatible across passes. ShadowCasterPass.hlsl uses _BaseMap_ST
            // and _BaseColor symbols even when alpha clip is disabled.
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowTint;
                float  _ShadowThreshold;
                float  _RampSmoothness;
                float4 _RimColor;
                float  _RimPower;
                float  _RimStrength;
                float4 _EmissionColor;
            CBUFFER_END

            // URP 17 moved this from Shaders/Utils/ to Shaders/ — adjust if upgrading/downgrading.
            // The include declares _LightDirection and _LightPosition itself, so we don't.
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
