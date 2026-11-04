// HD-2D 敌人实例化精灵：URP Unlit + Alpha Clip。
// 像素精灵用 alpha clip 而不是半透明混合，这样能写深度、按世界深度正确遮挡，
// 同时保持 unlit 的低开销；光照用屏幕空间/竖向渐变伪造（不采样实时光、不投影）。
Shader "Survivor/URPInstancedSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Frames ("Frame Count", Float) = 1
        _Rows ("Row Count", Float) = 1
        // 单帧内不透明内容的归一化矩形 (x, y, w, h)，用来裁掉透明留白、减少 overdraw
        _TrimRect ("Content Rect", Vector) = (0, 0, 1, 1)
        _OutlineColor ("Outline Color", Color) = (0.78, 0.58, 0.16, 1)
        _OutlineWidth ("Outline Width (texel)", Float) = 1.25
        _DissolveEdge ("Dissolve Edge Color", Color) = (1, 0.55, 0.18, 1)
        _FakeLightColor ("Fake Light Color", Color) = (1.0, 0.94, 0.86, 1)
        _AmbientColor ("Ambient Color", Color) = (0.42, 0.46, 0.55, 1)
        _FakeLightDir ("Fake Light Dir (xy)", Vector) = (0.35, 0.7, 0, 0)
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
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half _Cutoff;
                float _Frames;
                float _Rows;
                float4 _TrimRect;
                half4 _OutlineColor;
                float _OutlineWidth;
                half4 _DissolveEdge;
                half4 _FakeLightColor;
                half4 _AmbientColor;
                float4 _FakeLightDir;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Frame)
                UNITY_DEFINE_INSTANCED_PROP(float, _Row)
                UNITY_DEFINE_INSTANCED_PROP(float, _Flash)
                UNITY_DEFINE_INSTANCED_PROP(float, _Outline)
                UNITY_DEFINE_INSTANCED_PROP(float, _Dissolve)
            UNITY_INSTANCING_BUFFER_END(Props)

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
                float2 localUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 2D 哈希 + 值噪声：溶解呈块状而不是逐像素噪点
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

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;

                // 图集帧动画：横向按帧等分、纵向按行等分（row0=行走，row1=死亡）
                float frame = UNITY_ACCESS_INSTANCED_PROP(Props, _Frame);
                float row = UNITY_ACCESS_INSTANCED_PROP(Props, _Row);
                // 四边形已经收紧到内容矩形，这里把 0..1 的 uv 映射回单元格内的内容区域
                float2 cellUV = _TrimRect.xy + v.uv * _TrimRect.zw;
                o.uv = float2((cellUV.x + frame) / max(1.0, _Frames),
                              (cellUV.y + row) / max(1.0, _Rows));
                o.localUV = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float flash = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _Flash));
                float outline = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _Outline));
                float dissolve = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _Dissolve));

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half alpha = tex.a * tint.a;

                // 当前帧在图集中的矩形：描边采样与溶解噪声都必须限制在帧内，避免串帧
                float frames = max(1.0, _Frames);
                float rows = max(1.0, _Rows);
                float frame = floor(UNITY_ACCESS_INSTANCED_PROP(Props, _Frame) + 0.5);
                float row = floor(UNITY_ACCESS_INSTANCED_PROP(Props, _Row) + 0.5);
                float2 frameMin = float2(frame / frames, row / rows);
                float2 frameMax = float2((frame + 1.0) / frames, (row + 1.0) / rows);

                // 精英/Boss 描边：透明像素但四邻不透明时填充描边色。
                // 必须在 clip 之前算，并把边缘像素的 alpha 提到 1，否则会先被裁掉。
                half edge = 0.0;
                if (outline > 0.0)
                {
                    float2 texel = _MainTex_TexelSize.xy * max(0.5, _OutlineWidth);
                    float2 uvL = clamp(i.uv - float2(texel.x, 0.0), frameMin, frameMax);
                    float2 uvR = clamp(i.uv + float2(texel.x, 0.0), frameMin, frameMax);
                    float2 uvD = clamp(i.uv - float2(0.0, texel.y), frameMin, frameMax);
                    float2 uvU = clamp(i.uv + float2(0.0, texel.y), frameMin, frameMax);
                    half neighborA = max(
                        max(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvL).a,
                            SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvR).a),
                        max(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvD).a,
                            SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvU).a));
                    edge = step(alpha, 0.3) * step(0.3, neighborA) * outline;
                }

                half finalAlpha = max(alpha, edge * _OutlineColor.a);
                clip(finalAlpha - _Cutoff);

                // 假光照：底暗顶亮的竖向梯度 + 极轻的屏幕空间侧光，不采样任何实时光。
                float2 centered = i.localUV - 0.5;
                float2 lightDir = normalize(_FakeLightDir.xy + float2(1e-4, 1e-4));
                half side = saturate(dot(centered, lightDir) * 0.5 + 0.5);
                half vertical = saturate(i.localUV.y);
                half3 lighting = _AmbientColor.rgb
                               + _FakeLightColor.rgb * (0.45 + 0.55 * vertical)
                               + _FakeLightColor.rgb * (side - 0.5) * 0.18;
                lighting = max(lighting, half3(0.12, 0.12, 0.14));

                half3 rgb = tex.rgb * tint.rgb * lighting;

                // 受击闪红：在自身轮廓内向纯红插值
                rgb = lerp(rgb, half3(1.0, 0.15, 0.1), flash);

                // 描边覆盖颜色
                rgb = lerp(rgb, _OutlineColor.rgb, saturate(edge));

                // 死亡溶解：帧内 UV + 帧号做噪声阈值裁剪，阈值附近叠加余烬色
                if (dissolve > 0.0)
                {
                    float2 frameSize = max(frameMax - frameMin, 1e-5);
                    float2 localUv = (i.uv - frameMin) / frameSize;
                    float noise = ValueNoise(localUv * 24.0 + float2(frame * 7.13, row * 3.71));
                    clip(noise - dissolve);
                    float edgeGlow = 1.0 - saturate((noise - dissolve) / 0.08);
                    rgb = lerp(rgb, _DissolveEdge.rgb, edgeGlow * _DissolveEdge.a);
                }

                // alpha clip 模式下 alpha 恒为 1，形状完全由 clip 决定
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
