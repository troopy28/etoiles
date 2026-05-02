Shader "UI/SciFiPanel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (0, 0.5, 1, 1)
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.02
        _ScanlineSpeed ("Scanline Speed", Range(0, 5)) = 1.0
        _ScanlineDensity ("Scanline Density", Range(0, 100)) = 50.0
        _GridDensity ("Grid Density", Range(0, 50)) = 20.0
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [UnityGUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _GlowColor;
            float _BorderWidth;
            float _ScanlineSpeed;
            float _ScanlineDensity;
            float _GridDensity;

            v2f vert(appdata_t v) {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                float2 uv = i.texcoord;
                fixed4 base = tex2D(_MainTex, uv);
                
                // --- Bordure Néon ---
                float borderX = min(uv.x, 1.0 - uv.x);
                float borderY = min(uv.y, 1.0 - uv.y);
                float border = min(borderX, borderY);
                float borderAlpha = smoothstep(_BorderWidth, 0, border);
                
                // --- Scanlines Subtiles ---
                float scanline = sin(uv.y * _ScanlineDensity + _Time.y * _ScanlineSpeed) * 0.5 + 0.5;
                scanline = lerp(1.0, 0.92, scanline);
                
                // --- Grille Fine ---
                float2 gridUV = frac(uv * _GridDensity);
                float grid = (step(0.98, gridUV.x) + step(0.98, gridUV.y)) * 0.05;
                
                // --- Composition ---
                fixed4 col = i.color;
                col.rgb += borderAlpha * _GlowColor.rgb * 0.8; // Glow moins agressif
                col.rgb *= scanline;
                col.rgb += grid * _GlowColor.rgb;
                
                // Fond sombre translucide
                col.a *= 0.95;
                col.a *= base.a;
                
                return col;
            }
            ENDCG
        }
    }
}
