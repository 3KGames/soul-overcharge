using NaughtyAttributes;
using UnityEngine;

namespace Car.Controller.CarPhysics
{
	[CreateAssetMenu(menuName = "ArcadeCar/CarPhysicsData")]
	public class CarPhysicsData : ScriptableObject
	{
		[Min(0f)]
		[SerializeField] private float downforce = 100f;
		[Range(0f, 10f)]
		[SerializeField] private float sideFrictionCoefficient = 5f;
		[Range(0f, 50f)]
		[SerializeField] private float minSpeedForDrift      = 3f;      //Not sure if this is needed
		[MinMaxSlider(0f, 3f), Tooltip("Minimal and maximal drift rotation angle")]	
		[SerializeField] private Vector2 driftAngleCoefficient;
		
		public float Downforce					=> downforce;
		public float SideFrictionCoefficient	=> sideFrictionCoefficient;
		public float MinSpeedForDrift			=> minSpeedForDrift;
		public float MinDriftAngleCoefficient	=> driftAngleCoefficient.x;
		public float MaxDriftAngleCoefficient	=> driftAngleCoefficient.y;
	}
}