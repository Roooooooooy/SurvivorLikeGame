// HD-2D 地面：三层世界坐标采样，专门用来打破 512 贴图的整数平铺周期。
//   基础层：bg_stone_floor，约 13m 重复一次，保留大块石板。
//   细节层：bg_dungeon_floor，更细的砖纹，只在污渍区域露出。
//   宏观层：bg_grunge_large，约 60m 周期，做明暗/色温调制，破坏重复感。
// 全部按世界坐标取样，所以地面 Plane 的 scale 不会影响纹理尺度。
Shader "HD2D/Ground"
{
    Properties
    {
        _BaseTex ("Base Stone", 2D) = "white" {}
        _DetailTex ("Detail Stone", 2D) = "white" {}
        _MacroTex ("Macro Grunge", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _MacroTint ("Stain Tint", Color) = (0.72,0.69,0.64,1)
        _BaseScale ("Base Scale (per m)", Float) = 0.075
        _BroadScale ("Broad Scale (per m)", Float) = 0.021
        _DetailScale ("Detail Scale (per m)", Float) = 0.13
        _MacroScale ("Macro Scale (per m)", Float) = 0.016
        _BroadStrength ("Broad Strength", Range(0,1)) = 0.35
        _DetailStrength ("Detail Strength", Range(0,1)) = 0.28
        _MacroStrength ("Macro Strength", Range(0,1)) = 0.5
        _Ambient ("Ambient Floor", Range(0,2)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            TEXTURE2D(_BaseTex);   SAMPLER(sampler_BaseTex);
            TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);
            TEXTURE2D(_MacroTex);  SAMPLER(sampler_MacroTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MacroTint;
                float _BaseScale;
                float _BroadScale;
                float _DetailScale;
                float _MacroScale;
                float _BroadStrength;
                float _DetailStrength;
                float _MacroStrength;
                float _Ambient;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogCoord = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 worldUV = i.positionWS.xz;

                half3 baseCol   = SAMPLE_TEXTURE2D(_BaseTex,   sampler_BaseTex,   worldUV * _BaseScale).rgb;
                half3 broadCol  = SAMPLE_TEXTURE2D(_BaseTex,   sampler_BaseTex,   worldUV * _BroadScale).rgb;
                half3 detailCol = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, worldUV * _DetailScale).rgb;
                half3 macroCol  = SAMPLE_TEXTURE2D(_MacroTex,  sampler_MacroTex,  worldUV * _MacroScale).rgb;

                half macroLum = dot(macroCol, half3(0.299, 0.587, 0.114));
                half broadLum = dot(broadCol, half3(0.299, 0.587, 0.114));
                half stain = saturate(1.0 - macroLum);

                // 同一张贴图再做一个 47m 周期的低频明暗，只调制亮度、不叠加砖缝
                half broadShade = lerp(1.0 - _BroadStrength * 0.5, 1.0 + _BroadStrength * 0.5,
                    saturate(broadLum * 1.2));

                // 细节层以乘法方式叠加，只在污渍/粗糙区域增强，避免整片噪点
                half detailMask = saturate(stain * 1.4 + 0.35);
                half3 albedo = lerp(baseCol, baseCol * (0.70 + detailCol * 1.6), _DetailStrength * detailMask);

                // 大尺度明暗：暗部压暗、亮部略提亮，再用暖色污渍染色
                half macroShade = lerp(1.0, lerp(0.78, 1.22, saturate(macroLum * 1.15)), _MacroStrength);
                albedo *= macroShade * broadShade;
                albedo = lerp(albedo, albedo * _MacroTint.rgb, stain * _MacroStrength);
                albedo *= _BaseColor.rgb;

                float3 normalWS = normalize(i.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (ndotl * mainLight.shadowAttenuation);

                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0; li < lightCount; ++li)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    lighting += l.color * saturate(dot(normalWS, l.direction)) * l.distanceAttenuation;
                }
                #endif

                half3 ambient = SampleSH(normalWS) + _Ambient;
                lighting = max(lighting, ambient);

                half3 color = albedo * lighting;
                color = MixFog(color, i.fogCoord);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // 相机深度纹理需要地面参与，否则后处理/深度采样会缺地面
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
