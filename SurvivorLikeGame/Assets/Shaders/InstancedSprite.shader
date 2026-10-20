Shader "Survivor/InstancedSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Frames ("Frame Count", Float) = 1
        _Rows ("Row Count", Float) = 1
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
            float _Frames;
            float _Rows;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Frame)
                UNITY_DEFINE_INSTANCED_PROP(float, _Row)
                UNITY_DEFINE_INSTANCED_PROP(float, _Flash)
            UNITY_INSTANCING_BUFFER_END(Props)

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
                fixed4 tex = tex2D(_MainTex, i.uv);
                // 受击闪红：在自身轮廓内向纯红插值，保留透明形状
                fixed4 col = tex * tint;
                col.rgb = lerp(col.rgb, fixed3(1.0, 0.15, 0.1), flash);
                col.a = tex.a * tint.a;
                return col;
            }
            ENDCG
        }
    }
}
