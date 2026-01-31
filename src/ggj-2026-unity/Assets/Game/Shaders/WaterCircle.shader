Shader "Game/WaterCircle"
{
    Properties
    {
        [Header(Circle)]
        _Radius ("Radius", Range(0.1, 50)) = 5
        _EdgeSoftness ("Edge Softness", Range(0, 1)) = 0.1
        _Center ("Center Offset (XZ)", Vector) = (0, 0, 0, 0)
        
        [Header(Colors)]
        _ShallowColor ("Shallow Color", Color) = (0.4, 0.7, 0.9, 0.7)
        _DeepColor ("Deep Color", Color) = (0.1, 0.3, 0.5, 0.9)
        _FoamColor ("Foam/Edge Color", Color) = (1, 1, 1, 1)
        _EdgeFoamWidth ("Edge Foam Width", Range(0, 1)) = 0.2
        
        [Header(Waves)]
        _WaveSpeed ("Wave Speed", Range(0.1, 5)) = 1
        _WaveHeight ("Wave Height", Range(0, 0.5)) = 0.1
        _WaveScale ("Wave Scale", Range(0.1, 10)) = 2
        
        [Header(Cel Shading)]
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowColor ("Shadow Color", Color) = (0.2, 0.4, 0.6, 1)
        _SpecularThreshold ("Specular Threshold", Range(0.9, 1)) = 0.95
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        
        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 3
        _FresnelIntensity ("Fresnel Intensity", Range(0, 1)) = 0.5
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
            Name "WaterCircle"

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
                float3 positionOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Radius;
                float _EdgeSoftness;
                float4 _Center;
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float _EdgeFoamWidth;
                float _WaveSpeed;
                float _WaveHeight;
                float _WaveScale;
                float _ShadowThreshold;
                float4 _ShadowColor;
                float _SpecularThreshold;
                float4 _SpecularColor;
                float _FresnelPower;
                float _FresnelIntensity;
            CBUFFER_END

            float GetWaveHeight(float2 pos, float time)
            {
                float wave1 = sin(pos.x * _WaveScale + time * _WaveSpeed) * 0.5;
                float wave2 = sin(pos.y * _WaveScale * 0.8 + time * _WaveSpeed * 1.3) * 0.3;
                float wave3 = sin((pos.x + pos.y) * _WaveScale * 0.5 + time * _WaveSpeed * 0.7) * 0.2;
                return (wave1 + wave2 + wave3) * _WaveHeight;
            }

            float3 GetWaveNormal(float2 pos, float time)
            {
                float epsilon = 0.1;
                float heightL = GetWaveHeight(pos - float2(epsilon, 0), time);
                float heightR = GetWaveHeight(pos + float2(epsilon, 0), time);
                float heightD = GetWaveHeight(pos - float2(0, epsilon), time);
                float heightU = GetWaveHeight(pos + float2(0, epsilon), time);
                
                float3 normal = float3(heightL - heightR, 2.0 * epsilon, heightD - heightU);
                return normalize(normal);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float3 posOS = input.positionOS.xyz;
                float3 posWS = TransformObjectToWorld(posOS);
                
                float2 centerOffset = _Center.xz;
                float distFromCenter = length(posOS.xz - centerOffset);
                
                float insideCircle = 1.0 - saturate((distFromCenter - _Radius) / max(_EdgeSoftness * _Radius, 0.001));
                
                float waveHeight = GetWaveHeight(posWS.xz, _Time.y) * insideCircle;
                posOS.y += waveHeight;
                
                output.positionHCS = TransformObjectToHClip(posOS);
                output.positionWS = TransformObjectToWorld(posOS);
                output.positionOS = input.positionOS.xyz;
                
                float3 waveNormal = GetWaveNormal(posWS.xz, _Time.y);
                float3 worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.normalWS = normalize(lerp(worldNormal, waveNormal, 0.7 * insideCircle));
                
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
                output.uv = input.uv;
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centerOffset = _Center.xz;
                float distFromCenter = length(input.positionOS.xz - centerOffset);
                
                float circleMask = 1.0 - saturate((distFromCenter - _Radius) / max(_EdgeSoftness * _Radius, 0.001));
                
                if (circleMask <= 0.001)
                {
                    discard;
                }
                
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                
                float NdotL = dot(normalWS, lightDir);
                float cel = step(_ShadowThreshold, NdotL * 0.5 + 0.5);
                
                float3 halfDir = normalize(lightDir + viewDirWS);
                float NdotH = dot(normalWS, halfDir);
                float specular = step(_SpecularThreshold, NdotH);
                
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                fresnel *= _FresnelIntensity;
                
                float normalizedDist = distFromCenter / _Radius;
                float depthFactor = 1.0 - saturate(normalizedDist);
                half4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);
                
                float edgeFoam = smoothstep(1.0 - _EdgeFoamWidth, 1.0, normalizedDist);
                
                half4 shadowedColor = waterColor * _ShadowColor;
                half4 litColor = lerp(shadowedColor, waterColor, cel);
                
                litColor.rgb += specular * _SpecularColor.rgb * 0.5;
                
                litColor.rgb = lerp(litColor.rgb, _FoamColor.rgb, fresnel);
                litColor.rgb = lerp(litColor.rgb, _FoamColor.rgb, edgeFoam * circleMask);
                
                litColor.a = lerp(waterColor.a, 1.0, fresnel * 0.5);
                litColor.a *= circleMask;
                
                return litColor;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
