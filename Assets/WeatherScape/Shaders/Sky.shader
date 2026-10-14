// Procedural sky for URP: gradient, sun and moon discs, stars, drifting fbm clouds (coverage 0..1), lightning flash.
Shader "WeatherScape/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.25, 0.45, 0.80, 1)
        _HorizonColor ("Horizon", Color) = (0.70, 0.80, 0.90, 1)
        _GroundColor ("Below horizon", Color) = (0.35, 0.37, 0.40, 1)
        _SunDir ("Sun direction", Vector) = (0, 0.5, 0.8, 0)
        _SunColor ("Sun colour", Color) = (1, 0.95, 0.85, 1)
        _SunSize ("Sun size", Range(0.0005, 0.05)) = 0.006
        _MoonDir ("Moon direction", Vector) = (0, 0.5, -0.8, 0)
        _MoonColor ("Moon colour", Color) = (0.8, 0.85, 1, 1)
        _Stars ("Stars", Range(0, 1)) = 0
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.3
        _CloudColor ("Cloud lit colour", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud shade colour", Color) = (0.55, 0.6, 0.68, 1)
        _CloudScale ("Cloud scale", Float) = 0.9
        _CloudSpeed ("Cloud speed", Float) = 0.01
        _WindDir ("Wind direction (x,z)", Vector) = (1, 0.3, 0, 0)
        _Flash ("Lightning flash", Range(0, 2)) = 0
        _Exposure ("Exposure", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _MoonColor, _CloudColor, _CloudShade;
                float4 _SunDir, _MoonDir, _WindDir;
                float _SunSize, _Stars, _CloudCover, _CloudScale, _CloudSpeed, _Flash, _Exposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.dir = IN.positionOS.xyz;
                return o;
            }

            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int i = 0; i < 5; i++) { v += a * Noise(p); p = p * 2.03 + float2(1.7, 9.2); a *= 0.5; }
                return v;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.dir);
                float up = saturate(d.y);
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, 0.5));
                if (d.y < 0) col = lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-d.y * 5.0));

                float3 sunDir = normalize(_SunDir.xyz);
                float sd = dot(d, sunDir);
                float sunUp = saturate(sunDir.y * 6.0 + 0.6);
                float cloudBlock = 1.0 - _CloudCover * 0.9;
                col += _SunColor.rgb * (pow(saturate(sd), 10.0) * 0.35 + pow(saturate(sd), 200.0) * 0.6) * sunUp * cloudBlock;
                float disk = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.5, sd);
                col += _SunColor.rgb * disk * 6.0 * sunUp * saturate(1.0 - _CloudCover * 1.1);

                float3 moonDir = normalize(_MoonDir.xyz);
                float md = dot(d, moonDir);
                float moon = smoothstep(0.99955, 0.99975, md);
                col += _MoonColor.rgb * (moon * 2.5 + pow(saturate(md), 400.0) * 0.25) * saturate(1.0 - _CloudCover * 1.05) * saturate(moonDir.y * 8.0);

                if (_Stars > 0.001 && d.y > 0)
                {
                    float2 uv = float2(atan2(d.x, d.z), asin(d.y)) * 160.0;
                    float2 cell = floor(uv);
                    float h = Hash(cell);
                    float2 c = frac(uv) - 0.5;
                    float star = step(0.992, h) * saturate(1.0 - length(c) * 4.0);
                    star *= 0.6 + 0.4 * sin(_Time.y * (2.0 + h * 5.0) + h * 40.0);
                    col += star * _Stars * up * saturate(1.0 - _CloudCover * 1.2) * 1.5;
                }

                // clouds on a virtual plane above the camera
                if (d.y > -0.02)
                {
                    float2 wind = length(_WindDir.xy) > 0.001 ? normalize(_WindDir.xy) : float2(1, 0);
                    float2 cuv = d.xz / (d.y + 0.12) * _CloudScale + wind * _Time.y * _CloudSpeed;
                    float n = Fbm(cuv);
                    float n2 = Fbm(cuv * 2.7 + 3.1);
                    float th = 1.0 - _CloudCover;
                    float cov = smoothstep(th - 0.08, th + 0.22, n * 0.75 + n2 * 0.35);
                    cov = max(cov, smoothstep(0.75, 1.0, _CloudCover) * 0.85);
                    float shade = saturate((n - 0.35) * 1.8);
                    half3 cc = lerp(_CloudColor.rgb, _CloudShade.rgb, shade);
                    cc += _SunColor.rgb * pow(saturate(sd), 6.0) * 0.4 * sunUp * (1.0 - shade);
                    float fade = smoothstep(-0.02, 0.18, d.y);
                    col = lerp(col, cc, cov * fade);
                }

                col += _Flash * half3(0.75, 0.8, 1.0) * (0.4 + 0.6 * up);
                return half4(col * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
