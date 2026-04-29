// URP unlit additive shader for ship reactor flames. Sculpts a Unity cylinder
// primitive into a tapered cone via radial vertex compression along the flame
// axis. Adds a 3-stop longitudinal color gradient, periodic shock diamonds on
// the centerline, a bright inner core, and FBM noise modulation. Per-reactor
// variation (edge color, brightness, alpha, anim time) is driven from
// ShipReactor.cs via MaterialPropertyBlock.
Shader "Custom/ReactorFlame"
{
    Properties
    {
        [Header(Color Stages)]
        [HDR] _CoreColor    ("Core / Nozzle Color", Color)         = (1.0, 0.95, 0.85, 1)
        [HDR] _MidColor     ("Mid Plume Color",     Color)         = (1.0, 0.55, 0.15, 1)
        [HDR] _EdgeColor    ("Edge / Tip Color",    Color)         = (0.3, 0.6, 1.0, 1)

        [Header(Intensity)]
        _Brightness         ("Brightness",          Range(0, 20))  = 6
        _Alpha              ("Alpha",               Range(0, 1))   = 1

        [Header(Cone Shape)]
        _TipRadius          ("Tip Radius",          Range(0, 1))   = 0.05
        _TaperPower         ("Taper Curve Power",   Range(0.3, 4)) = 1.4
        _BaseBulge          ("Base Bulge",          Range(0, 0.5)) = 0.0

        [Header(Gradient Stops)]
        _CoreEnd            ("Core End (v)",        Range(0, 0.5)) = 0.12
        _MidEnd             ("Mid End (v)",         Range(0.2, 0.95)) = 0.55

        [Header(Shock Diamonds)]
        _DiamondCount       ("Diamond Count",       Range(0, 15))  = 5
        _DiamondSharpness   ("Diamond Sharpness",   Range(2, 32))  = 10
        _DiamondReach       ("Diamond Reach",       Range(0, 1))   = 0.55
        _DiamondIntensity   ("Diamond Intensity",   Range(0, 10))  = 3

        [Header(Inner Core)]
        _InnerCoreIntensity ("Inner Core Intensity",Range(0, 10))  = 2
        _InnerCorePower     ("Inner Core Sharpness",Range(1, 32))  = 8
        _InnerCoreReach     ("Inner Core Reach",    Range(0, 1))   = 0.6

        [Header(Fades)]
        _FresnelPower       ("Fresnel Power",       Range(0.5, 8)) = 2.5
        _LengthFade         ("Length Fade Power",   Range(0.5, 6)) = 2.5
        _RadialFade         ("Radial Fade Power",   Range(0.5, 6)) = 1.6

        [Header(Noise)]
        _NoiseFreq          ("Noise Frequency",     Range(0.5, 12))= 4
        _NoiseScroll        ("Noise Scroll Speed",  Range(0, 8))   = 3.0
        _NoiseStrength      ("Noise Strength",      Range(0, 1))   = 0.45
        _Displace           ("Vertex Displace",     Range(0, 0.3)) = 0.04
        _AnimTime           ("Animation Time",      Float)         = 0

        [Header(Axis)]
        _FlameAxis          ("Flame Axis (object space)", Vector)  = (0, 1, 0, 0)
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

        Blend SrcAlpha One     // additive HDR
        ZWrite Off
        Cull Off               // visible from any angle, including from inside

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define PI 3.14159265

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;   // pre-taper, used for v / r in frag
                float2 uv         : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _MidColor;
                float4 _EdgeColor;
                float  _Brightness;
                float  _Alpha;

                float  _TipRadius;
                float  _TaperPower;
                float  _BaseBulge;

                float  _CoreEnd;
                float  _MidEnd;

                float  _DiamondCount;
                float  _DiamondSharpness;
                float  _DiamondReach;
                float  _DiamondIntensity;

                float  _InnerCoreIntensity;
                float  _InnerCorePower;
                float  _InnerCoreReach;

                float  _FresnelPower;
                float  _LengthFade;
                float  _RadialFade;

                float  _NoiseFreq;
                float  _NoiseScroll;
                float  _NoiseStrength;
                float  _Displace;
                float  _AnimTime;

                float4 _FlameAxis;
            CBUFFER_END

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
                for (int oct = 0; oct < 2; oct++)
                {
                    v += a * valueNoise(p);
                    p *= 2.05;
                    a *= 0.55;
                }
                return v;
            }

            // 3-stop longitudinal gradient (core -> mid -> edge) along v in [0,1].
            float3 gradient3(float v)
            {
                float core_to_mid = smoothstep(0.0, 1.0, saturate(v / max(_CoreEnd, 1e-4)));
                float mid_to_edge = smoothstep(0.0, 1.0,
                                    saturate((v - _MidEnd) / max(1.0 - _MidEnd, 1e-4)));
                float3 a = lerp(_CoreColor.rgb, _MidColor.rgb, core_to_mid);
                return lerp(a, _EdgeColor.rgb, mid_to_edge);
            }

            Varyings vert(Attributes IN)
            {
                // Decompose object-space position into axial + perpendicular components.
                float axial_proj = dot(IN.positionOS.xyz, _FlameAxis.xyz);
                float3 axial = _FlameAxis.xyz * axial_proj;
                float3 perp  = IN.positionOS.xyz - axial;

                // v in [0,1]: 0 at base, 1 at tip. Unity cylinder primitive has y in [-1, +1]
                // (height 2), so we map axial_proj * 0.5 + 0.5 -> [0, 1].
                float v = saturate(axial_proj * 0.5 + 0.5);

                // Conical taper: radius shrinks from full (1) at base to _TipRadius at tip,
                // following a power curve (linear if _TaperPower=1, sharper if >1).
                float taper = lerp(1.0, _TipRadius, pow(v, _TaperPower));

                // Optional bulge near the base (slight pre-flare/mushroom).
                float bulge = 1.0 + _BaseBulge * sin(v * PI);
                float radial_scale = taper * bulge;

                // FBM-driven radial wobble, concentrated past mid-flame for stable root.
                float3 sp = IN.positionOS.xyz * _NoiseFreq
                          + _FlameAxis.xyz * (_AnimTime * _NoiseScroll);
                float n = fbm(sp);
                float wobble = (n - 0.5) * 2.0 * _Displace * smoothstep(0.0, 0.5, v);

                // Apply taper + wobble to the perpendicular component.
                // Cylinder primitive has |perp| ~ 0.5, so wobble*2 maps to absolute world units.
                float3 newPerp = perp * (radial_scale + wobble * 2.0);
                float3 displacedOS = axial + newPerp;

                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(displacedOS);
                OUT.positionCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.uv         = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Axial position [0,1] and radial distance [0,1] in the original cylinder frame.
                // Unity cylinder primitive has y in [-1, +1] (height 2) and radius 0.5.
                float axial_proj = dot(IN.positionOS, _FlameAxis.xyz);
                float v = saturate(axial_proj * 0.5 + 0.5);
                float3 perp = IN.positionOS - _FlameAxis.xyz * axial_proj;
                float r = saturate(length(perp) / 0.5);

                // 3-stop longitudinal gradient: hot white -> mid orange -> cool edge.
                float3 col = gradient3(v);

                // Inner core: bright narrow cone hugging the centerline near the base.
                float core_radial = pow(saturate(1.0 - r), _InnerCorePower);
                float core_axial  = saturate(1.0 - v / max(_InnerCoreReach, 1e-4));
                float inner_core  = core_radial * core_axial;
                col += _CoreColor.rgb * inner_core * _InnerCoreIntensity;

                // Shock diamonds: periodic spikes along axis, focused on centerline,
                // fading out past _DiamondReach (subsonic zone).
                float diamond_phase = v * _DiamondCount * PI;
                float diamond_envelope = saturate(1.0 - smoothstep(0.0, _DiamondReach, v));
                float diamond = pow(saturate(sin(diamond_phase)), _DiamondSharpness)
                              * diamond_envelope
                              * pow(saturate(1.0 - r), 4.0);
                col += _CoreColor.rgb * diamond * _DiamondIntensity;

                // Fresnel rim for silhouette pop.
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                // FBM noise modulation: keeps the plume alive and breathing.
                float3 sp = IN.positionOS * _NoiseFreq
                          + _FlameAxis.xyz * (_AnimTime * _NoiseScroll);
                float n = fbm(sp);
                float noise_mod = lerp(1.0 - _NoiseStrength, 1.0 + _NoiseStrength * 0.5, n);
                col *= _Brightness * noise_mod;

                // Alpha: length fade + radial fade + small fresnel boost on silhouette.
                float a = _Alpha
                        * pow(1.0 - v, _LengthFade)
                        * pow(1.0 - r, _RadialFade);
                a = saturate(a + fresnel * 0.2 * _Alpha);

                return half4(col, a);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
