Shader "Survivor/InstancedSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Frames ("Frame Count", Float) = 1
        _Rows ("Row Count", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.78, 0.58, 0.16, 1) // 精英/Boss 描边色（暗金）
        _OutlineWidth ("Outline Width (texel)", Float) = 1.25           // 描边宽度，单位为图集像素
        _DissolveEdge ("Dissolve Edge Color", Color) = (1, 0.55, 0.18, 1) // 溶解边缘的余烬色
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // Unity 自动填充：xy = 1/贴图宽高
            float _Frames;
            float _Rows;
            float4 _OutlineColor;
            float _OutlineWidth;
            float4 _DissolveEdge;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Frame)
                UNITY_DEFINE_INSTANCED_PROP(float, _Row)
                UNITY_DEFINE_INSTANCED_PROP(float, _Flash)
                UNITY_DEFINE_INSTANCED_PROP(float, _Outline)
                UNITY_DEFINE_INSTANCED_PROP(float, _Dissolve)
            UNITY_INSTANCING_BUFFER_END(Props)

            // 2D 哈希：把坐标打散成 [0,1] 伪随机值
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 值噪声：格点哈希 + 双线性平滑插值，溶解时呈块状而非逐像素噪点
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

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                // 图集帧动画：横向按帧等分、纵向按行等分（row0=行走，row1=死亡）
                float frame = UNITY_ACCESS_INSTANCED_PROP(Props, _Frame);
                float row = UNITY_ACCESS_INSTANCED_PROP(Props, _Row);
                o.uv = float2((v.uv.x + frame) / _Frames, (v.uv.y + row) / _Rows);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float flash = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _Flash));
                float outline = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _Outline));
                float dissolve = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _Dissolve));
                fixed4 tex = tex2D(_MainTex, i.uv);
                // 受击闪红：在自身轮廓内向纯红插值，保留透明形状
                fixed4 col = tex * tint;
                col.rgb = lerp(col.rgb, fixed3(1.0, 0.15, 0.1), flash);
                col.a = tex.a * tint.a;

                // 当前帧在图集中的矩形：描边采样与溶解噪声都必须限制在帧内，避免串帧
                float frames = max(1.0, _Frames);
                float rows = max(1.0, _Rows);
                float frame = floor(UNITY_ACCESS_INSTANCED_PROP(Props, _Frame) + 0.5);
                float row = floor(UNITY_ACCESS_INSTANCED_PROP(Props, _Row) + 0.5);
                float2 frameMin = float2(frame / frames, row / rows);
                float2 frameMax = float2((frame + 1.0) / frames, (row + 1.0) / rows);

                // 精英/Boss 描边：中心透明而四邻不透明处填充描边色（外扩一圈）
                if (outline > 0.0)
                {
                    float2 texel = _MainTex_TexelSize.xy * max(0.5, _OutlineWidth);
                    float2 uvL = clamp(i.uv - float2(texel.x, 0.0), frameMin, frameMax);
                    float2 uvR = clamp(i.uv + float2(texel.x, 0.0), frameMin, frameMax);
                    float2 uvD = clamp(i.uv - float2(0.0, texel.y), frameMin, frameMax);
                    float2 uvU = clamp(i.uv + float2(0.0, texel.y), frameMin, frameMax);
                    float neighborA = max(
                        max(tex2D(_MainTex, uvL).a, tex2D(_MainTex, uvR).a),
                        max(tex2D(_MainTex, uvD).a, tex2D(_MainTex, uvU).a));
                    float edge = step(tex.a, 0.3) * step(0.3, neighborA) * outline;
                    col.rgb = lerp(col.rgb, _OutlineColor.rgb, edge);
                    col.a = max(col.a, edge * _OutlineColor.a);
                }

                // 死亡溶解：帧内 UV + 帧号做噪声阈值裁剪，阈值附近叠加余烬色
                if (dissolve > 0.0)
                {
                    float2 frameSize = max(frameMax - frameMin, 1e-5);
                    float2 localUv = (i.uv - frameMin) / frameSize;
                    float noise = ValueNoise(localUv * 24.0 + float2(frame * 7.13, row * 3.71));
                    clip(noise - dissolve); // 噪声低于阈值：丢弃该像素
                    float edgeGlow = 1.0 - saturate((noise - dissolve) / 0.08);
                    col.rgb = lerp(col.rgb, _DissolveEdge.rgb, edgeGlow * _DissolveEdge.a);
                }

                return col;
            }
            ENDCG
        }
    }
}