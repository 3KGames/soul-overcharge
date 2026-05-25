// SandstormFogCore.hlsl
#ifndef SANDSTORM_FOG_CORE_INCLUDED
#define SANDSTORM_FOG_CORE_INCLUDED

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

// Новые переменные для "туннеля" видимости
float _ForwardClearStrength; 
float _ForwardClearWidth;    

static const float bayerMatrix[16] = {
    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
   12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
   15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
};

float CalculateSandstormFogFactor(float3 worldPos, float3 camPos, float2 screenPixelPos)
{
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

    // --- ПРОСЧЕТ "ТУННЕЛЯ" ВИДИМОСТИ ---
    // Вектор "вперед" камеры из системной матрицы URP
    float3 camForward = -UNITY_MATRIX_V[2].xyz;
    
    // Считаем насколько текущий пиксель близок к центру обзора (от 0 до 1)
    float gazeDot = max(0.0, dot(viewDir, camForward));
    
    // Создаем маску: 1.0 в центре экрана, плавно спадает до 0.0 по краям заданного радиуса
    float centerMask = smoothstep(min(_ForwardClearWidth, 0.999), 1.0, gazeDot);
    
    // Уменьшаем базовую плотность тумана пропорционально маске
    localDensity *= lerp(1.0, 1.0 - _ForwardClearStrength, centerMask);
    // -----------------------------------

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
            opticalDepth = abs((localDensity / viewDir.y) * (F_end - F_start));
        }
        else
        {
            float densityAtY = saturate(1.0 - pow(camPos.y / H, k));
            opticalDepth = localDensity * densityAtY * (t_max - t_min);
        }
    }

    float fogFactor = saturate(1.0 - exp(-opticalDepth));
    float steps = max(1.0, (float)_StepsNum);
    
    int ditherX = (int)fmod(screenPixelPos.x, 4.0);
    int ditherY = (int)fmod(screenPixelPos.y, 4.0);
    
    float ditherValue = bayerMatrix[ditherX + ditherY * 4];
    ditherValue = lerp(0.5, ditherValue, _DitherStrength);
    
    return saturate(floor(fogFactor * steps + ditherValue) / steps);
}
#endif