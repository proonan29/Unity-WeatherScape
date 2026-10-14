// Stylised ocean / lake / flood water for URP: Gerstner waves in world space (amplitude follows the wind),
// fresnel sky reflection, sun glint, crest foam and rain ripples.
Shader "WeatherScape/Water"
{
    Properties
    {
        _DeepColor ("Deep colour", Color) = (0.04, 0.17, 0.26, 1)
        _ShallowColor ("Shallow colour", Color) = (0.10, 0.38, 0.42, 1)
        _SkyColor ("Sky reflection", Color) = (0.60, 0.70, 0.82, 1)
        _FoamColor ("Foam colour", Color) = (0.92, 0.95, 0.97, 1)
        _WaveAmp ("Wave amplitude (m)", Float) = 0.4
        _WaveLength ("Main wave length (m)", Float) = 22
        _WaveSpeed ("Wave speed", Float) = 1
        _WindDir ("Wind direction (x,z)", Vector) = (1, 0.3, 0, 0)
        _Foam ("Foam amount", Range(0, 1)) = 0.2
        _Rain ("Rain ripples", Range(0, 1)) = 0
        _Gloss ("Glint sharpness", Float) = 300
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor, _SkyColor, _FoamColor;
                float _WaveAmp, _WaveLength, _WaveSpeed, _Foam, _Rain, _Gloss;
                float4 _WindDir;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float crest : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            float3 Gerstner(float2 dir, float wavelength, float amp, float3 p, float t, inout float3 tangent, inout float3 binormal)
            {
                float k = 6.2831853 / wavelength;
                float c = sqrt(9.8 / k) * _WaveSpeed;
                float2 d = normalize(dir);
                float steep = min(amp * k, 0.28);
                float a = steep / k;
                float f = k * (dot(d, p.xz) - c * t);
                float sf = sin(f), cf = cos(f);
                tangent += float3(-d.x * d.x * steep * sf, d.x * steep * cf, -d.x * d.y * steep * sf);
                binormal += float3(-d.x * d.y * steep * sf, d.y * steep * cf, -d.y * d.y * steep * sf);
                return float3(d.x * a * cf, a * sf, d.y * a * cf);
            }

            float2 Rot(float2 v, float a) { float s = sin(a), c = cos(a); return float2(c * v.x - s * v.y, s * v.x + c * v.y); }

            Varyings vert(Attributes IN)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(IN.positionOS.xyz);
                float t = _Time.y;
                float2 w = length(_WindDir.xy) > 0.001 ? _WindDir.xy : float2(1, 0);
                float3 tangent = float3(1, 0, 0), binormal = float3(0, 0, 1);
                float3 disp = 0;
                float L = max(_WaveLength, 1.0);
                disp += Gerstner(w, L, _WaveAmp * 0.55, p, t, tangent, binormal);
                disp += Gerstner(Rot(w, 0.45), L * 0.61, _WaveAmp * 0.25, p, t, tangent, binormal);
                disp += Gerstner(Rot(w, -0.6), L * 0.37, _WaveAmp * 0.13, p, t, tangent, binormal);
                disp += Gerstner(Rot(w, 1.1), L * 0.21, _WaveAmp * 0.07, p, t, tangent, binormal);
                p += disp;
                o.positionWS = p;
                o.normalWS = normalize(cross(binormal, tangent));
                o.crest = disp.y / max(_WaveAmp, 0.01);
                o.positionCS = TransformWorldToHClip(p);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                // rain ripples: small animated rings perturb the normal
                if (_Rain > 0.01)
                {
                    float2 cell = floor(IN.positionWS.xz * 1.5);
                    float2 f = frac(IN.positionWS.xz * 1.5) - 0.5;
                    float ph = frac(_Time.y * 1.3 + Hash(cell));
                    float r = length(f);
                    float ring = sin((r - ph * 0.5) * 40.0) * saturate(1.0 - ph) * saturate(0.5 - r) * 2.0;
                    n = normalize(n + float3(f.x, 0, f.y) * ring * _Rain * 0.6);
                }
                float3 v = normalize(GetWorldSpaceViewDir(IN.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(n, v)), 5.0);
                float depthMix = saturate(IN.crest * 0.5 + 0.55);
                half3 body = lerp(_DeepColor.rgb, _ShallowColor.rgb, depthMix * 0.7);
                half3 ambient = SampleSH(n);
                half ndl = saturate(dot(n, light.direction));
                half3 lit = body * (ambient + light.color * ndl * 0.6 * light.shadowAttenuation);
                half3 col = lerp(lit, _SkyColor.rgb, saturate(fresnel * 0.85));
                float3 h = normalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), _Gloss) * 4.0 * light.shadowAttenuation;
                col += light.color * spec;
                float foam = saturate((IN.crest - (1.0 - _Foam * 1.2)) * 2.5) * _Foam;
                foam *= 0.6 + 0.4 * Hash(floor(IN.positionWS.xz * 3.0));
                col = lerp(col, _FoamColor.rgb * (ambient + light.color * 0.6), saturate(foam));
                col = MixFog(col, IN.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
