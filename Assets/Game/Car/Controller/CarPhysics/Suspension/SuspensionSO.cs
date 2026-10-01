using UnityEngine;

namespace Game.Car.Controller.CarPhysics.Suspension
{
	[CreateAssetMenu(fileName = "Suspension", menuName = "Car/Suspension", order = 1)]
	public class SuspensionSO : ScriptableObject
	{
		[Header("Wheel")]
		[Min(0.05f)] [SerializeField] private float wheelRadius = 0.44f;
		[Min(0.05f)] [SerializeField] private float tireWidth = 0.25f;

		[Header("Strut")]
		[Min(0.1f)] [SerializeField] private float maxLength = 1f;
		[Min(0.05f)] [SerializeField] private float minLength = 0.5f;

		[Header("Spring / Damper")]
		[Range(0.4f, 5f)] [SerializeField] private float springFrequencyHz = 1.5f;
		[Range(0.05f, 2f)] [SerializeField] private float dampingRatio = 0.35f;

		public float WheelRadius => wheelRadius;
		public float TireWidth   => tireWidth;
		public float MaxLength   => maxLength;
		public float MinLength   => minLength;
		public float Travel      => maxLength - minLength;

		/// <summary>springCoeff = ω0² = (2π·f0)², 1/с².</summary>
		public float SpringCoeff
		{
			get
			{
				float omega = 2f * Mathf.PI * springFrequencyHz;
				return omega * omega;
			}
		}

		/// <summary>damperCoeff = 2·ζ·ω0, 1/с.</summary>
		public float DamperCoeff => 2f * dampingRatio * Mathf.Sqrt(SpringCoeff);
	}
}