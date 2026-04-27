// URP unlit additive shader for the supernova VFX. Vertex displacement uses FBM
// noise in object space for organic dendrite-like deformation. All animation is
// driven from Supernova.cs via SetColor / SetFloat — the shader itself is static.
Shader "Custom/Supernova"
{
    Properties
    {
        _Color      ("Core Color",      Color)        = (1, 1, 1, 1)
        _EdgeColor  ("Edge Color",      Color)        = (1, 0.4, 0.1, 1)
        _Power      ("Fresnel Power",   Range(0.5, 8)) = 2.0
        _Brightness ("Brightness",      Range(0, 20)) = 5
        _Alpha      ("Alpha",           Range(0, 1))  = 1
        _Displace   ("Displacement",    Range(0, 1))  = 0
        _NoiseFreq  ("Noise Frequency", Range(0.5, 10)) = 4
        _AnimTime   ("Animation Time",  Float)        = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One     // additive
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _EdgeColor;
                float  _Power;
                float  _Brightness;
                float  _Alpha;
                float  _Displace;
                float  _NoiseFreq;
                float  _AnimTime;
            CBUFFER_END

            // Cheap hash → [0,1]
            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // Trilinear value noise → [0,1]
            float valueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);   // smoothstep

                float n000 = hash13(i);
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));

                float nxy0 = lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y);
                float nxy1 = lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            // 3-octave FBM
            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int oct = 0; oct < 3; oct++)
                {
                    v += a * valueNoise(p);
                    p *= 2.05;
                    a *= 0.55;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                float3 nO = normalize(IN.normalOS);

                // Sample FBM in object space, scrolled with animation time.
                // Different speeds per axis → less obvious tiling.
                float3 sp = IN.positionOS.xyz * _NoiseFreq
                          + float3(_AnimTime, _AnimTime * 0.7, _AnimTime * 1.3);
                float n = fbm(sp);
                // Combine sharpened FBM with a ridged term for stronger dendrite/spike profile.
                // Ridged: 1 - |2v - 1| produces sharp ridges where noise crosses 0.5.
                float ridge = 1.0 - abs(2.0 * n - 1.0);
                ridge = pow(ridge, 1.5);
                float spike = pow(n, 2.5);
                float profile = max(spike, ridge * 0.85);

                float3 displacedOS = IN.positionOS.xyz + nO * (profile * _Displace);

                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(displacedOS);
                OUT.positionCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float NdotV = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _Power);

                float3 col = lerp(_Color.rgb, _EdgeColor.rgb, fresnel);
                col *= _Brightness;

                return half4(col, _Alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
