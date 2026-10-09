Shader "Shader Graphs/ToonSmokeParticleDeform"
{
    Properties
    {
        _HighlightTint      ("Highlight Tint",     Color)  = (0.7971698, 1, 0.9823405, 1)
        _ShadowTint         ("Shadow Tint",        Color)  = (0.4494927, 0.846718, 0.8584906, 1)
        _Steps              ("Shading Steps",      Range(1, 8)) = 1
        _ShadowAttenuation  ("Shadow Attenuation", Float)  = 1
        _Additive           ("Additive",           Float)  = 0
        _Power              ("Power",              Float)  = 0.5
        _VoronoiDensity     ("Voronoi Density",    Float)  = 0.67
        _DeformScale        ("Deform Scale",       Float)  = 1
        _DisplacementSpeed  ("Displacement Speed", Float)  = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"        = "UniversalPipeline"
            "RenderType"            = "Opaque"
            "UniversalMaterialType" = "Unlit"
            "Queue"                 = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _HighlightTint;
            float4 _ShadowTint;
            float  _Steps;
            float  _ShadowAttenuation;
            float  _Additive;
            float  _Power;
            float  _VoronoiDensity;
            float  _DeformScale;
            float  _DisplacementSpeed;
        CBUFFER_END

        // Noise
        void HashLegacySine(float2 p, out float2 o)
        {
            float2 tmp = float2(dot(p, float2(127.1, 311.7)),
                                dot(p, float2(269.5, 183.3)));
            o = frac(sin(tmp) * 43758.5453);
        }

        float2 VoronoiRandomVector(float2 uv, float angleOffset)
        {
            float2 h;
            HashLegacySine(uv, h);
            return float2(sin(h.y * angleOffset), cos(h.x * angleOffset)) * 0.5 + 0.5;
        }

        float Voronoi(float2 uv, float angleOffset, float density)
        {
            float2 g   = floor(uv * density);
            float2 f   = frac (uv * density);
            float  res = 8.0;

            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                float2 lattice = float2(x, y);
                float2 offset  = VoronoiRandomVector(lattice + g, angleOffset);
                res = min(res, distance(lattice + offset, f));
            }
            return res;
        }

        float RandomRange(float seed, float minV, float maxV)
        {
            float r = frac(sin(dot(float2(seed, seed), float2(12.9898, 78.233))) * 43758.5453);
            return lerp(minV, maxV, r);
        }

        // Deform
        float ComputeDeform(float3 posOS, float3 size, float3 center,
                            float random, float time)
        {
            float3 divided = (posOS - center) / size;
            float  seed    = RandomRange(random, 0.0, 1000.0);
            float  angle   = seed + time * _DisplacementSpeed;

            float v1 = Voronoi(divided.xy                  + angle, angle, _VoronoiDensity);
            float v2 = Voronoi(float2(divided.z, divided.y) + angle, angle, _VoronoiDensity);

            float p1 = pow(v1 + _Additive, _Power);
            float p2 = pow(v2 + _Additive, _Power);

            return saturate(1.0 - (p1 + p2)) * _DeformScale;
        }

        void DeformAndRecalculateNormal(
            float3 posOS, float3 normalOS, float3 tangentOS,
            float3 size, float3 center, float random, float time,
            out float3 outPosOS, out float3 outNormalOS)
        {
            float  d0        = ComputeDeform(posOS, size, center, random, time);
            float3 posDeform = posOS + normalOS * (d0 * size);

            const float kNeighborDist = 0.09;

            float3 tangent   = normalize(tangentOS);
            float3 bitangent = normalize(cross(normalOS, tangent));

            float3 n1 = posOS + bitangent * kNeighborDist;
            float3 n2 = posOS + tangent   * kNeighborDist;

            float d1 = ComputeDeform(n1, size, center, random, time);
            float d2 = ComputeDeform(n2, size, center, random, time);

            float3 n1Deform = n1 + normalOS * (d1 * size);
            float3 n2Deform = n2 + normalOS * (d2 * size);

            float3 dirT = normalize(n2Deform - posDeform);
            float3 dirB = normalize(n1Deform - posDeform);

            outPosOS    = posDeform;
            outNormalOS = normalize(cross(dirT, dirB));
        }
        ENDHLSL

        // Forward
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            Blend One Zero
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 color      : COLOR;
                float4 uv0        : TEXCOORD0;   // xyz = bounds, w = random seed
                float4 uv1        : TEXCOORD1;   // xyz = pivot
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 posOS, normalOS;
                DeformAndRecalculateNormal(
                    IN.positionOS.xyz,
                    IN.normalOS,
                    IN.tangentOS.xyz,
                    IN.uv0.xyz,
                    IN.uv1.xyz,
                    IN.uv0.w,
                    _TimeParameters.x,
                    posOS, normalOS);

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(normalOS);
                OUT.color      = IN.color;
                OUT.fogCoord   = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 n = normalize(IN.normalWS);

                // Main light
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float ndotl = dot(n, mainLight.direction);
                float lit   = saturate(ndotl * _ShadowAttenuation);
                lit *= mainLight.shadowAttenuation;
                lit *= mainLight.distanceAttenuation;
                
                // Posterize
                float steps   = max(_Steps, 1.0);
                float stepped = floor(lit * steps) / steps;

                float3 baseColor = IN.color.rgb;
                float3 shadowCol    = baseColor * _ShadowTint.rgb;
                float3 highlightCol = baseColor * _HighlightTint.rgb * mainLight.color;
                float3 col = lerp(shadowCol, highlightCol, stepped);
                col = MixFog(col, IN.fogCoord);
                
                return half4(col, IN.color.a);
            }
            ENDHLSL
        }

        //  Shadow caster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct AttributesSC
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 uv0        : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsSC
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsSC vertShadow(AttributesSC IN)
            {
                VaryingsSC OUT = (VaryingsSC)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posOS, normalOS;
                DeformAndRecalculateNormal(
                    IN.positionOS.xyz, IN.normalOS, IN.tangentOS.xyz,
                    IN.uv0.xyz, IN.uv1.xyz, IN.uv0.w,
                    _TimeParameters.x,
                    posOS, normalOS);

                float3 posWS = TransformObjectToWorld(posOS);
                float3 nWS   = TransformObjectToWorldNormal(normalOS);

                float3 lightDirWS = normalize(_MainLightPosition.xyz);
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDirWS));

                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = posCS;
                return OUT;
            }

            half4 fragShadow(VaryingsSC IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return 0;
            }
            ENDHLSL
        }

        // Depth only
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            struct AttributesD
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 uv0        : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsD
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsD vertDepth(AttributesD IN)
            {
                VaryingsD OUT = (VaryingsD)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posOS, normalOS;
                DeformAndRecalculateNormal(
                    IN.positionOS.xyz, IN.normalOS, IN.tangentOS.xyz,
                    IN.uv0.xyz, IN.uv1.xyz, IN.uv0.w,
                    _TimeParameters.x,
                    posOS, normalOS);

                OUT.positionCS = TransformObjectToHClip(posOS);
                return OUT;
            }

            half4 fragDepth(VaryingsD IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Shader Graph/FallbackError"
}