using System;
using UnityEngine;

namespace Game.Enemies.Vehicle
{
	[CreateAssetMenu(fileName = "EnemyVehicleConfig", menuName = "Enemies/Vehicle Config", order = 0)]
	public sealed class EnemyVehicleConfigSO : ScriptableObject
	{
		public SteeringSettings Steering = new();
		public YawSettings Yaw = new();
		public SpeedSettings Speed = new();
		public StabilitySettings Stability = new();
		public BodySettings Body = new();
		public DrivetrainSettings Drivetrain = new();
		
		[Serializable]
		public sealed class SteeringSettings
		{
			[Min(0.5f)] public float Wheelbase = 3.0f;
			[Range(10f, 55f)] public float MaxSteerAngleDeg = 35f;
			[Min(1f)] public float MinLookahead = 5f;
			[Min(0f)] public float LookaheadTime = 0.65f;
			[Min(5f)] public float MaxLookahead = 28f;
			[Min(0f)] public float CurvatureGain = 1.0f;
			[Min(0f)] public float EdgeMargin = 0.8f;
			[Min(1f)] public float RecoveryTurnSpeed = 8f;
		}

		[Serializable]
		public sealed class YawSettings
		{
			[Min(0f)] public float Kp = 10f;
			[Min(0f)] public float Kd = 0.25f;
			[Min(0.01f)] public float DerivativeTau = 0.1f; // TODO: What???
			[Min(1f)] public float MaxAccel = 25f;
		}
		
		[Serializable]
		public sealed class SpeedSettings
		{
			[Min(0f)] public float Kp = 2.5f;
			[Min(1f)] public float MaxDriveAccel = 14f;
			[Min(1f)] public float MaxBrakeAccel = 20f;
			[Min(0f)] public float CornerFrictionMu = 1.1f;
		}
		
		[Serializable]
		public sealed class StabilitySettings
		{
			[Min(0f)] public float LateralDamping = 6f;
			[Min(1f)] public float HighSpeedDampingMultiplier = 2.0f;
			[Min(0f)] public float UprightStiffness = 35f;
			[Min(0f)] public float UprightDamping = 8f;
			[Min(1f)] public float MaxUprightAccel = 45f;
		}
		
		[Serializable]
		public sealed class BodySettings
		{
			[Min(100f)] public float Mass = 1500f;
			[Min(0f)] public float LinearDamping = 0.08f;
			[Min(0f)] public float AngularDamping = 0.15f;
			public Vector3 CenterOfMass = new(0f, -0.35f, 0f);
			[Min(1f)] public float MaxAngularVelocity = 15f;
			[Min(0.1f)] public float StuckSpeedThreshold = 1.0f;
			[Min(0.5f)] public float StuckDuration = 2.5f;

			[Header("Hover Suspension (Simple Raycast)")]
			[Min(0f)] public float HoverHeight = 0.3f;
			[Min(0f)] public float HoverRaycastOffset = 0f;
			[Min(0.5f)] public float HoverFrequency = 2.5f;
			[Range(0.1f, 1.5f)] public float HoverDampingRatio = 1.0f;
			[Tooltip("LayerMask для raycast hover. Если None — использует Physics.DefaultRaycastLayers")]
			public LayerMask HoverGroundMask;
		}

		[Serializable]
		public sealed class DrivetrainSettings
		{
			[Min(0.1f)] public float WheelRadius = 0.35f;
			[Min(0.5f)] public float FinalDrive = 3.5f;
			public float[] GearRatios = { 3.2f, 2.1f, 1.5f, 1.1f, 0.85f };
			[Min(300f)] public float IdleRpm = 900f;
			[Min(1000f)] public float MaxRpm = 6500f;
			[Min(800f)] public float ShiftUpRpm = 5800f;
			[Min(400f)] public float ShiftDownRpm = 2400f;
			[Min(0.05f)] public float ShiftCooldown = 0.4f;
			// TODO: Replace with load param
			[Min(0f)] public float ThrottleRpmBoost = 450f;
			[Min(1f)] public float RpmResponseSpeed = 12f;

			public int GearCount => GearRatios.Length;

			public float GetGearRatio(int gear) => GearRatios[gear];
		}
	}
}