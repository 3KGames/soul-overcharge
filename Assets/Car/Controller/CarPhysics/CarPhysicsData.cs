using NaughtyAttributes;
using UnityEngine;

namespace Car.Controller.CarPhysics
{
	[CreateAssetMenu(menuName = "ArcadeCar/CarPhysicsData")]
	public class CarPhysicsData : ScriptableObject
	{
		[Min(0f)]
		[SerializeField] private float		downforce = 100f;
		[Range(0f, 10f)]
		[SerializeField] private float		sideFrictionCoefficient = 5f;
		[Range(0f, 10f)]
		[SerializeField] private float		driftSideFrictionCoefficient = 5f;
		[Range(0f, 50f)]
		[SerializeField] private float		minSpeedForDrift      = 3f;      //Not sure if this is needed
		[MinMaxSlider(0f, 3f), Tooltip("Minimal and maximal drift rotation angle coefficient")]	
		[SerializeField] private Vector2	driftAngleCoefficient;
		[SerializeField] private float		driftBrakeForce = 10f;
		[Range(0f, 1f)]
		[SerializeField] private float		driftMaxSpeedCoefficient = 1/3f;
		[Range(0f, 1f)]
		[SerializeField] private float		driftAccelerationCoefficient = 0.5f;
		
		
		public float Downforce						=> downforce;
		public float SideFrictionCoefficient		=> sideFrictionCoefficient;
		public float DriftSideFrictionCoefficient	=> driftSideFrictionCoefficient;
		public float MinSpeedForDrift				=> minSpeedForDrift;
		public float MinDriftAngleCoefficient		=> driftAngleCoefficient.x;
		public float MaxDriftAngleCoefficient		=> driftAngleCoefficient.y;
		public float DriftBrakeForce				=> driftBrakeForce;
		public float DriftMaxSpeedCoefficient		=> driftMaxSpeedCoefficient;
		public float DriftAccelerationCoefficient	=> driftAccelerationCoefficient;
	}
}