// 地面辉光：平铺在 XZ 平面的加法混合圆盘，用于传送门/宝箱落点。
Shader "HD2D/GroundGlow"
{
    Properties
    {
        _Color ("Color", Color) = (0.6, 0.35, 1, 1)
        _Intensity ("Intensity", Float) = 1.5
        _Falloff ("Falloff", Float) = 2.0
        _Pulse ("Pulse", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }

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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Falloff;
                half _Pulse;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                if (r > 1.0) return half4(0, 0, 0, 0);

                float a = pow(saturate(1.0 - r), _Falloff);
                a *= 0.85 + 0.15 * sin(_Time.y * 2.0) * _Pulse;
                half3 col = _Color.rgb * _Intensity;
                return half4(col * a, a);
            }
            ENDHLSL
        }
    }
}
