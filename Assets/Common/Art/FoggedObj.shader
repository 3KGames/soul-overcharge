Shader "Custom/Sprite/SteppedSandstormFog_Shared"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)

        [Header(Base Fog Settings)]
        _FogMinMaxDist ("Fog Start Distance (X)", Vector) = (5, 50, 0, 0)
        _FogDensity ("Fog Base Density", Float) = 0.1
        _Height ("Sandstorm Ceiling Height", Float) = 15.0
        _FogSharpness ("Ceiling Sharpness", Range(1, 10)) = 4.0
        _FogColor ("Sand Color", Color) = (0.8, 0.6, 0.2, 1)

        [Header(Wind and Noise)]
        [NoScaleOffset] _NoiseTex ("Noise Texture (Seamless)", 2D) = "gray" {}
        _WindSpeed ("Wind Speed (X, Z)", Vector) = (5.0, 2.0, 0, 0)
        _NoiseScale ("Noise Scale", Float) = 0.05
        _HeightNoise ("Height Noise (Waviness)", Float) = 4.0
        _DensityNoise ("Density Noise Amount", Range(0, 1)) = 0.6

        [Header(Retro Style)]
        _StepsNum ("Number of Steps", Int) = 5
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType"="Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off 
        Cull Off 
        ZTest LEqual

        Pass
        {
            Name "SpriteSteppedSandstorm"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            float4 _Color;

            float4 _FogMinMaxDist;
            float _FogDensity;
            float _Height;
            float _FogSharpness;
            float4 _FogColor;
            
            float4 _WindSpeed;
            float _NoiseScale;
            float _HeightNoise;
            float _DensityNoise;

            int _StepsNum;
            float _DitherStrength;

            static const float bayerMatrix[16] = {
                0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
               12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
               15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

            struct Attributes {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input) {
                Varyings output;
                
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                
                output.uv = input.uv;
                output.color = input.color * _Color;
                
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                
                if (texColor.a < 0.01)
                    discard;

                float3 worldPos = input.positionWS;
                float3 camPos = _WorldSpaceCameraPos.xyz;
                float3 rayDir = worldPos - camPos;
                float sceneDist = length(rayDir);
                float3 viewDir = rayDir / sceneDist;

                float evalDist = min(sceneDist, _FogMinMaxDist.y);
                float3 noisePos = camPos + viewDir * evalDist;
                
                float2 noiseUV = noisePos.xz * _NoiseScale + _Time.y * _WindSpeed.xy;
                float n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                float2 noiseUV2 = noiseUV * 2.0 - _Time.y * _WindSpeed.xy * 0.5;
                float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV2).r;
                float n = (n1 + n2 * 0.5) / 1.5; 
                float n_mapped = n * 2.0 - 1.0; 

                float localHeight = max(0.1, _Height + n_mapped * _HeightNoise);
                float localDensity = max(0.0, _FogDensity * (1.0 + n_mapped * _DensityNoise));

                float t_min = _FogMinMaxDist.x; 
                float t_max = sceneDist;
                
                if (abs(viewDir.y) > 0.001)
                {
                    float t_height = (localHeight - camPos.y) / viewDir.y;
                    float t_ground = (0 - camPos.y) / viewDir.y;
                    
                    float t0 = min(t_height, t_ground);
                    float t1 = max(t_height, t_ground);

                    t_min = max(t_min, t0);
                    t_max = min(t_max, t1);
                }
                else
                {
                    if (camPos.y < 0.0 || camPos.y > localHeight) 
                        t_max = -1.0; 
                }

                float opticalDepth = 0.0;

                if (t_max > t_min)
                {
                    float y_start = clamp(camPos.y + viewDir.y * t_min, 0.0, localHeight);
                    float y_end   = clamp(camPos.y + viewDir.y * t_max, 0.0, localHeight);

                    float k = _FogSharpness;
                    float H = localHeight;

                    if (abs(viewDir.y) > 0.001)
                    {
                        float powK1 = k + 1.0;
                        float denominator = powK1 * pow(H, k);

                        float F_start = y_start - (pow(y_start, powK1) / denominator);
                        float F_end   = y_end - (pow(y_end, powK1) / denominator);

                        opticalDepth = (localDensity / viewDir.y) * (F_end - F_start);
                        opticalDepth = abs(opticalDepth); 
                    }
                    else
                    {
                        float densityAtY = saturate(1.0 - pow(camPos.y / H, k));
                        opticalDepth = localDensity * densityAtY * (t_max - t_min);
                    }
                }

                float fogFactor = saturate(1.0 - exp(-opticalDepth));

                float steps = max(1.0, (float)_StepsNum);
                float2 pixelPos = input.positionCS.xy; 
                
                int ditherX = (int)fmod(pixelPos.x, 4.0);
                int ditherY = (int)fmod(pixelPos.y, 4.0);
                
                float ditherValue = bayerMatrix[ditherX + ditherY * 4];
                ditherValue = lerp(0.5, ditherValue, _DitherStrength);

                fogFactor = floor(fogFactor * steps + ditherValue) / steps;
                fogFactor = saturate(fogFactor);

                half3 finalColor = lerp(texColor.rgb, _FogColor.rgb, fogFactor);
                
                return half4(finalColor, texColor.a);
            }
            ENDHLSL
        }
    }
}