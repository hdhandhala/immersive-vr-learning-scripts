Shader "Custom/BatteryGridGuide_RectShape_Quest"
{
    Properties
    {
        _ChipColor ("Chip Color", Color) = (0,1,0.7,1)
        _GlowIntensity ("Glow Intensity", Float) = 1.5

        _ChipCount ("Chips Per Axis", Float) = 12
        _ChipSize ("Chip Size", Range(0.05,0.9)) = 0.6
        _CornerRadius ("Corner Radius", Range(0.01,0.3)) = 0.12

        _PulseSpeed ("Pulse Speed", Float) = 1.5
        _ScrollSpeed ("Scroll Speed", Float) = 0.05

        _FootprintSize ("Footprint Size XY", Vector) = (0.45, 0.35, 0, 0)
        _EdgeFeather ("Edge Feather", Range(0.001,0.1)) = 0.03
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
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ChipColor;
                half _GlowIntensity;
                half _ChipCount;
                half _ChipSize;
                half _CornerRadius;
                half _PulseSpeed;
                half _ScrollSpeed;
                half4 _FootprintSize;
                half _EdgeFeather;
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

            half sdRoundedRect(half2 p, half2 b, half r)
            {
                half2 q = abs(p) - b + r;
                return length(max(q, half2(0.0h, 0.0h))) + min(max(q.x, q.y), 0.0h) - r;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half2 uv = IN.uv;
                uv.y += _Time.y * _ScrollSpeed;

                half2 gridUV = frac(uv * _ChipCount);
                half2 centered = gridUV - 0.5h;

                half2 chipSize = half2(_ChipSize * 0.5h, _ChipSize * 0.5h);
                half chipDist = sdRoundedRect(centered, chipSize, _CornerRadius);
                half chip = smoothstep(0.02h, 0.0h, chipDist);

                half pulse = 0.7h + 0.3h * sin(_Time.y * _PulseSpeed * 6.2831h);
                half glow = chip * pulse * _GlowIntensity;

                half2 shapeCenter = IN.uv - 0.5h;
                half footprintDist = sdRoundedRect(shapeCenter, _FootprintSize.xy, 0.18h);
                half shapeMask = saturate(1.0h - smoothstep(0.0h, _EdgeFeather, footprintDist));

                half alpha = saturate(chip * shapeMask);
                half3 col = _ChipColor.rgb * glow;

                return half4(col, alpha * _ChipColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}