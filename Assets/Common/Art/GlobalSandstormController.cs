using UnityEngine;

[ExecuteAlways]
public class GlobalSandstormController : MonoBehaviour
{
	[Header("Base Fog Settings")]
	public Vector2 fogMinMaxDist = new Vector2(5, 50);
	public float fogDensity = 0.1f;
	public float height = 15.0f;
	[Range(1, 10)] public float fogSharpness = 4.0f;
	public Color fogColor = new Color(0.8f, 0.6f, 0.2f, 1f);

	[Header("Wind and Noise")]
	public Texture2D noiseTex;
	public Vector2 windSpeed = new Vector2(5.0f, 2.0f);
	public float noiseScale = 0.05f;
	public float heightNoise = 4.0f;
	[Range(0, 1)] public float densityNoise = 0.6f;

	[Header("Retro Style")]
	public int stepsNum = 5;
	[Range(0, 1)] public float ditherStrength = 0.5f;
	
	[Header("Forward Visibility (Vignette)")]
	[Range(0f, 1f)] public float forwardClearStrength = 0.6f;
	[Range(0.5f, 0.99f)] public float forwardClearWidth = 0.9f;

	void Update()
	{
		Shader.SetGlobalVector("_FogMinMaxDist", new Vector4(fogMinMaxDist.x, fogMinMaxDist.y, 0, 0));
		Shader.SetGlobalFloat("_FogDensity", fogDensity);
		Shader.SetGlobalFloat("_Height", height);
		Shader.SetGlobalFloat("_FogSharpness", fogSharpness);
		Shader.SetGlobalColor("_FogColor", fogColor);
        
		if (noiseTex != null)
			Shader.SetGlobalTexture("_NoiseTex", noiseTex);
            
		Shader.SetGlobalVector("_WindSpeed", new Vector4(windSpeed.x, windSpeed.y, 0, 0));
		Shader.SetGlobalFloat("_NoiseScale", noiseScale);
		Shader.SetGlobalFloat("_HeightNoise", heightNoise);
		Shader.SetGlobalFloat("_DensityNoise", densityNoise);
        
		Shader.SetGlobalInt("_StepsNum", stepsNum);
		Shader.SetGlobalFloat("_DitherStrength", ditherStrength);
		Shader.SetGlobalFloat("_ForwardClearStrength", forwardClearStrength);
		Shader.SetGlobalFloat("_ForwardClearWidth", forwardClearWidth);
	}
}