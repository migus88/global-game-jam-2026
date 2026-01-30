Shader "Game/VisionCone"
{
    Properties
    {
        _Color ("Color", Color) = (0, 1, 0, 0.3)
        _Fill ("Fill", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "VisionCone"
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

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
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fill;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float distanceFromCenter = input.uv.y;
                
                // Base color with distance fade
                half4 color = _Color;
                color.a *= (1.0 - distanceFromCenter * 0.5);
                
                // Fill effect - brighter near center based on fill amount
                float fillIntensity = saturate(_Fill * 2.0) * (1.0 - distanceFromCenter);
                color.rgb += fillIntensity * 0.3;
                color.a += fillIntensity * 0.2;
                
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
