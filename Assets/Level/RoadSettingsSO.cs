using UnityEngine;

[CreateAssetMenu(fileName = "NewRoadSettings", menuName = "Road Generator/Road Settings")]
public class RoadSettingsSO : ScriptableObject
{
	[Header("Настройки геометрии")]
	public float laneWidth = 3.5f;
	public float roadThickness = 1f;
	public Mesh laneMesh;
	public float textureRatio = 1f;
	public int transitionLength = 8;

	[Header("Материалы асфальта")]
	public Material cleanEmptyRoadMaterial;
	public Material cleanCenterLaneMaterial;
	public Material cleanSideLaneMaterial;

	[Header("Префабы Ям (Декали)")]
	public GameObject smallPotholePrefab;
	public GameObject mediumPotholePrefab;
	public GameObject largePotholePrefab;

	[Header("Префабы переходов")]
	public GameObject leftTransitionPrefab;
	public GameObject rightTransitionPrefab;
}