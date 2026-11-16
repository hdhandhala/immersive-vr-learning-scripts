Shader "Custom/VRFloorGuide_URP"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.0, 1.0, 0.85, 1.0)
        _GlowColor ("Glow Color", Color) = (0.2, 1.0, 1.0, 1.0)
        _MainTex ("Flow Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}

        _Brightness ("Brightness", Range(0, 10)) = 2.5
        _Emission ("Emission", Range(0, 20)) = 4.0

        _ScrollSpeed ("Scroll Speed", Float) = 1.2
        _PulseSpeed ("Pulse Speed", Float) = 1.5
        _PulseStrength ("Pulse Strength", Range(0, 2)) = 0.35

        _CoreWidth ("Core Width", Range(0.01, 1)) = 0.22
        _EdgeSoftness ("Edge Softness", Range(0.001, 1)) = 0.28

        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.15
        _FlowTiling ("Flow Tiling", Float) = 4.0
        _NoiseTiling ("Noise Tiling", Float) = 2.0

        _Alpha ("Alpha", Range(0, 1)) = 0.95
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED
            #pragma multi_compile _ UNITY_STEREO_MULTIVIEW_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            float4 _BaseColor;
            float4 _GlowColor;
            float _Brightness;
            float _Emission;
            float _ScrollSpeed;
            float _PulseSpeed;
            float _PulseStrength;
            float _CoreWidth;
            float _EdgeSoftness;
            float _NoiseAmount;
            float _FlowTiling;
            float _NoiseTiling;
            float _Alpha;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 uv = IN.uv;

                float scroll = _Time.y * _ScrollSpeed;
                float2 flowUV = float2(uv.x, uv.y * _FlowTiling - scroll);
                float2 noiseUV = float2(uv.x * 2.0, uv.y * _NoiseTiling - scroll * 0.35);

                float flow = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, flowUV).r;
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;

                float centerDist = abs(uv.x - 0.5) * 2.0;
                float core = 1.0 - smoothstep(_CoreWidth, _CoreWidth + _EdgeSoftness, centerDist);

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed * 6.28318) * _PulseStrength;
                float sparkle = lerp(1.0, noise, _NoiseAmount);

                float intensity = core * flow * pulse * sparkle;

                float3 color = (_BaseColor.rgb * _Brightness + _GlowColor.rgb * _Emission) * intensity;
                float alpha = saturate(core * flow * _Alpha);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}