Shader "Custom/HologramTwoColorDots_URP"
{
    Properties
    {
        _Blue ("Blue Color", Color) = (0.0, 0.6, 1.0, 1)
        _Green("Green Color", Color) = (0.0, 1.0, 0.3, 1)

        _Alpha ("Base Alpha", Range(0,1)) = 0.43
        _Intensity ("Intensity", Float) = 2.0

        _DotCount ("Dot Density", Float) = 55.0
        _DotRadius ("Dot Radius", Range(0.01,0.5)) = 0.16
        _DotSoftness ("Dot Softness", Range(0.0001,0.2)) = 0.02

        _MoveSpeed ("Move Speed", Float) = 0.25
        _MoveAmount ("Move Amount (inside cell)", Range(0,0.45)) = 0.22
        _EnableMotion ("Enable Motion (0/1)", Range(0,1)) = 1

        _BlueGreenMix ("Blue vs Green (0..1)", Range(0,1)) = 0.5

        _FadeTop ("Fade Top", Range(0,1)) = 1.0
        _FadeBottom ("Fade Bottom", Range(0,1)) = 0.2

        _RimPower ("Rim Power", Range(0.5,8)) = 3.0
        _RimStrength ("Rim Strength", Range(0,2)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

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

            // VR stereo safety
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED
            #pragma multi_compile _ UNITY_STEREO_MULTIVIEW_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 posWS       : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
                float2 uv          : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Blue;
                float4 _Green;

                float _Alpha;
                float _Intensity;

                float _DotCount;
                float _DotRadius;
                float _DotSoftness;

                float _MoveSpeed;
                float _MoveAmount;
                float _EnableMotion;

                float _BlueGreenMix;

                float _FadeTop;
                float _FadeBottom;

                float _RimPower;
                float _RimStrength;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 randDir(float2 cellId)
            {
                float a = hash21(cellId + 3.33) * 6.2831853;
                return float2(cos(a), sin(a));
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.posWS);

                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(OUT.posWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimStrength;
                float fade = saturate(lerp(_FadeBottom, _FadeTop, IN.uv.y));

                float2 p = IN.uv * _DotCount;
                float2 cellId = floor(p);
                float2 cellUV = frac(p) - 0.5;

                // decide dot color per cell (blue or green)
                float pick = hash21(cellId + 9.17); // 0..1
                float3 dotColor = (pick < _BlueGreenMix) ? _Blue.rgb : _Green.rgb;

                // optional random motion inside cell (never leaves cell -> no collisions)
                float2 offset = 0;
                if (_EnableMotion > 0.5)
                {
                    float2 dir = randDir(cellId);
                    float phase = hash21(cellId + 17.13);
                    float t = (_Time.y * _MoveSpeed) + phase * 10.0;
                    offset = dir * (sin(t) * _MoveAmount);
                }

                float2 local = cellUV - offset;
                float d = length(local);

                float dotMask = 1.0 - smoothstep(_DotRadius, _DotRadius + _DotSoftness, d);

                float3 col = dotColor * _Intensity;
                float alpha = _Alpha * fade * dotMask;

                // subtle rim to sell hologram
                col += col * rim;
                alpha = saturate(alpha + rim * 0.12);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}