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

        // 湿渍与苔藓：让大块地面有冷暖/材质层次，而不是一片均匀石板
        _WetStrength ("Wet Strength", Range(0,1)) = 0.35
        _WetColor ("Wet Sheen Color", Color) = (0.42,0.55,0.75,1)
        _MossStrength ("Moss Strength", Range(0,1)) = 0.22
        _MossColor ("Moss Tint", Color) = (0.45,0.55,0.32,1)
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
                float _WetStrength;
                float4 _WetColor;
                float _MossStrength;
                float4 _MossColor;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

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

                // 湿渍：暗色污渍区域更容易积水，压低反照率并加冷色高光
                half wet = saturate(stain * 1.15) * _WetStrength;

                // 苔藓：低频噪声 + 污渍区域，给阴湿角落一点冷绿色
                half mossNoise = ValueNoise(worldUV * 0.42 + 13.7);
                half moss = smoothstep(0.55, 0.78, mossNoise)
                          * smoothstep(0.25, 0.75, stain)
                          * _MossStrength;

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

                // 湿渍压暗、苔藓染色；最后轻微提亮暗部，避免砖缝死黑
                albedo *= lerp(1.0, 0.90, wet);
                albedo = lerp(albedo, albedo * _MossColor.rgb, moss);
                albedo = pow(max(albedo, 1e-4), 0.92);

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

                // 湿面高光：只加在积水区域，用冷色模拟天光反射
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                half spec = pow(saturate(dot(normalWS, halfDir)), 72.0);
                color += _WetColor.rgb * (spec * wet * 0.5);

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
