Shader "Custom/URP/ChromaKeyUnlit_VR_Quest"
{
    Properties
    {
        _MainTex    ("Video Texture", 2D) = "white" {}
        _KeyColor   ("Key Color", Color) = (0,1,0,1)
        _Threshold  ("Threshold", Range(0,1)) = 0.35
        _Smoothness ("Smoothness", Range(0.001,1)) = 0.10
        _Spill      ("Spill Suppression", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        LOD 100

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing

            // XR / single-pass support
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED
            #pragma multi_compile _ UNITY_STEREO_MULTIVIEW_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _KeyColor;
                half _Threshold;
                half _Smoothness;
                half _Spill;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                half3 key = _KeyColor.rgb;

                // Color distance from key
                half d = distance(col.rgb, key);

                // Alpha mask
                half alpha = smoothstep(_Threshold, _Threshold + max(_Smoothness, 0.001h), d);

                // Spill suppression
                half spillMask = saturate(1.0h - (d / max(_Threshold, 0.0001h)));
                col.g = lerp(col.g, min(col.r, col.b), spillMask * _Spill);

                col.a *= alpha;
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}