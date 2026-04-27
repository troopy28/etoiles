// URP unlit additive shader for the supernova VFX. Animation (color, brightness,
// alpha, scale) is driven from Supernova.cs via SetColor/SetFloat — the shader
// itself is static.
Shader "Custom/Supernova"
{
    Properties
    {
        _Color      ("Core Color",   Color)        = (1, 1, 1, 1)
        _EdgeColor  ("Edge Color",   Color)        = (1, 0.4, 0.1, 1)
        _Power      ("Fresnel Power", Range(0.5, 8)) = 2.0
        _Brightness ("Brightness",   Range(0, 20)) = 5
        _Alpha      ("Alpha",        Range(0, 1))  = 1
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
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
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
