// HD-2D 用的精灵着色器：URP Lit + Alpha Clip + ShadowCaster。
// 像素精灵用 alpha clip（而不是半透明混合），这样才能写深度并投射真实阴影。
Shader "HD2D/SpriteLit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _AmbientBoost ("Ambient Boost", Range(0,2)) = 0.35
        // 角色专属冷色描边；宽度为 0 时不做任何额外采样（其它精灵保持原样）
        _OutlineColor ("Outline Color", Color) = (0.38,0.56,0.78,1)
        _OutlineWidth ("Outline Width (texel)", Range(0,2)) = 0
        // 深度状态：主角主体保持正常深度，被遮挡时由 HD2D/SpriteXRay 的剪影补可见性
        _ZTest ("ZTest", Float) = 4
        _ZWrite ("ZWrite", Float) = 1
        // 默认不透明；溶解材质会在运行时切成 SrcAlpha / OneMinusSrcAlpha
        _SrcBlend ("Src Blend", Float) = 1
        _DstBlend ("Dst Blend", Float) = 0
        // 死亡/消失：0=完整，1=完全消失
        _Dissolve ("Dissolve", Range(0,1)) = 0
        [Toggle] _DissolveGold ("Golden Dissolve", Float) = 0
        _DissolveEdgeColor ("Dissolve Edge Color", Color) = (0.65, 0.62, 0.58, 1)
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.2)) = 0.06
        _DissolveWind ("Dissolve Wind (xy)", Vector) = (-0.10, 0.38, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Cutoff;
                half _AmbientBoost;
                half4 _OutlineColor;
                half _OutlineWidth;
                half _Dissolve;
                half _DissolveGold;
                half4 _DissolveEdgeColor;
                half _DissolveEdgeWidth;
                float4 _DissolveWind;
                half _SrcBlend;
                half _DstBlend;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionOS = v.positionOS.xyz;
                // 原地溶解：不做整体飘移或膨胀。
                VertexPositionInputs pos = GetVertexPositionInputs(positionOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                // billboard 精灵：法线正对相机，受光方向随视角变化
                o.normalWS = -normalize(GetWorldSpaceViewDir(pos.positionWS));
                return o;
            }

            // 3 层 value noise：溶解边缘是不规则团块，比棋盘抖动自然。
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 c = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(c);
                float b = Hash21(c + float2(1.0, 0.0));
                float d = Hash21(c + float2(0.0, 1.0));
                float e = Hash21(c + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(d, e, f.x), f.y);
            }

            float FBM(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    v += amp * ValueNoise(p);
                    p *= 2.03;
                    amp *= 0.5;
                }
                return v / 0.875;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color * i.color;

                float dissolveMask = 1.0;
                float dissolveAlpha = 1.0;
                float goldEdge = 0.0;
                if (_Dissolve > 0.0001)
                {
                    float edge = max(0.001, _DissolveEdgeWidth);
                    if (_DissolveGold > 0.5)
                    {
                        // 主角：固定在原始像素上的团块逐步溶解，亮金边沿着边界推进。
                        float2 pixel = floor(i.uv * _MainTex_TexelSize.zw);
                        float n = FBM(pixel * 0.14);
                        float threshold = lerp(-edge, 1.0 + edge, _Dissolve);
                        dissolveMask = smoothstep(threshold - edge, threshold + edge, n);
                        dissolveAlpha = dissolveMask;
                        goldEdge = saturate(1.0 - abs(n - threshold) / edge);
                    }
                    else
                    {
                        // 灰烬消散仍使用细粒度噪声和整体淡出。
                        float2 noiseUV = i.uv * _MainTex_TexelSize.zw * 0.45;
                        float n = FBM(noiseUV);
                        dissolveMask = saturate((n - _Dissolve) / edge);
                        dissolveAlpha = dissolveMask * (1.0 - _Dissolve);
                    }
                }

                // 外轮廓冷色描边：宽为 0 的材质（哥布林/柱子）完全不参与额外采样
                if (_Dissolve <= 0.0001 && tex.a < _Cutoff && _OutlineWidth > 0.001)
                {
                    float2 t = _MainTex_TexelSize.xy * _OutlineWidth;
                    half n =
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(t.x, 0)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - float2(t.x, 0)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0, t.y)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - float2(0, t.y)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + t).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - t).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(t.x, -t.y)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-t.x, t.y)).a;
                    if (n > _Cutoff * 2.0)
                        return half4(_OutlineColor.rgb * (0.35 + _AmbientBoost * 0.4), 1.0);
                }
                if (_Dissolve > 0.0001)
                    tex.a *= dissolveAlpha;
                else
                    clip(tex.a - _Cutoff);

                float3 normalWS = normalize(i.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);

                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half shadow = lerp(1.0, mainLight.shadowAttenuation, 0.7);
                half3 lighting = mainLight.color * (ndotl * shadow);

                // 附加点光（火盆）
                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0; li < lightCount; ++li)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    half nl = saturate(dot(normalWS, l.direction));
                    lighting += l.color * (nl * l.distanceAttenuation);
                }
                #endif

                // 环境光 + 保底提亮，避免背光面死黑
                half3 ambient = SampleSH(normalWS) + _AmbientBoost;
                lighting = max(lighting, ambient);

                // 溶解中的残留像素去色成灰烬，保留原始明暗细节；alpha 负责软淡出。
                half3 lit = tex.rgb * lighting;
                if (_DissolveGold > 0.5)
                {
                    half3 gilded = lerp(lit, _DissolveEdgeColor.rgb * 0.42,
                        smoothstep(0.0, 1.0, _Dissolve) * 0.45);
                    // 自发光边缘不受场景阴影压暗，在倒地轮廓上保留金色亮边。
                    return half4(gilded + _DissolveEdgeColor.rgb * goldEdge * 1.4, tex.a);
                }
                half lum = dot(lit, half3(0.299, 0.587, 0.114));
                half3 ash = lerp(half3(lum, lum, lum), _DissolveEdgeColor.rgb * 0.75, 0.35);
                half3 color = lerp(lit, ash, smoothstep(0.0, 1.0, _Dissolve));
                color = lerp(_DissolveEdgeColor.rgb, color, dissolveMask);
                return half4(color, tex.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Cutoff;
                half _AmbientBoost;
                half4 _OutlineColor;
                half _OutlineWidth;
                half _Dissolve;
                half _DissolveGold;
                half4 _DissolveEdgeColor;
                half _DissolveEdgeWidth;
                float4 _DissolveWind;
                half _SrcBlend;
                half _DstBlend;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(float3(0, 0, -1));

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                clip(tex.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // 深度预通道（URP 需要，否则深度纹理/Screen Space 后处理会缺角色）
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Cutoff;
                half _AmbientBoost;
                half4 _OutlineColor;
                half _OutlineWidth;
                half _Dissolve;
                half _DissolveGold;
                half4 _DissolveEdgeColor;
                half _DissolveEdgeWidth;
                float4 _DissolveWind;
                half _SrcBlend;
                half _DstBlend;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                clip(tex.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
