Shader "Custom/BatterySpawnReveal_URP"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)

        _Reveal ("Reveal", Range(0,1)) = 0
        _FXProgress ("FX Progress", Range(0,1)) = 1

        _ShellColor ("Shell Color", Color) = (0.2, 1.0, 0.95, 1.0)
        _ShellWidth ("Shell Width", Range(0.0, 0.03)) = 0.006

        _TileScale ("Tile Scale", Float) = 24
        _CornerRadius ("Corner Radius", Range(0.01, 0.49)) = 0.18
        _TileSoftness ("Tile Softness", Range(0.001, 0.2)) = 0.05
        _InflateStrength ("Inflate Strength", Range(0, 2)) = 1.0

        _WaveSpeed ("Wave Speed", Float) = 1.5
        _WaveFrequency ("Wave Frequency", Float) = 4.0
        _WaveAmplitude ("Wave Amplitude", Float) = 0.08
        _SecondaryWaveAmplitude ("Secondary Wave Amplitude", Float) = 0.04

        _EdgeGlow ("Edge Glow", Range(0, 8)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "SpawnShell"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ _STEREO_INSTANCING_ON
            #pragma multi_compile _ _STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float3 viewDirWS  : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShellColor;
                float _Reveal;
                float _FXProgress;
                float _ShellWidth;

                float _TileScale;
                float _CornerRadius;
                float _TileSoftness;
                float _InflateStrength;

                float _WaveSpeed;
                float _WaveFrequency;
                float _WaveAmplitude;
                float _SecondaryWaveAmplitude;

                float _EdgeGlow;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 expandedOS = IN.positionOS.xyz + normalize(IN.normalOS) * (_ShellWidth * _FXProgress);
                VertexPositionInputs posInputs = GetVertexPositionInputs(float4(expandedOS, 1.0));
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.positionOS = expandedOS;
                OUT.normalWS = normalize(nrmInputs.normalWS);
                OUT.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);

                return OUT;
            }

            float roundedBoxSDF(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float t = _Time.y * _WaveSpeed;
                float2 uv = IN.positionOS.xz;

                float waveA = sin(uv.x * _WaveFrequency + t);
                float waveB = cos(uv.y * (_WaveFrequency * 0.9) - t * 1.2);
                float waveC = sin((uv.x + uv.y) * (_WaveFrequency * 0.65) + t * 0.7);

                float2 warpedUV = uv;
                warpedUV.x += waveB * _WaveAmplitude + waveC * _SecondaryWaveAmplitude;
                warpedUV.y += waveA * _WaveAmplitude + waveC * _SecondaryWaveAmplitude;

                float2 gridUV = frac(warpedUV * _TileScale) - 0.5;

                float radius = _CornerRadius;
                float2 halfSize = float2(0.5 - radius, 0.5 - radius);

                float sdf = roundedBoxSDF(gridUV, halfSize, radius);

                float tileMask = 1.0 - smoothstep(0.0, _TileSoftness, sdf);

                float centerDist = saturate(length(gridUV) / 0.5);
                float dome = pow(1.0 - centerDist, 1.8);

                float inner = saturate(1.0 - sdf / max(_TileSoftness, 0.0001));
                float puff = saturate(max(dome, inner)) * _InflateStrength;

                float ripple = (waveA + waveB + waveC) * 0.3333;
                float waveLight = saturate(0.55 + ripple * 0.5);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float fres = pow(1.0 - saturate(dot(N, V)), 2.0);

                float alpha = tileMask * _FXProgress;
                alpha *= lerp(0.65, 1.0, puff);
                alpha *= lerp(0.5, 1.0, fres);

                float3 col = _ShellColor.rgb;

                col *= lerp(0.45, 1.35, puff);
                col *= lerp(0.8, 1.2, waveLight);
                col *= (1.0 + fres * _EdgeGlow);

                return half4(col, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "MainReveal"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ _STEREO_INSTANCING_ON
            #pragma multi_compile _ _STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Reveal;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS);
                OUT.positionCS = posInputs.positionCS;
                OUT.uv = IN.uv;
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                float alpha = smoothstep(0.0, 1.0, _Reveal);
                float directional = saturate((_Reveal * 1.2) - (IN.positionOS.y * 0.15));
                alpha *= smoothstep(0.0, 1.0, directional + 0.2);

                tex.a *= alpha;
                return tex;
            }
            ENDHLSL
        }
    }

    FallBack Off
}