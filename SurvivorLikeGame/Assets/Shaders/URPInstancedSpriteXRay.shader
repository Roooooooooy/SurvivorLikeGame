// 敌人遮挡 XRay：复用 Survivor/URPInstancedSprite 的实例矩阵和图集帧数据，
// 只把被前景几何挡住的部分画成单色剪影。和主角 XRay 用同一种 ZTest Greater 思路，
// 但颜色独立，便于区分。
Shader "Survivor/URPInstancedSpriteXRay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Frames ("Frame Count", Float) = 1
        _Rows ("Row Count", Float) = 1
        _TrimRect ("Content Rect", Vector) = (0, 0, 1, 1)
        _XRayColor ("XRay Color", Color) = (1, 0.42, 0.12, 0.40)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "XRay"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZTest Greater
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

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
                half _Cutoff;
                float _Frames;
                float _Rows;
                float4 _TrimRect;
                half4 _XRayColor;
            CBUFFER_END

            // 实例属性布局必须和 Survivor/URPInstancedSprite 完全一致，
            // 否则同一个 MaterialPropertyBlock 在第二个材质上可能绑不到 _Frame/_Row。
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 与主 shader 相同的块状溶解噪声
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

                float frame = UNITY_ACCESS_INSTANCED_PROP(Props, _Frame);
                float row = UNITY_ACCESS_INSTANCED_PROP(Props, _Row);
                float2 cellUV = _TrimRect.xy + v.uv * _TrimRect.zw;
                o.uv = float2((cellUV.x + frame) / max(1.0, _Frames),
                              (cellUV.y + row) / max(1.0, _Rows));
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(tex.a - _Cutoff);

                float dissolve = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _Dissolve));
                if (dissolve > 0.0)
                {
                    float frame = floor(UNITY_ACCESS_INSTANCED_PROP(Props, _Frame) + 0.5);
                    float row = floor(UNITY_ACCESS_INSTANCED_PROP(Props, _Row) + 0.5);
                    float frames = max(1.0, _Frames);
                    float rows = max(1.0, _Rows);
                    float2 frameMin = float2(frame / frames, row / rows);
                    float2 frameMax = float2((frame + 1.0) / frames, (row + 1.0) / rows);
                    float2 frameSize = max(frameMax - frameMin, 1e-5);
                    float2 localUv = (i.uv - frameMin) / frameSize;
                    float noise = ValueNoise(localUv * 24.0 + float2(frame * 7.13, row * 3.71));
                    clip(noise - dissolve);
                }

                return half4(_XRayColor.rgb, _XRayColor.a);
            }
            ENDHLSL
        }
    }
}
