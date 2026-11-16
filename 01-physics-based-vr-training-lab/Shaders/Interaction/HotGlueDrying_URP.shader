Shader "Custom/HotGlueDrying_URP_VR_Quest"
{
    Properties
    {
        [MainTexture] _WetBaseMap ("Wet Base Map", 2D) = "white" {}
        _DryBaseMap ("Dry Base Map", 2D) = "white" {}

        _WetTint ("Wet Tint", Color) = (1.0, 0.99, 0.96, 1.0)
        _DryTint ("Dry Tint", Color) = (0.93, 0.91, 0.87, 1.0)

        _WetNormalMap ("Wet Normal Map", 2D) = "bump" {}
        _DryNormalMap ("Dry Normal Map", 2D) = "bump" {}

        _WetNormalStrength ("Wet Normal Strength", Range(0,2)) = 0.55
        _DryNormalStrength ("Dry Normal Strength", Range(0,2)) = 0.25

        _WetAlpha ("Wet Alpha", Range(0,1)) = 0.50
        _DryAlpha ("Dry Alpha", Range(0,1)) = 0.86

        _WetSmoothness ("Wet Smoothness", Range(0,1)) = 0.92
        _DrySmoothness ("Dry Smoothness", Range(0,1)) = 0.38

        _DryProgress ("Dry Progress", Range(0,1)) = 0.0

        _FresnelWet ("Fresnel Wet", Range(0.1,8)) = 3.4
        _FresnelDry ("Fresnel Dry", Range(0.1,8)) = 1.8

        _SpecularBoost ("Specular Boost", Range(0,2)) = 1.05
        _AmbientBoost ("Ambient Boost", Range(0,2)) = 1.0
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

            TEXTURE2D(_WetBaseMap);
            SAMPLER(sampler_WetBaseMap);

            TEXTURE2D(_DryBaseMap);
            SAMPLER(sampler_DryBaseMap);

            TEXTURE2D(_WetNormalMap);
            SAMPLER(sampler_WetNormalMap);

            TEXTURE2D(_DryNormalMap);
            SAMPLER(sampler_DryNormalMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _WetTint;
                half4 _DryTint;

                half _WetNormalStrength;
                half _DryNormalStrength;

                half _WetAlpha;
                half _DryAlpha;

                half _WetSmoothness;
                half _DrySmoothness;

                half _DryProgress;

                half _FresnelWet;
                half _FresnelDry;

                half _SpecularBoost;
                half _AmbientBoost;
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

                half dryT = saturate(_DryProgress);

                half4 wetBase = SAMPLE_TEXTURE2D(_WetBaseMap, sampler_WetBaseMap, IN.uv);
                half4 dryBase = SAMPLE_TEXTURE2D(_DryBaseMap, sampler_DryBaseMap, IN.uv);

                half4 baseTex = lerp(wetBase, dryBase, dryT);
                half4 tint = lerp(_WetTint, _DryTint, dryT);

                half wetAlpha = wetBase.a * _WetAlpha;
                half dryAlpha = dryBase.a * _DryAlpha;
                half alpha = lerp(wetAlpha, dryAlpha, dryT);

                half smoothness = lerp(_WetSmoothness, _DrySmoothness, dryT);

                half4 wetNormalTex = SAMPLE_TEXTURE2D(_WetNormalMap, sampler_WetNormalMap, IN.uv);
                half4 dryNormalTex = SAMPLE_TEXTURE2D(_DryNormalMap, sampler_DryNormalMap, IN.uv);

                half3 wetNormalTS = UnpackNormal(wetNormalTex);
                half3 dryNormalTS = UnpackNormal(dryNormalTex);

                wetNormalTS.xy *= _WetNormalStrength;
                dryNormalTS.xy *= _DryNormalStrength;

                half3 normalTS = normalize(lerp(wetNormalTS, dryNormalTS, dryT));

                half3x3 tbn = half3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );

                half3 normalWS = normalize(mul(normalTS, tbn));
                half3 viewDir = normalize(IN.viewDirWS);

                Light mainLight = GetMainLight();
                half3 lightDir = normalize((half3)mainLight.direction);
                half3 lightColor = (half3)mainLight.color;

                half3 albedo = baseTex.rgb * tint.rgb;

                half NdotL = saturate(dot(normalWS, lightDir));
                half3 diffuse = albedo * lightColor * NdotL;

                half3 halfVec = normalize(lightDir + viewDir);
                half NdotH = saturate(dot(normalWS, halfVec));

                half specPow = lerp(8.0h, 96.0h, smoothness);
                half specular = pow(NdotH, specPow) * smoothness * _SpecularBoost;

                half fresnelPower = lerp(_FresnelWet, _FresnelDry, dryT);
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDir)), fresnelPower);
                half3 fresnelCol = fresnel * albedo * 0.18h;

                half3 ambient = albedo * 0.18h * _AmbientBoost;

                half3 finalCol = ambient + diffuse + (specular * lightColor) + fresnelCol;

                return half4(finalCol, saturate(alpha * tint.a));
            }
            ENDHLSL
        }
    }

    FallBack Off
}