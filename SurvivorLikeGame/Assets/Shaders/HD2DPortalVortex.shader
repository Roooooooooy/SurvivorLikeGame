// 裂隙传送门：面向相机的螺旋能量面。
// 同一条 Shader 用不同 _Seed / _Speed / 颜色叠两层，做出有纵深的漩涡。
Shader "HD2D/PortalVortex"
{
    Properties
    {
        _ColorInner ("Inner Color", Color) = (0.65, 0.35, 1, 1)
        _ColorOuter ("Outer Color", Color) = (0.15, 0.55, 1, 1)
        _Intensity ("Intensity", Float) = 2.5
        _Speed ("Swirl Speed", Float) = 1.6
        _Twist ("Twist", Float) = 2.2
        _NoiseScale ("Noise Scale", Float) = 7
        _RimPower ("Rim Boost", Float) = 1.4
        _Seed ("Seed", Float) = 0
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
            Name "Vortex"
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
                float4 _ColorInner;
                float4 _ColorOuter;
                float _Intensity;
                float _Speed;
                float _Twist;
                float _NoiseScale;
                float _RimPower;
                float _Seed;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

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
                p.y *= 0.80; // 椭圆：纵向拉长
                float r = length(p);
                if (r > 1.0) return half4(0, 0, 0, 0);

                float ang = atan2(p.y, p.x);
                float t = _Time.y;

                // 角度随半径扭曲，形成向内收束的螺旋。
                float swirl = ang + t * _Speed + (1.0 - r) * _Twist * 6.28318;

                float bandsA = sin(swirl * 3.0 + _Seed) * 0.5 + 0.5;
                bandsA = pow(bandsA, 1.6);
                float bandsB = sin(swirl * 5.0 - t * 0.7 + _Seed * 1.7) * 0.5 + 0.5;
                float n = valueNoise(p * _NoiseScale + float2(t * 0.35, -t * 0.22) + _Seed);
                float energy = saturate(bandsA * 0.65 + bandsB * 0.25 + n * 0.35);

                float core = smoothstep(0.02, 0.12, r);
                float rim = smoothstep(0.60, 1.0, r);
                float alpha = core * (1.0 - smoothstep(0.88, 1.0, r));
                alpha *= 0.50 + energy * 0.70;
                alpha = saturate(alpha * (0.80 + rim * 0.9));

                float3 col = lerp(_ColorInner.rgb, _ColorOuter.rgb, saturate(r * 1.15));
                col *= _Intensity * (0.55 + energy * 0.9);
                col += _ColorOuter.rgb * rim * _RimPower * _Intensity;

                return half4(col * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
