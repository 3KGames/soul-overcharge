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
            #include "SandstormFogCore.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

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
                float4 worldPos4 = mul(UNITY_MATRIX_I_VP, clipPos);
                float3 worldPos = worldPos4.xyz / worldPos4.w;
                
                float3 camPos = _WorldSpaceCameraPos.xyz;
                float2 pixelPos = input.uv * _ScreenParams.xy;

                float fogFactor = CalculateSandstormFogFactor(worldPos, camPos, pixelPos);

                return lerp(originalColor, _FogColor, fogFactor);
            }
            ENDHLSL
        }
    }
}