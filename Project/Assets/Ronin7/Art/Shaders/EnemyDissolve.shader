// EnemyDissolve — P5-03 death dissolve. Simple lit forward pass (main light + SH ambient) with an
// alpha-clip noise dissolve and an HDR emissive edge. Only dying enemies are swapped onto it
// (DeathDissolve), so the clip cost is paid by at most a few renderers. _Dissolve 0..1 is driven by a
// MaterialPropertyBlock together with _BaseMap/_BaseColor copied from the enemy's original material.
Shader "Ronin7/EnemyDissolve"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Base Tint", Color) = (1,1,1,1)
        _Dissolve("Dissolve", Range(0,1)) = 0
        [HDR] _EdgeColor("Edge Color", Color) = (4,0.6,2.2,1)
        _EdgeWidth("Edge Width", Range(0.01,0.3)) = 0.12
        _NoiseScale("Noise Scale", Float) = 24
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Dissolve;
                float4 _EdgeColor;
                float  _EdgeWidth;
                float  _NoiseScale;
            CBUFFER_END

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float n = ValueNoise(IN.uv * _NoiseScale);
                // Map _Dissolve 0..1 so 0 keeps everything and 1 clips everything (edge band included).
                float t = _Dissolve * (1.0 + _EdgeWidth);
                clip(n - t);
                float edge = 1.0 - saturate((n - t) / _EdgeWidth);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                Light l = GetMainLight();
                half3 nrm = normalize(IN.normalWS);
                half3 lit = albedo.rgb * (l.color * saturate(dot(nrm, l.direction)) + SampleSH(nrm));
                return half4(lit + _EdgeColor.rgb * edge * step(0.001, _Dissolve), 1);
            }
            ENDHLSL
        }
    }
}
