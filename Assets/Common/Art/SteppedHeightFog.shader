Shader "Custom/PostProcess/SteppedSandstormFog"
{
    Properties
    {
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
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "SteppedSandstormPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

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
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input) {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float x = -1.0 + 2.0 * ((input.vertexID & 1) << 1);
                float y = -1.0 + 2.0 * ((input.vertexID & 2)     );
                output.positionCS = float4(x, y, 0.0, 1.0);
                output.uv = float2((x + 1.0) * 0.5, (y + 1.0) * 0.5);
                #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1.0 - output.uv.y;
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 originalColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.uv);
                float rawDepth = SampleSceneDepth(input.uv);

                float2 clipSpaceUV = input.uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                clipSpaceUV.y = -clipSpaceUV.y;
                #endif
                float4 clipPos = float4(clipSpaceUV, rawDepth, 1.0);
                float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
                worldPos.xyz /= worldPos.w;

                float3 camPos = _WorldSpaceCameraPos.xyz;
                float3 rayDir = worldPos.xyz - camPos;
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
                float2 pixelPos = input.uv * _ScreenParams.xy;
                int ditherX = (int)fmod(pixelPos.x, 4.0);
                int ditherY = (int)fmod(pixelPos.y, 4.0);
                
                float ditherValue = bayerMatrix[ditherX + ditherY * 4];
                ditherValue = lerp(0.5, ditherValue, _DitherStrength);

                fogFactor = floor(fogFactor * steps + ditherValue) / steps;
                fogFactor = saturate(fogFactor);

                return lerp(originalColor, _FogColor, fogFactor);
            }
            ENDHLSL
        }
    }
}