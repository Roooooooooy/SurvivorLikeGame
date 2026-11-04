// 主角遮挡 X 光：只在“被前景几何挡住”的像素上画一层低透明度剪影。
// 与 HD2D/SpriteLit 配合使用：主体走正常深度，只有被墙面/障碍/大怪挡住的部分
// 由这个 pass（ZTest Greater）补一个轮廓，避免主体用 ZTest Always 直接盖在墙上。
Shader "HD2D/SpriteXRay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Silhouette Color", Color) = (0.42, 0.66, 1.0, 0.30)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
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
            ZTest Greater     // 只画深度测试失败的像素 = 被遮挡的部分
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
                clip(alpha - _Cutoff);

                // 剪影颜色固定，不受光照影响；透明度由材质决定
                half4 c = _Color * i.color;
                return half4(c.rgb, c.a);
            }
            ENDHLSL
        }
    }
}
