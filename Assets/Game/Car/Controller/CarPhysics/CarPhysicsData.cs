using NaughtyAttributes;
using UnityEngine;

namespace Car.Controller.CarPhysics
{
	[CreateAssetMenu(menuName = "ArcadeCar/CarPhysicsData")]
	public class CarPhysicsData : ScriptableObject
	{
		[Range(0f, 50f)]
		[SerializeField] private float		sideFrictionCoefficient = 5f;
		[Header("Brakes")]
		[Tooltip("Тормозной момент на колесо, Н*м.")]
		[Min(0f)]
		[SerializeField] private float		brakeTorqueNm = 12000f;

		[Range(0f, 50f)]
		[SerializeField] private float		driftSideFrictionCoefficient = 5f;
		[Range(0f, 50f)]
		[SerializeField] private float		minSpeedForDrift      = 3f;
		[MinMaxSlider(0f, 3f), Tooltip("Minimal and maximal drift rotation angle coefficient")]
		[SerializeField] private Vector2	driftAngleCoefficient;
		[Tooltip("Тормозной момент на колесо в дрифте, Н*м. Заметно слабее обычного: в заносе сцепления и так не хватает")]
		[Min(0f)]
		[SerializeField] private float		driftBrakeTorqueNm = 4000f;
		[Range(0f, 2f)]
		[SerializeField] private float		driftAccelerationCoefficient = 0.5f;

		[Header("Off-road Penalties")]
		[Range(0f, 1f)]
		[Tooltip("Множитель ускорения на бездорожье (например, 0.5 = едет в 2 раза медленнее)")]
		[SerializeField] private float offroadSpeedMultiplier = 0.5f;
		[Range(0f, 1f)]
		[Tooltip("Множитель бокового трения на бездорожье (машину будет сильнее заносить)")]
		[SerializeField] private float offroadGripMultiplier = 0.4f;
		[Range(0f, 1f)]
		[Tooltip("Множитель скорости при отсутствии душ (например, 0.4 = 40% от обычной скорости)")]
		[SerializeField] private float noSoulsSpeedMultiplier = 0.4f;
		[Range(0f, 1f)]
		[Tooltip("Множитель сцепления при отсутствии душ (например, 0.3 = сильное скольжение и плохой поворот)")]
		[SerializeField] private float noSoulsGripMultiplier = 0.3f;

		[Header("New")]
		[SerializeField] private float baseMass = 1300;

		[SerializeField] private float aeroEfficiency = 0.35f;
		[SerializeField] private float frontArea = 2f;

		public float SideFrictionCoefficient		=> sideFrictionCoefficient;
		public float BrakeTorqueNm					=> brakeTorqueNm;
		public float DriftSideFrictionCoefficient	=> driftSideFrictionCoefficient;
		public float MinSpeedForDrift				=> minSpeedForDrift;
		public float MinDriftAngleCoefficient		=> driftAngleCoefficient.x;
		public float MaxDriftAngleCoefficient		=> driftAngleCoefficient.y;
		public float DriftBrakeTorqueNm				=> driftBrakeTorqueNm;
		public float DriftAccelerationCoefficient	=> driftAccelerationCoefficient;
		public float OffroadSpeedMultiplier			=> offroadSpeedMultiplier;
		public float OffroadGripMultiplier			=> offroadGripMultiplier;
		public float NoSoulsSpeedMultiplier			=> noSoulsSpeedMultiplier;
		public float NoSoulsGripMultiplier			=> noSoulsGripMultiplier;

		public float BaseMass						=> baseMass;
		public float AeroEfficiency					=> aeroEfficiency;
		public float FrontArea						=> frontArea;
	}
}