Shader "Custom/DrillHole_URP_VR_Quest"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.28, 0.24, 0.20, 1)
        _RimColor ("Rim Color", Color) = (0.58, 0.48, 0.38, 1)
        _SpiralColor ("Spiral Color", Color) = (0.20, 0.18, 0.16, 1)
        _CenterColor ("Center Color", Color) = (0.03, 0.03, 0.03, 1)
        _DustColor ("Dust Color", Color) = (0.72, 0.62, 0.50, 1)

        _MainTex ("Base Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 0.45

        _HoleRadius ("Hole Radius", Range(0.01,1)) = 0.36
        _HoleSoftness ("Hole Softness", Range(0.001,0.5)) = 0.08

        _CenterDepthRadius ("Center Depth Radius", Range(0.001,1)) = 0.22
        _CenterDepthStrength ("Center Depth Strength", Range(0,3)) = 1.65

        _SpiralStrength ("Spiral Strength", Range(0,2)) = 1.0
        _SpiralFrequency ("Spiral Frequency", Float) = 22.0
        _SpiralTightness ("Spiral Tightness", Float) = 18.0
        _SpiralContrast ("Spiral Contrast", Range(0.1,8)) = 2.8

        _SpiralBandCenter ("Spiral Band Center", Range(0,1)) = 0.24
        _SpiralBandWidth ("Spiral Band Width", Range(0.01,1)) = 0.18
        _SpiralBandBoost ("Spiral Band Boost", Range(0,3)) = 1.5

        _RimWidth ("Rim Width", Range(0.001,0.5)) = 0.10
        _RimSoftness ("Rim Softness", Range(0.001,0.5)) = 0.06
        _RimBrightness ("Rim Brightness", Range(0,3)) = 1.2

        _DustWidth ("Dust Width", Range(0.001,0.5)) = 0.08
        _DustSoftness ("Dust Softness", Range(0.001,0.5)) = 0.12
        _DustStrength ("Dust Strength", Range(0,2)) = 0.55

        _InnerShadowStrength ("Inner Shadow Strength", Range(0,3)) = 1.25
        _DepthGradientPower ("Depth Gradient Power", Range(0.1,6)) = 2.0

        _Smoothness ("Smoothness", Range(0,1)) = 0.28
        _SpecularBoost ("Specular Boost", Range(0,2)) = 0.25

        _Progress ("Progress", Range(0,1)) = 1.0
        _Alpha ("Alpha", Range(0,1)) = 1.0
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
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half3 positionWS   : TEXCOORD1;
                half3 normalWS     : TEXCOORD2;
                half3 tangentWS    : TEXCOORD3;
                half3 bitangentWS  : TEXCOORD4;
                half3 viewDirWS    : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half4 _SpiralColor;
                half4 _CenterColor;
                half4 _DustColor;

                half _NormalStrength;

                half _HoleRadius;
                half _HoleSoftness;

                half _CenterDepthRadius;
                half _CenterDepthStrength;

                half _SpiralStrength;
                half _SpiralFrequency;
                half _SpiralTightness;
                half _SpiralContrast;

                half _SpiralBandCenter;
                half _SpiralBandWidth;
                half _SpiralBandBoost;

                half _RimWidth;
                half _RimSoftness;
                half _RimBrightness;

                half _DustWidth;
                half _DustSoftness;
                half _DustStrength;

                half _InnerShadowStrength;
                half _DepthGradientPower;

                half _Smoothness;
                half _SpecularBoost;

                half _Progress;
                half _Alpha;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = (half3)posInputs.positionWS;
                OUT.normalWS = normalize((half3)normInputs.normalWS);
                OUT.tangentWS = normalize((half3)normInputs.tangentWS);
                OUT.bitangentWS = normalize((half3)normInputs.bitangentWS);
                OUT.viewDirWS = (half3)GetWorldSpaceViewDir(posInputs.positionWS);
                OUT.uv = IN.uv;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half2 uv = IN.uv;
                half2 centered = uv * 2.0h - 1.0h;

                half r = length(centered);
                half angle = atan2(centered.y, centered.x);

                half holeMask = 1.0h - smoothstep(_HoleRadius, _HoleRadius + _HoleSoftness, r);
                holeMask *= _Progress;

                clip(holeMask - 0.01h);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                half4 normalTex = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
                half3 normalTS = UnpackNormal(normalTex);
                normalTS.xy *= _NormalStrength;
                normalTS = normalize(normalTS);

                half3x3 tbn = half3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );

                half3 normalWS = normalize(mul(normalTS, tbn));
                half3 viewDir = normalize(IN.viewDirWS);

                half centerDepth = 1.0h - smoothstep(0.0h, _CenterDepthRadius, r);
                centerDepth = pow(saturate(centerDepth), _DepthGradientPower);
                centerDepth *= _CenterDepthStrength;

                half spiralCoord = angle * _SpiralFrequency + (1.0h - r) * _SpiralTightness;
                half spiralWave = 0.5h + 0.5h * sin(spiralCoord);
                spiralWave = pow(saturate(spiralWave), _SpiralContrast);

                half bandDist = abs(r - _SpiralBandCenter);
                half bandMask = 1.0h - smoothstep(0.0h, _SpiralBandWidth, bandDist);
                bandMask = saturate(bandMask * _SpiralBandBoost);

                spiralWave *= bandMask;
                spiralWave *= holeMask;
                spiralWave *= _SpiralStrength;

                half rim = smoothstep(_HoleRadius - _RimWidth - _RimSoftness, _HoleRadius - _RimWidth, r);
                rim *= 1.0h - smoothstep(_HoleRadius - _RimSoftness, _HoleRadius + _RimSoftness, r);
                rim = saturate(rim) * _RimBrightness;

                half dustOuter = smoothstep(_HoleRadius, _HoleRadius + _DustSoftness + _DustWidth, r);
                half dustInner = smoothstep(_HoleRadius, _HoleRadius + _DustSoftness, r);
                half dustRing = saturate(dustOuter - dustInner) * _DustStrength;

                half innerShadow = saturate(centerDepth * _InnerShadowStrength);

                half3 col = tex.rgb * _BaseColor.rgb;
                col = lerp(col, _SpiralColor.rgb, spiralWave);
                col = lerp(col, _CenterColor.rgb, innerShadow);
                col = lerp(col, _RimColor.rgb, rim);
                col = lerp(col, _DustColor.rgb, dustRing);

                Light mainLight = GetMainLight();
                half3 lightDir = normalize((half3)mainLight.direction);
                half3 lightColor = (half3)mainLight.color;

                half NdotL = saturate(dot(normalWS, lightDir));
                half3 diffuse = col * lightColor * NdotL;

                half3 halfVec = normalize(lightDir + viewDir);
                half NdotH = saturate(dot(normalWS, halfVec));
                half specPow = lerp(8.0h, 64.0h, _Smoothness);
                half specular = pow(NdotH, specPow) * _Smoothness * _SpecularBoost;

                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDir)), 3.0h);
                half3 fresnelCol = fresnel * rim * _RimColor.rgb * 0.15h;

                half3 ambient = col * 0.18h;
                half3 finalCol = ambient + diffuse + (specular * lightColor) + fresnelCol;

                half alpha = holeMask * _Alpha;
                return half4(finalCol, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}