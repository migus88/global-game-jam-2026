Shader "Game/VisionCone"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0, 1, 0, 0.3)
        _FillColor ("Fill Color", Color) = (1, 0, 0, 0.6)
        _Fill ("Fill", Range(0, 1)) = 0
        _MaxDistance ("Max Distance", Float) = 10
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
                float3 positionOS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _FillColor;
                float _Fill;
                float _MaxDistance;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Calculate distance from center (origin) in local space
                float distFromCenter = length(input.positionOS.xz);
                float normalizedDist = distFromCenter / _MaxDistance;

                // UV.x contains the actual ray hit distance ratio (accounts for obstacles)
                float actualDistRatio = input.uv.x;

                // Base visibility - fade out towards edges
                float edgeFade = 1.0 - smoothstep(0.7, 1.0, actualDistRatio);

                // Fill effect: fills from center outward based on _Fill value
                // The fill extends from 0 to (_Fill * maxDistance)
                float fillThreshold = _Fill;
                float fillMask = step(normalizedDist, fillThreshold);

                // Smooth transition at fill edge
                float fillEdge = smoothstep(fillThreshold - 0.05, fillThreshold, normalizedDist);
                fillMask = 1.0 - fillEdge;

                // Only show fill where we actually have vision (not blocked by obstacles)
                fillMask *= step(normalizedDist, actualDistRatio + 0.01);

                // Combine colors
                half4 baseWithFade = _BaseColor;
                baseWithFade.a *= edgeFade;

                half4 fillWithFade = _FillColor;
                fillWithFade.a *= edgeFade;

                // Lerp between base and fill based on fill mask
                half4 finalColor = lerp(baseWithFade, fillWithFade, fillMask * _Fill);

                // Add slight pulse effect when detecting
                float pulse = sin(_Time.y * 4.0) * 0.5 + 0.5;
                finalColor.a += fillMask * _Fill * pulse * 0.1;

                return finalColor;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
