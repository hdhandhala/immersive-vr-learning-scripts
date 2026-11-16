Shader "Custom/BlueGreenWavyLines_URP_VR_Quest"
{
    Properties
    {
        _ColorBlue ("Blue Color", Color) = (0.0, 0.6, 1.0, 1)
        _ColorGreen ("Green Color", Color) = (0.0, 1.0, 0.3, 1)

        _LineCount ("Number of Lines", Float) = 18.0
        _LineWidth ("Line Thickness", Range(0.001, 0.2)) = 0.04
        _EdgeSoftness ("Edge Softness", Range(0.0001, 0.1)) = 0.01

        _WaveAmplitude ("Wave Amplitude", Range(0,0.5)) = 0.10
        _WaveFrequency ("Wave Frequency", Float) = 10.0
        _WaveSpeed ("Wave Speed", Float) = 0.7

        _Intensity ("Color Intensity", Float) = 2.5
        _Alpha ("Line Alpha", Range(0,1)) = 1.0

        _Direction ("Direction (X=U, Y=V)", Vector) = (1,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

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
                float4 positionHCS : SV_POSITION;
                half2 uv           : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorBlue;
                half4 _ColorGreen;

                half _LineCount;
                half _LineWidth;
                half _EdgeSoftness;

                half _WaveAmplitude;
                half _WaveFrequency;
                half _WaveSpeed;

                half _Intensity;
                half _Alpha;

                half4 _Direction;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = (half2)IN.uv;
                return OUT;
            }

            half LineMask(half x, half width, half soft)
            {
                half d = abs(frac(x) - 0.5h);
                half halfW = width * 0.5h;
                return 1.0h - smoothstep(halfW, halfW + max(soft, 0.0001h), d);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half2 dir = normalize(_Direction.xy + half2(0.00001h, 0.0h));

                half baseCoord = dot(IN.uv, dir) * _LineCount;
                half travelCoord = dot(IN.uv, half2(-dir.y, dir.x));

                half wave =
                    sin(travelCoord * _WaveFrequency * 6.2831853h + _Time.y * _WaveSpeed)
                    * _WaveAmplitude;

                half coordBlue = baseCoord + wave;
                half coordGreen = baseCoord - wave;

                half mBlue = LineMask(coordBlue, _LineWidth, _EdgeSoftness);
                half mGreen = LineMask(coordGreen, _LineWidth, _EdgeSoftness);

                half alpha = saturate((mBlue + mGreen) * _Alpha);
                half3 col = (mBlue * _ColorBlue.rgb + mGreen * _ColorGreen.rgb) * _Intensity;

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}