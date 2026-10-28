// 屏幕后处理：暗角 + 胶片颗粒 + 扫描线 + 轻微色差 + 暖色调。
// Built-in Render Pipeline 的 OnRenderImage 专用；C# 端为 CameraPostFX。
Shader "Hidden/ScreenFX"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Vignette ("Vignette", Range(0, 1)) = 0.35
        _Grain ("Grain", Range(0, 0.3)) = 0.06
        _Scanlines ("Scanlines", Range(0, 0.3)) = 0.04
        _Aberration ("Aberration", Range(0, 0.01)) = 0.0008
        _Tint ("Tint", Color) = (1, 0.97, 0.9, 1)
        _TimeSeed ("Time Seed", Float) = 0
    }

    SubShader
    {
        // 全屏 Blit：不写深度、不做剔除
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // x/y = 1/宽高，z/w = 宽高（像素）

            float _Vignette;
            float _Grain;
            float _Scanlines;
            float _Aberration;
            float4 _Tint;
            float _TimeSeed;

            // 二维像素哈希：同一帧稳定，靠 _TimeSeed 让颗粒逐帧变化
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 centered = uv - 0.5;
                float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);

                // 1) 色差：沿径向偏移 R/B 通道，越靠边缘越明显，中心保持干净
                float2 radialDir = normalize(centered + 1e-5);
                float radialDist = length(centered * float2(aspect, 1.0));
                float2 offset = radialDir * _Aberration * (0.35 + radialDist * 1.6);

                float4 col;
                col.r = tex2D(_MainTex, uv + offset).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - offset).b;
                col.a = 1.0;

                // 2) 暖色调：轻微压蓝，让整体偏烛光暖黄
                col.rgb *= _Tint.rgb;

                // 3) 暗角：按宽高比归一到角落距离，中心不动、四角渐进变暗
                float2 cornerVec = float2(aspect, 1.0);
                float cornerDist = length(centered * 2.0 * cornerVec) / length(cornerVec);
                float vig = smoothstep(1.0 - _Vignette * 0.55, 1.0 + _Vignette * 0.25, cornerDist);
                col.rgb *= 1.0 - vig * _Vignette * 0.85;

                // 4) 扫描线：按屏幕像素行做 3 像素周期的明暗交替，幅度很小
                float scan = 0.5 + 0.5 * cos(uv.y * _ScreenParams.y * 2.0943951);
                col.rgb *= 1.0 - _Scanlines * scan;

                // 5) 胶片颗粒：按像素哈希叠加噪点，强度由 _Grain 控制
                float2 grainUV = uv * _ScreenParams.xy + _TimeSeed * float2(37.0, 17.0);
                float noise = hash21(floor(grainUV)) - 0.5;
                col.rgb = saturate(col.rgb + noise * _Grain);

                return col;
            }
            ENDCG
        }
    }

    FallBack Off
}