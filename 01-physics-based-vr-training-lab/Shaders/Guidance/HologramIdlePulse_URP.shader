Shader "Custom/HologramPulseSquareSegmentsSmooth_URP_XR"
{
    Properties
    {
        _PulseColor ("Pulse Color", Color) = (0.35, 1, 1, 1)
        _BgColor ("Background Color", Color) = (0, 0.08, 0.12, 0.04)

        _Speed ("Speed", Range(0.1, 5)) = 0.8
        _MaxSize ("Max Size", Range(0.1, 1.5)) = 1.0
        _LineWidth ("Line Width", Range(0.001, 0.2)) = 0.035
        _Softness ("Line Softness", Range(0.001, 0.1)) = 0.02
        _CenterSize ("Center Size", Range(0.001, 0.2)) = 0.012

        _SegmentCount ("Segments Per Side", Range(2, 30)) = 8
        _SegmentFill ("Segment Fill", Range(0.05, 1.0)) = 0.38
        _SegmentSoftness ("Segment Softness", Range(0.001, 0.2)) = 0.08

        _WaveCount ("Wave Count", Range(1, 4)) = 3
        _TrailFade ("Trail Fade", Range(0.1, 4.0)) = 1.5
        _Glow ("Glow", Range(0, 4)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            // XR / stereo support
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED
            #pragma multi_compile _ UNITY_STEREO_MULTIVIEW_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            CBUFFER_START(UnityPerMaterial)
                half4 _PulseColor;
                half4 _BgColor;
                half _Speed;
                half _MaxSize;
                half _LineWidth;
                half _Softness;
                half _CenterSize;
                half _SegmentCount;
                half _SegmentFill;
                half _SegmentSoftness;
                half _WaveCount;
                half _TrailFade;
                half _Glow;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half SquareDist(half2 p)
            {
                return max(abs(p.x), abs(p.y));
            }

            half SquareRing(half dist, half radius, half width, half soft)
            {
                half inner = smoothstep(radius - width - soft, radius - width, dist);
                half outer = 1.0h - smoothstep(radius, radius + soft, dist);
                return saturate(inner * outer);
            }

            half SegmentMaskSmooth(half2 uv, half radius, half segmentCount, half segmentFill, half segmentSoftness)
            {
                half ax = abs(uv.x);
                half ay = abs(uv.y);

                half sidePos;

                if (ax > ay)
                {
                    sidePos = uv.y / max(radius, 0.0001h) * 0.5h + 0.5h;
                }
                else
                {
                    sidePos = uv.x / max(radius, 0.0001h) * 0.5h + 0.5h;
                }

                sidePos = saturate(sidePos);

                half cell = frac(sidePos * segmentCount);
                half seg = 1.0h - smoothstep(segmentFill, segmentFill + segmentSoftness, cell);
                return saturate(seg);
            }

            half EaseOut(half t)
            {
                return 1.0h - pow(1.0h - t, 2.0h);
            }

            half PulseLayer(half2 uv, half timeOffset)
            {
                half rawT = frac(_Time.y * _Speed + timeOffset);
                half t = EaseOut(rawT);

                half radius = lerp(0.0h, _MaxSize, t);

                half d = SquareDist(uv);
                half ring = SquareRing(d, radius, _LineWidth, _Softness);
                half seg = SegmentMaskSmooth(uv, radius, _SegmentCount, _SegmentFill, _SegmentSoftness);

                half lifeFade = pow(1.0h - rawT, _TrailFade);

                return ring * seg * lifeFade;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half2 uv = IN.uv * 2.0h - 1.0h;
                half d = SquareDist(uv);

                half pulse = 0.0h;

                pulse += PulseLayer(uv, 0.0h);

                if (_WaveCount >= 2.0h)
                    pulse += PulseLayer(uv, 0.33h);

                if (_WaveCount >= 3.0h)
                    pulse += PulseLayer(uv, 0.66h);

                if (_WaveCount >= 4.0h)
                    pulse += PulseLayer(uv, 0.16h);

                pulse = saturate(pulse);

                half center = 1.0h - smoothstep(_CenterSize, _CenterSize + 0.02h, d);

                half3 col = _BgColor.rgb;
                col += _PulseColor.rgb * pulse * _Glow;
                col += _PulseColor.rgb * center * 0.25h;

                half alpha = _BgColor.a + pulse * 0.95h + center * 0.18h;
                alpha = saturate(alpha);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}