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
            #include "SandstormFogCore.hlsl" // Подключение общей математики

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _Color;

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
                float2 pixelPos = input.positionCS.xy;

                float fogFactor = CalculateSandstormFogFactor(worldPos, camPos, pixelPos);

                half3 finalColor = lerp(texColor.rgb, _FogColor.rgb, fogFactor);
                return half4(finalColor, texColor.a);
            }
            ENDHLSL
        }
    }
}