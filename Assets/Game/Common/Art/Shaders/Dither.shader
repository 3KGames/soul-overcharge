Shader "Hidden/Dither" {
    Properties {
        [HideInInspector] _BlitTexture ("Blit Texture", 2D) = "white" {}
        
        _Spread ("Dither Spread", Range(0.0, 1.0)) = 0.5
        [IntRange] _RedColorCount ("Red Color Count", Range(2, 32)) = 8
        [IntRange] _GreenColorCount ("Green Color Count", Range(2, 32)) = 8
        [IntRange] _BlueColorCount ("Blue Color Count", Range(2, 32)) = 8
        [IntRange] _BayerLevel ("Bayer Matrix Level (0=2x2, 1=4x4, 2=8x8)", Range(0, 2)) = 0
    }

    SubShader {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D_X(_BlitTexture);
            
            float4 _BlitTexture_TexelSize; 

            Varyings vp(Attributes input) {
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
        ENDHLSL

        Pass {
            HLSLPROGRAM
            #pragma vertex vp
            #pragma fragment fp

            float _Spread;
            int _RedColorCount, _GreenColorCount, _BlueColorCount, _BayerLevel;

            static const int bayer2[2 * 2] = {
                0, 2,
                3, 1
            };

            static const int bayer4[4 * 4] = {
                0, 8, 2, 10,
                12, 4, 14, 6,
                3, 11, 1, 9,
                15, 7, 13, 5
            };

            static const int bayer8[8 * 8] = {
                0, 32, 8, 40, 2, 34, 10, 42,
                48, 16, 56, 24, 50, 18, 58, 26,  
                12, 44,  4, 36, 14, 46,  6, 38, 
                60, 28, 52, 20, 62, 30, 54, 22,  
                3, 35, 11, 43,  1, 33,  9, 41,  
                51, 19, 59, 27, 49, 17, 57, 25, 
                15, 47,  7, 39, 13, 45,  5, 37, 
                63, 31, 55, 23, 61, 29, 53, 21
            };

            float GetBayer2(int x, int y) {
                return float(bayer2[(x % 2) + (y % 2) * 2]) * (1.0f / 4.0f) - 0.5f;
            }

            float GetBayer4(int x, int y) {
                return float(bayer4[(x % 4) + (y % 4) * 4]) * (1.0f / 16.0f) - 0.5f;
            }

            float GetBayer8(int x, int y) {
                return float(bayer8[(x % 8) + (y % 8) * 8]) * (1.0f / 64.0f) - 0.5f;
            }

            half4 fp(Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.uv);

                int x = (int)(input.uv.x * _BlitTexture_TexelSize.z);
                int y = (int)(input.uv.y * _BlitTexture_TexelSize.w);

                float bayerValues[3] = { 0, 0, 0 };
                bayerValues[0] = GetBayer2(x, y);
                bayerValues[1] = GetBayer4(x, y);
                bayerValues[2] = GetBayer8(x, y);

                float colorStep = 1.0f / (_RedColorCount - 1.0f);
                float4 output = col + (_Spread * colorStep) * bayerValues[_BayerLevel];

                output.r = floor((_RedColorCount - 1.0f) * output.r + 0.5f) / (_RedColorCount - 1.0f);
                output.g = floor((_GreenColorCount - 1.0f) * output.g + 0.5f) / (_GreenColorCount - 1.0f);
                output.b = floor((_BlueColorCount - 1.0f) * output.b + 0.5f) / (_BlueColorCount - 1.0f);

                return output;
            }
            ENDHLSL
        }
    }
}