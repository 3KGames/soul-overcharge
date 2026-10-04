Shader "Custom/URP_GouraudShading"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _BaseColor ("Tint Color", Color) = (1,1,1,1)
        [Enum(Both, 0, Front, 1, Back, 2)] _Cull("Render Faces", Float) = 2
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        
        [Toggle(_VERTEX_SNAP_ON)] _VertexSnap("Vertex Snapping", Float) = 0
        [Toggle(_AFFINE_MAPPING_ON)] _AffineMapping("Affine Mapping", Float) = 0
    }
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _VERTEX_SNAP_ON
            #pragma shader_feature_local _AFFINE_MAPPING_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 uv           : TEXCOORD0;
                float3 vertexLighting : TEXCOORD1;
            };
    
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float _Cutoff;
            CBUFFER_END
    
            Varyings vert(Attributes v)
            {
                Varyings o;
                
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);

                #ifdef _VERTEX_SNAP_ON
                    o.positionHCS.xyz /= o.positionHCS.w;
                    o.positionHCS.x = floor(o.positionHCS.x * _ScreenParams.x) / _ScreenParams.x;
                    o.positionHCS.y = floor(o.positionHCS.y * _ScreenParams.y) / _ScreenParams.y;
                    o.positionHCS.xyz *= o.positionHCS.w;
                #endif

                float2 baseUV = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                
                #ifdef _AFFINE_MAPPING_ON
                    o.uv.xy = baseUV * o.positionHCS.w;
                    o.uv.z = o.positionHCS.w;
                #else
                    o.uv.xy = baseUV;
                    o.uv.z = 1.0;
                #endif

                // Gouraud
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;

                float lightIntensity = max(0.0, dot(normalWS, lightDir));
                float3 ambientLighting = SampleSH(normalWS);

                o.vertexLighting = lightIntensity * mainLight.color + ambientLighting;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                #ifdef _AFFINE_MAPPING_ON
                    float2 finalUV = i.uv.xy / i.uv.z;
                #else
                    float2 finalUV = i.uv.xy;
                #endif

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV) * _BaseColor;

                #ifdef _ALPHATEST_ON
                    clip(col.a - _Cutoff);
                #endif

                // Gouraud
                col.rgb *= i.vertexLighting;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}