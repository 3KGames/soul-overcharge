Shader "Hidden/Sharpness" {
    Properties {
        [HideInInspector] _BlitTexture ("Blit Texture", 2D) = "white" {}
        _Amount ("Sharpness Amount", Range(-1.0, 1.0)) = 1.0 
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
            float _Amount;

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
            Name "Sharpness"

            HLSLPROGRAM
            #pragma vertex vp
            #pragma fragment fp

            half4 fp(Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                
                float2 texel = _BlitTexture_TexelSize.xy;

                float4 col = saturate(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv));

                float neighbor = _Amount * -1.0;
                float center = _Amount * 4.0 + 1.0;

                float4 n = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(0.0, texel.y));
                float4 e = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(texel.x, 0.0));
                float4 s = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(0.0, -texel.y));
                float4 w = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(-texel.x, 0.0));

                float4 output = n * neighbor + e * neighbor + col * center + s * neighbor + w * neighbor;

                return saturate(output);
            }
            ENDHLSL
        }
    }
}