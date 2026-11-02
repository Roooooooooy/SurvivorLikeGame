// HD-2D 移轴：按屏幕 Y 到焦线的距离做高斯模糊。
// 中间一条清晰带，上下逐渐虚化（八方旅人那种微缩模型感）。
// 必须复用 core 的 Blit.hlsl：RenderGraph 的 AddBlitPass 用全屏三角形（SV_VertexID），
// 自己写 POSITION + TransformObjectToHClip 的顶点会被裁到屏幕外，pass 等于没画。
Shader "Hidden/HD2D/TiltShift"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "TiltShift"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _FocusY;
            float _FocusBand;
            float _Falloff;
            float _MaxBlur;
            float _Curve;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float d = abs(uv.y - _FocusY);
                float blur = saturate((d - _FocusBand) / max(_Falloff, 1e-4));
                blur = pow(blur, _Curve);
                float radius = blur * _MaxBlur;

                if (radius < 0.01)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float4 col = 0;
                float wsum = 0;

                // 5x5 高斯核，半径随屏幕 Y 距离缩放
                [unroll]
                for (int y = -2; y <= 2; y++)
                {
                    [unroll]
                    for (int x = -2; x <= 2; x++)
                    {
                        float2 offset = float2(x, y) * _BlitTexture_TexelSize.xy * radius;
                        float w = exp(-(x * x + y * y) / 4.0);
                        col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset) * w;
                        wsum += w;
                    }
                }

                return col / wsum;
            }
            ENDHLSL
        }
    }
}
