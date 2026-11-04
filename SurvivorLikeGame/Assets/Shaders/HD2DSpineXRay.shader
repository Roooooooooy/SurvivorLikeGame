// Spine 主角遮挡 X 光：与 HD2D/SpriteXRay 同样的思路，
// 直接用 Spine 生成的网格（UV + 顶点色），只在被前景挡住的位置画低透明度剪影。
// 主体仍由 Spine/Skeleton 正常深度绘制，不再需要 ZTest Always。
Shader "HD2D/SpineXRay"
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
            "Queue" = "Transparent+1"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "XRay"
            Tags { "LightMode" = "UniversalForward" }
            ZTest Greater

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
                // Spine 网格的顶点色带槽位 alpha，用它保证剪影形状与主体一致
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
                clip(alpha - _Cutoff);

                half4 c = _Color * i.color;
                return half4(c.rgb, c.a);
            }
            ENDHLSL
        }
    }
}
