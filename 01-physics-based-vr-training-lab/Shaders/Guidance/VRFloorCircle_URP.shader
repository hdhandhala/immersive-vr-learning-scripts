Shader "Custom/VRFloorCircle_URP"
{
    Properties
    {
        _RingColor ("Ring Color", Color) = (0.2,1,1,1)
        _GlowColor ("Glow Color", Color) = (0.2,1,1,1)

        _FlowTex ("Flow Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}

        _Brightness ("Brightness", Range(0,10)) = 1.9
        _Emission ("Emission", Range(0,10)) = 0.9

        _ScrollSpeed ("Scroll Speed", Float) = 4
        _PulseSpeed ("Pulse Speed", Float) = 0.5
        _PulseStrength ("Pulse Strength", Range(0,2)) = 0.21
        _PulseEnabled ("Pulse Enabled", Range(0,1)) = 1

        _CoreWidth ("Core Width", Range(0.001,1)) = 0.22
        _EdgeSoftness ("Edge Softness", Range(0.001,1)) = 0.22
        _NoiseAmount ("Noise Amount", Range(0,1)) = 0.15

        _FlowTiling ("Flow Tiling", Float) = 4
        _NoiseTiling ("Noise Tiling", Float) = 2
        _Alpha ("Alpha", Range(0,1)) = 0.75
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

            TEXTURE2D(_FlowTex);
            SAMPLER(sampler_FlowTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

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

            float4 _RingColor;
            float4 _GlowColor;
            float _Brightness;
            float _Emission;
            float _ScrollSpeed;
            float _PulseSpeed;
            float _PulseStrength;
            float _PulseEnabled;
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

                float ringMask =
                    smoothstep(0.0, _EdgeSoftness, uv.x) *
                    (1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, uv.x));

                float core = 1.0 - abs(uv.x - 0.5) / max(_CoreWidth, 0.0001);
                core = saturate(core);
                core = smoothstep(0.0, 1.0, core);

                float2 flowUV = float2(uv.y * _FlowTiling - _Time.y * _ScrollSpeed, 0.5);
                float flow = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, flowUV).r;

                float2 noiseUV = float2(uv.y * _NoiseTiling + _Time.y * 0.15, uv.x * _NoiseTiling);
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                noise = lerp(1.0, noise, _NoiseAmount);

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed * 6.28318) * _PulseStrength;
                pulse = lerp(1.0, pulse, _PulseEnabled);

                float intensity = ringMask * (core * 0.65 + flow * 0.35) * noise * pulse;

                float3 color = (_RingColor.rgb * _Brightness + _GlowColor.rgb * _Emission) * intensity;
                float alpha = saturate(ringMask * intensity * _Alpha + ringMask * 0.15);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}