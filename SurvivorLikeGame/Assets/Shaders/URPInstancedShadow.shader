// 敌人脚下贴地椭圆阴影：一个 XZ 平面 quad，按实例缩放平铺。
// 不依赖阴影贴图/实时阴影，1000 个敌人总共只需 1~2 个实例化 draw call。
Shader "Survivor/URPInstancedShadow"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0.02, 0.015, 0.025, 0.55)
        _Softness ("Softness", Range(0.01, 1)) = 0.45
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
            Name "ForwardShadow"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Softness;
            CBUFFER_END

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

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // 圆形径向衰减，按实例矩阵缩放后就是椭圆贴地阴影
                float2 p = i.uv * 2.0 - 1.0;
                float d = length(p);
                half a = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
                a *= a;
                return half4(_Color.rgb, _Color.a * a);
            }
            ENDHLSL
        }
    }
}
