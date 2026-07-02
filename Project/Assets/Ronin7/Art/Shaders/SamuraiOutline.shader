// SamuraiOutline — inverted-hull outline. Cull Front + vertex push along object-space normal.
// Cheap on Quest (no screen-space cost). Intended use: as a SECOND material on the same mesh
// renderer (assigned to a duplicated mesh slot) so a single SubShader Pass is enough here.
// Agents adding outlines should add this material as element [1] on the target renderer.
Shader "Ronin7/SamuraiOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.02,0.02,0.02,1)
        _OutlineWidth("Outline Width (object-space)", Range(0,0.1)) = 0.01
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        LOD 100

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS     : POSITION;
                float3 normalOS       : NORMAL;
                // Smoothed normals baked into UV2 by LowPolyMeshes. Flat-shaded meshes have
                // per-face normals that split at hard edges and crack the inverted hull; extruding
                // along the position-smoothed normal keeps the outline watertight. Meshes without
                // a UV2 channel read zeros and fall back to normalOS below.
                float3 smoothNormalOS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float3 n = dot(IN.smoothNormalOS, IN.smoothNormalOS) > 1e-4
                         ? normalize(IN.smoothNormalOS)
                         : normalize(IN.normalOS);
                float3 pushed = IN.positionOS.xyz + n * _OutlineWidth;
                OUT.positionCS = TransformObjectToHClip(pushed);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
