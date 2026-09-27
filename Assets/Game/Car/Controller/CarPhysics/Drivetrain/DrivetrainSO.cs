using UnityEngine;

namespace Car.Controller.CarPhysics.Drivetrain
{
	[CreateAssetMenu(fileName = "Drivetrain", menuName = "Car/Drivetrain", order = 2)]
	public class DrivetrainSO : ScriptableObject
	{
		[Header("Wheel")]
		[Tooltip("Радиус колеса, м")]
		[Min(0.05f)] [SerializeField] private float wheelRadius = 0.35f;

		[Tooltip("Колёсная база, м")]
		[Min(0.1f)] [SerializeField] private float wheelbase = 2.6f;

		[Header("Tire")]
		[Tooltip("Продольная жёсткость шины, Н на м/с проскальзывания. " +
		         "Используется БЕЗ домножений, rb.mass в петле не участвует. " +
		         "Реальные шины 10^4..10^5, меньшие значения дают большую пробуксовку.")]
		[Min(1f)] [SerializeField] private float tireStiffness = 5000f;

		[Tooltip("Внутренних шагов шины за физический кадр. Считается, что жёсткая пружина " +
		         "при явной интеграции расходится, если dt > 2 / (k * (R^2/I + 1/M)). " +
		         "10 шагов при dt = 0.02 даёт шаг 2 мс с трёхкратным запасом.")]
		[Range(1, 64)] [SerializeField] private float tireSubsteps = 10f;

		[Tooltip("Инерция колеса вместе с шиной, кг*м^2")]
		[Min(0.01f)] [SerializeField] private float wheelInertia = 2f;

		[Tooltip("Вязкое трение в колесе. При 85 рад/с даёт всего WheelInternalFriction * 85 Н*м")]
		[Min(0f)] [SerializeField] private float wheelInternalFriction = 0.05f;

		[Header("Brakes")]
		[Tooltip("Предел продольной силы сцепления, Н. Шина не может передать кузову больше. " +
		         "Тормозной момент ограничивается этим значением, умноженным на радиус колеса, " +
		         "поэтому колесо не встаёт даже при полном тормозе. " +
		         "Замедление = это значение / (BaseMass + EngineMass). " +
		         "При 26000 Н и 921 кг это 28.2 м/с^2 — аркадное значение.")]
		[Min(0f)] [SerializeField] private float maxLongitudinalGrip = 26000f;

		[Header("Steering")]
		[Tooltip("Максимальный угол руля при 0 км/ч, градусы")]
		[Range(0.1f, 80f)] [SerializeField] private float maxSteerAngleLowSpeed = 35f;

		[Tooltip("Максимальный угол руля на максимальной скорости, градусы")]
		[Range(0.1f, 80f)] [SerializeField] private float maxSteerAngleHighSpeed = 5f;

		[Tooltip("Порог, после которого руль считается «ватным», км/ч")]
		[Min(0f)] [SerializeField] private float highSpeedThreshold = 50f;

		[Tooltip("0 — полный угол на месте, 1 — минимальный на скорости")]
		[SerializeField] private AnimationCurve steerAngleCurve =
			AnimationCurve.Linear(0f, 0f, 1f, 1f);

		public float WheelRadius             => wheelRadius;
		public float MaxSteerAngleLowSpeed  => maxSteerAngleLowSpeed;
		public float MaxSteerAngleHighSpeed => maxSteerAngleHighSpeed;
		public float HighSpeedThreshold     => highSpeedThreshold;
		public AnimationCurve SteerAngleCurve => steerAngleCurve;
		public float Wheelbase               => wheelbase;

		public float TireStiffness           => tireStiffness;
		public float TireSubsteps            => tireSubsteps;
		public float WheelInertia            => wheelInertia;
		public float WheelInternalFriction   => wheelInternalFriction;
		public float MaxLongitudinalGrip     => maxLongitudinalGrip;
	}
}
