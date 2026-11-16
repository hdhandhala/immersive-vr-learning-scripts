Shader "Custom/VRButtonMaster"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.08, 0.10, 0.14, 0.95)
        _EdgeColor ("Edge Color", Color) = (0.0, 0.65, 1.0, 1.0)
        _GlowColor ("Glow Color", Color) = (0.0, 0.65, 1.0, 1.0)

        _CornerRadius ("Corner Radius", Range(0.01, 0.45)) = 0.18
        _EdgeThickness ("Edge Thickness", Range(0.001, 0.15)) = 0.025
        _GlowStrength ("Glow Strength", Range(0, 5)) = 1.0

        _HoverBoost ("Hover Boost", Range(0, 2)) = 0
        _PressedDarken ("Pressed Darken", Range(0, 1)) = 0

        _TopHighlightStrength ("Top Highlight Strength", Range(0, 2)) = 0.35
        _InnerShadowStrength ("Inner Shadow Strength", Range(0, 2)) = 0.25

        _WidthScale ("Width Scale", Range(0.2, 4)) = 1
        _HeightScale ("Height Scale", Range(0.2, 4)) = 1

        _Alpha ("Alpha", Range(0,1)) = 1
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
            Name "Forward"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED
            #pragma multi_compile _ UNITY_STEREO_MULTIVIEW_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _GlowColor;
                float _CornerRadius;
                float _EdgeThickness;
                float _GlowStrength;
                float _HoverBoost;
                float _PressedDarken;
                float _TopHighlightStrength;
                float _InnerShadowStrength;
                float _WidthScale;
                float _HeightScale;
                float _Alpha;
            CBUFFER_END

            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 uv = IN.uv;
                float2 p = (uv - 0.5) * 2.0;
                p.x *= _WidthScale;
                p.y *= _HeightScale;

                float2 bounds = float2(1.0, 1.0);
                float radius = saturate(_CornerRadius);
                float edge = max(_EdgeThickness, 0.0001);

                float d = sdRoundedBox(p, bounds - radius, radius);

                float inside = 1.0 - smoothstep(0.0, 0.01, d);
                float border = 1.0 - smoothstep(edge, edge + 0.02, abs(d));
                float glow = saturate(1.0 - smoothstep(0.0, 0.08, abs(d)));

                float topMask = saturate((uv.y - 0.35) / 0.65);
                float innerShade = saturate((0.65 - uv.y) / 0.65);

                half3 baseRgb = _BaseColor.rgb;

                // pressed darkens
                baseRgb *= (1.0 - _PressedDarken * 0.55);

                // subtle glossy top
                baseRgb += topMask * _TopHighlightStrength * 0.18;

                // subtle lower shadow
                baseRgb -= innerShade * _InnerShadowStrength * 0.12;

                // border and glow
                baseRgb += _EdgeColor.rgb * border;
                baseRgb += _GlowColor.rgb * glow * (_GlowStrength + _HoverBoost) * 0.35;

                half4 col = half4(baseRgb, _BaseColor.a * inside * _Alpha);
                col *= IN.color;

                return col;
            }
            ENDHLSL
        }
    }
}