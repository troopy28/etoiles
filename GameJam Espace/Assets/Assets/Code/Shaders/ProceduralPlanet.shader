// URP Lit shader for procedurally textured planets. The fragment computes a
// 5-color ramp from a noise field (rocky mode) or from a turbulent latitude
// (gas-giant mode) and feeds it to UniversalFragmentPBR for proper star
// lighting. Per-instance variation is pushed via MaterialPropertyBlock from
// PlanetVisualGenerator.cs.
Shader "Custom/ProceduralPlanet"
{
    Properties
    {
        _PaletteA       ("Palette A (low / deep)",  Color) = (0.05, 0.10, 0.40, 1)
        _PaletteB       ("Palette B",               Color) = (0.95, 0.85, 0.55, 1)
        _PaletteC       ("Palette C",               Color) = (0.10, 0.55, 0.20, 1)
        _PaletteD       ("Palette D",               Color) = (0.40, 0.30, 0.20, 1)
        _PaletteE       ("Palette E (high / snow)", Color) = (0.95, 0.95, 0.98, 1)

        _Threshold0     ("Threshold 0 (A→B)", Range(0, 1)) = 0.40
        _Threshold1     ("Threshold 1 (B→C)", Range(0, 1)) = 0.50
        _Threshold2     ("Threshold 2 (C→D)", Range(0, 1)) = 0.65
        _Threshold3     ("Threshold 3 (D→E)", Range(0, 1)) = 0.85

        _NoiseFreq      ("Noise Frequency",        Range(0.5, 10)) = 2.5
        _NoiseSeed      ("Noise Seed (offset)",    Vector)         = (0, 0, 0, 0)

        _PlanetMode     ("Planet Mode (0=rocky 1=gas)", Range(0, 1)) = 0
        _BandTurbulence ("Band Turbulence",         Range(0, 1))   = 0.30
        _PoleIceAmount  ("Pole Ice Amount",         Range(0, 0.5)) = 0.15

        _Smoothness     ("Smoothness",              Range(0, 1)) = 0.20
        _Metallic       ("Metallic",                Range(0, 1)) = 0.0

        // Standard URP Lit fields kept so the inspector matches Lit defaults.
        _Cull           ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
            "IgnoreProjector"= "True"
        }

        // ---------------- Forward Lit ----------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // URP lighting variants
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _PaletteA;
                float4 _PaletteB;
                float4 _PaletteC;
                float4 _PaletteD;
                float4 _PaletteE;
                float  _Threshold0;
                float  _Threshold1;
                float  _Threshold2;
                float  _Threshold3;
                float  _NoiseFreq;
                float4 _NoiseSeed;
                float  _PlanetMode;
                float  _BandTurbulence;
                float  _PoleIceAmount;
                float  _Smoothness;
                float  _Metallic;
                float  _Cull;
            CBUFFER_END

            // ----- Noise helpers (mirrored from Supernova.shader) -----
            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float valueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

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

            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int oct = 0; oct < 4; oct++)
                {
                    v += a * valueNoise(p);
                    p *= 2.05;
                    a *= 0.55;
                }
                return v;
            }

            // 5-color palette ramp with smooth transitions at the 4 thresholds.
            float3 SamplePalette(float t)
            {
                const float w = 0.025; // transition half-width
                float3 c = _PaletteA.rgb;
                c = lerp(c, _PaletteB.rgb, smoothstep(_Threshold0 - w, _Threshold0 + w, t));
                c = lerp(c, _PaletteC.rgb, smoothstep(_Threshold1 - w, _Threshold1 + w, t));
                c = lerp(c, _PaletteD.rgb, smoothstep(_Threshold2 - w, _Threshold2 + w, t));
                c = lerp(c, _PaletteE.rgb, smoothstep(_Threshold3 - w, _Threshold3 + w, t));
                return c;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vn = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS   = vn.normalWS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.fogCoord   = ComputeFogFactor(vp.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Direction on the unit sphere in object space — assumes a roughly
                // centered sphere mesh, which the project's planet prefab uses.
                float3 sphereDir = normalize(IN.positionOS);
                float  latitude  = sphereDir.y; // [-1, 1]

                float3 albedo;

                if (_PlanetMode < 0.5)
                {
                    // -------- ROCKY: altitude from 3D FBM --------
                    float3 noisePos = sphereDir * _NoiseFreq + _NoiseSeed.xyz;
                    float  altitude = saturate(fbm(noisePos));

                    float3 baseCol = SamplePalette(altitude);

                    // Polar ice caps with noisy edges so they don't look like a clean band.
                    float poleStart = 1.0 - _PoleIceAmount;
                    float poleEdge  = (fbm(sphereDir * 4.0 + _NoiseSeed.xyz * 1.3) - 0.5) * 0.18;
                    float poleMask  = smoothstep(poleStart - 0.06, poleStart + 0.06, abs(latitude) + poleEdge);

                    albedo = lerp(baseCol, _PaletteE.rgb, poleMask);
                }
                else
                {
                    // -------- GAS GIANT: latitude bands warped by 2D-ish FBM --------
                    float3 turbPos    = sphereDir * (_NoiseFreq * 1.5) + _NoiseSeed.xyz;
                    float  turbulence = fbm(turbPos) - 0.5;

                    float bandedLat = latitude + turbulence * _BandTurbulence;
                    float t         = saturate(bandedLat * 0.5 + 0.5); // [-1,1] -> [0,1]
                    albedo = SamplePalette(t);
                }

                // ----- Pack into URP PBR pipeline -----
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo      = albedo;
                surfaceData.metallic    = _Metallic;
                surfaceData.smoothness  = (_PlanetMode < 0.5) ? _Smoothness : (_Smoothness * 0.5);
                surfaceData.occlusion   = 1.0;
                surfaceData.alpha       = 1.0;
                surfaceData.normalTS    = half3(0, 0, 1);

                InputData inputData = (InputData)0;
                inputData.positionWS        = IN.positionWS;
                inputData.normalWS          = normalize(IN.normalWS);
                inputData.viewDirectionWS   = normalize(GetWorldSpaceViewDir(IN.positionWS));
                inputData.fogCoord          = IN.fogCoord;
                inputData.shadowCoord       = TransformWorldToShadowCoord(IN.positionWS);
                inputData.bakedGI           = SampleSH(inputData.normalWS);
                inputData.shadowMask        = half4(1, 1, 1, 1);
                inputData.normalizedScreenSpaceUV = float2(0, 0);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb   = MixFog(color.rgb, IN.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // ---------------- Shadow Caster ----------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            Cull   Back
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attr
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct V2F
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attr input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            V2F shadowVert(Attr input)
            {
                V2F o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = GetShadowPositionHClip(input);
                return o;
            }

            half4 shadowFrag(V2F i) : SV_Target { return 0; }
            ENDHLSL
        }

        // ---------------- Depth Only ----------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   depthVert
            #pragma fragment depthFrag

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attr
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct V2F
            {
                float4 positionCS : SV_POSITION;
            };

            V2F depthVert(Attr input)
            {
                V2F o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 depthFrag(V2F i) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
