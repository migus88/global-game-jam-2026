Shader "Game/WaterFlowing"
{
    Properties
    {
        [Header(Colors)]
        _ShallowColor ("Shallow Color", Color) = (0.4, 0.7, 0.9, 0.7)
        _DeepColor ("Deep Color", Color) = (0.1, 0.3, 0.5, 0.9)
        _FoamColor ("Foam/Edge Color", Color) = (1, 1, 1, 1)
        
        [Header(Flow)]
        _FlowDirection ("Flow Direction", Vector) = (1, 0, 0, 0)
        _FlowSpeed ("Flow Speed", Range(0.1, 10)) = 2
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.3
        
        [Header(Waves)]
        _WaveSpeed ("Wave Speed", Range(0.1, 5)) = 1.5
        _WaveHeight ("Wave Height", Range(0, 0.5)) = 0.15
        _WaveScale ("Wave Scale", Range(0.1, 10)) = 3
        _Turbulence ("Turbulence", Range(0, 1)) = 0.5
        
        [Header(Cel Shading)]
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowColor ("Shadow Color", Color) = (0.2, 0.4, 0.6, 1)
        _SpecularThreshold ("Specular Threshold", Range(0.9, 1)) = 0.95
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        
        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 3
        _FresnelIntensity ("Fresnel Intensity", Range(0, 1)) = 0.5
        
        [Header(Foam Lines)]
        _FoamLineScale ("Foam Line Scale", Range(1, 20)) = 8
        _FoamLineSpeed ("Foam Line Speed", Range(0.1, 5)) = 1
        _FoamLineIntensity ("Foam Line Intensity", Range(0, 1)) = 0.3
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
            Name "WaterFlowing"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            // Stencil buffer to prevent overlapping water planes from double-rendering
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float2 flowUV : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _FlowDirection;
                float _FlowSpeed;
                float _FlowStrength;
                float _WaveSpeed;
                float _WaveHeight;
                float _WaveScale;
                float _Turbulence;
                float _ShadowThreshold;
                float4 _ShadowColor;
                float _SpecularThreshold;
                float4 _SpecularColor;
                float _FresnelPower;
                float _FresnelIntensity;
                float _FoamLineScale;
                float _FoamLineSpeed;
                float _FoamLineIntensity;
            CBUFFER_END

            float GetFlowWaveHeight(float2 pos, float time, float2 flowDir)
            {
                float2 flowOffset = flowDir * time * _FlowSpeed;
                float2 flowPos = pos + flowOffset;
                
                float wave1 = sin(flowPos.x * _WaveScale + time * _WaveSpeed) * 0.4;
                float wave2 = sin(flowPos.y * _WaveScale * 0.7 + time * _WaveSpeed * 1.2) * 0.3;
                
                float turbulence = sin(pos.x * _WaveScale * 2.0 + time * _WaveSpeed * 2.0) 
                                 * sin(pos.y * _WaveScale * 2.0 + time * _WaveSpeed * 1.8) * _Turbulence;
                
                float flowWave = sin(dot(pos, flowDir) * _WaveScale * 0.5 - time * _FlowSpeed * 2.0) * _FlowStrength;
                
                return (wave1 + wave2 + turbulence + flowWave) * _WaveHeight;
            }

            float3 GetFlowNormal(float2 pos, float time, float2 flowDir)
            {
                float epsilon = 0.1;
                float heightL = GetFlowWaveHeight(pos - float2(epsilon, 0), time, flowDir);
                float heightR = GetFlowWaveHeight(pos + float2(epsilon, 0), time, flowDir);
                float heightD = GetFlowWaveHeight(pos - float2(0, epsilon), time, flowDir);
                float heightU = GetFlowWaveHeight(pos + float2(0, epsilon), time, flowDir);
                
                float3 normal = float3(heightL - heightR, 2.0 * epsilon, heightD - heightU);
                return normalize(normal);
            }

            float GetFoamLines(float2 pos, float time, float2 flowDir)
            {
                float2 flowOffset = flowDir * time * _FoamLineSpeed;
                float flowCoord = dot(pos + flowOffset, flowDir) * _FoamLineScale;
                
                float foamLine = sin(flowCoord) * 0.5 + 0.5;
                foamLine = smoothstep(0.7, 0.9, foamLine);
                
                float perpCoord = dot(pos, float2(-flowDir.y, flowDir.x)) * _FoamLineScale * 0.5;
                float variation = sin(perpCoord + time * 0.5) * 0.5 + 0.5;
                
                return foamLine * variation * _FoamLineIntensity;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float3 posOS = input.positionOS.xyz;
                float3 posWS = TransformObjectToWorld(posOS);
                
                float2 flowDir = normalize(_FlowDirection.xz);
                
                float waveHeight = GetFlowWaveHeight(posWS.xz, _Time.y, flowDir);
                posOS.y += waveHeight;
                
                output.positionHCS = TransformObjectToHClip(posOS);
                output.positionWS = TransformObjectToWorld(posOS);
                
                float3 waveNormal = GetFlowNormal(posWS.xz, _Time.y, flowDir);
                float3 worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.normalWS = normalize(lerp(worldNormal, waveNormal, 0.7));
                
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
                output.uv = input.uv;
                output.flowUV = posWS.xz;
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float2 flowDir = normalize(_FlowDirection.xz);
                
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                
                float NdotL = dot(normalWS, lightDir);
                float cel = step(_ShadowThreshold, NdotL * 0.5 + 0.5);
                
                float3 halfDir = normalize(lightDir + viewDirWS);
                float NdotH = dot(normalWS, halfDir);
                float specular = step(_SpecularThreshold, NdotH);
                
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                fresnel *= _FresnelIntensity;
                
                float foamLines = GetFoamLines(input.flowUV, _Time.y, flowDir);
                
                // Use uniform color blend (no UV dependency) for seamless tiling
                half4 waterColor = lerp(_DeepColor, _ShallowColor, 0.5);
                
                half4 shadowedColor = waterColor * _ShadowColor;
                half4 litColor = lerp(shadowedColor, waterColor, cel);
                
                litColor.rgb += specular * _SpecularColor.rgb * 0.5;
                
                litColor.rgb = lerp(litColor.rgb, _FoamColor.rgb, fresnel);
                litColor.rgb = lerp(litColor.rgb, _FoamColor.rgb, foamLines);
                
                litColor.a = lerp(waterColor.a, 1.0, fresnel * 0.5 + foamLines * 0.3);
                
                return litColor;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
