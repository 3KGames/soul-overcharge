using UnityEngine;

namespace Car.Controller.CarPhysics.Engine
{
	[CreateAssetMenu(fileName = "Engine", menuName = "Car/Engine", order = 0)]
	public class EngineSO : ScriptableObject
	{
		[Header("Engine")]
		[Tooltip("Масса блока цилиндров, кг")]
		[Min(0f)] [SerializeField] private float blockMass = 50f;

		[Tooltip("Рабочий объём, литры")]
		[Min(0.1f)] [SerializeField] private float volume = 2f;

		[Range(1, 24)] [SerializeField] private float cylinderNum = 4f;

		[Tooltip("Масса одного цилиндра, кг")]
		[Min(0f)] [SerializeField] private float cylinderMass = 0.3f;

		[Header("Kinematics")]
		[Tooltip("Ход поршня. Вместе с PistonMaxSpeed задаёт максимальные обороты: " +
		         "MaxRPM = 30000 * PistonMaxSpeed / PistonStroke")]
		[Min(0.001f)] [SerializeField] private float pistonStroke = 0.086f;

		[Tooltip("Подбирается вместе с PistonStroke так, чтобы MaxRPM был около 7000. " +
		         "Задаёт потолок оборотов, ниже которого крутящий момент не растёт.")]
		[Min(0.0001f)] [SerializeField] private float pistonMaxSpeed = 0.02f;

		[Tooltip("Показатель нелинейности отклика на газ. Больше — газ ближе к порогу")]
		[Min(0.1f)] [SerializeField] private float throttlePow = 1.5f;

		[Tooltip("Инерция = InertiaCoef * Volume, кг*м^2")]
		[Min(0.001f)] [SerializeField] private float inertiaCoef = 0.1f;

		[Header("Thermodynamics")]
		[Range(0f, 5f)] [SerializeField] private float turboCoef = 1f;

		[Range(0f, 1.5f)] [SerializeField] private float volumetricEfficiency = 0.85f;

		[Tooltip("Массовое соотношение воздух/топливо, AFR")]
		[Range(1f, 30f)] [SerializeField] private float airToFuelRatio = 14.7f;

		[Range(0f, 1f)] [SerializeField] private float thermalEfficiency = 0.3f;

		[Tooltip("Удельная энергия топлива, Дж/кг")]
		[Min(0f)] [SerializeField] private float fuelEnergy = 34e6f;

		[Header("Leiderman (A + B*r + C*r^2), r = RPM / MaxRPM")]
		[Range(-5f, 5f)] [SerializeField] private float a = 0.5f;

		[Range(-5f, 5f)] [SerializeField] private float b = 1.25f;

		[Range(-5f, 5f)] [SerializeField] private float c = -0.78f;

		[Header("Friction")]
		[Min(0f)] [SerializeField] private float frictionC0 = 3f;

		[Min(0f)] [SerializeField] private float frictionC1 = 0.05f;

		[Min(0f)] [SerializeField] private float frictionC2 = 0.000034f;

		[Header("Idle")]
		[Tooltip("Целевые обороты холостых, об/мин. К ним стремится П-регулятор")]
		[Range(500f, 2000f)] [SerializeField] private float idleRPM = 850f;

		[Tooltip("Жёсткость П-регулятора холостых. Меньше — больше просадка оборотов " +
		         "под нагрузкой. Насыщение при IdleOmega * IdleThrottleGain >> 1 — норма, " +
		         "это аварийный подхват после полной остановки.")]
		[Range(0.0001f, 1f)] [SerializeField] private float idleThrottleGain = 0.05f;

		[Tooltip("Аварийный пол оборотов. ОБЯЗАТЕЛЬНО строго меньше IdleRPM, иначе " +
		         "П-регулятор выродится в ноль и мотор залипнет на холостых.")]
		[Range(0f, 2000f)] [SerializeField] private float stallRPM = 300f;

		[Header("Rev limiter")]
		[Tooltip("Длительность среза топлива на отсечке, с")]
		[Range(0.01f, 0.3f)] [SerializeField] private float revLimiterCutTime = 0.06f;

		public float BlockMass            => blockMass;
		public float Volume               => volume;
		public float CylinderNum          => cylinderNum;
		public float CylinderMass         => cylinderMass;
		public float PistonStroke         => pistonStroke;
		public float PistonMaxSpeed       => pistonMaxSpeed;
		public float ThrottlePow          => throttlePow;
		public float InertiaCoef          => inertiaCoef;
		public float TurboCoef            => turboCoef;
		public float VolumetricEfficiency => volumetricEfficiency;
		public float AirToFuelRatio       => airToFuelRatio;
		public float ThermalEfficiency    => thermalEfficiency;
		public float FuelEnergy           => fuelEnergy;
		public float A                    => a;
		public float B                    => b;
		public float C                    => c;
		public float FrictionC0           => frictionC0;
		public float FrictionC1           => frictionC1;
		public float FrictionC2           => frictionC2;
		public float IdleRPM              => idleRPM;
		public float IdleThrottleGain     => idleThrottleGain;
		public float StallRPM             => stallRPM;
		public float RevLimiterCutTime    => revLimiterCutTime;
	}
}
