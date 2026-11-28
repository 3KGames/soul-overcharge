using System;
using Game.Common.Runtime;
using UnityEngine;

namespace Game.Enemies.Vehicle
{
	public class EnemyVehicleModel : IVehicleTelemetry
	{
		public EnemyVehicleConfigSO Config { get; }

		// TODO: Think
		public int SpatialAgentId { get; set; } = -1;
		
		public IRoadPath CurrentPath { get; set; }
		public float DistanceAlongPath { get; set; }
		public int CurrentLane { get; set; }
		public float LateralOffset { get; set; }
		public float Curvature { get; set; }
		public Vector3 ProjectedPosition { get; set; }
		public Vector3 RoadForward { get; set; } = Vector3.forward;
		public Vector3 RoadRight { get; set; } = Vector3.right;
		public Vector3 RoadUp { get; set; } = Vector3.up;
		public bool HasRoad => CurrentPath != null;
		
		// TODO: Think
		public IRoadPath LastValidPath { get; set; }
		public float LastValidDistance { get; set; }
		public float LastValidLateralOffset { get; set; }
		
		public float TargetLateralOffset { get; set; }
		public float TargetSpeed { get; set; }
		public Vector3? WorldTargetOverride { get; set; }
		public Vector3? PendingPushImpulse { get; set; } //TODO:
		
		public bool YawControllerInitialized { get; set; }
		public float PrevYawRate { get; set; }
		public float FilteredYawAlpha { get; set; }
		public float StuckTimer { get; set; }
		
		public Vector3 LastGoalWorld { get; set; }
		public float LastSteerAngleRad { get; set; }
		public float LastThrottle { get; set; }

		public float CurrentRpm { get; set; }
		public float ShiftCooldownTimer { get; set; }
		public int CurrentGear { get; set; }
		public float EngineLoad { get; set; }
		public float ForwardSpeed { get; set; }
		public float LateralSlipRadians { get; set; }

		public float NormalizedRpm =>
			Mathf.InverseLerp(Config.Drivetrain.IdleRpm, Config.Drivetrain.MaxRpm, CurrentRpm);

		public event Action<int> OnGearChanged;
		
		public EnemyVehicleModel(EnemyVehicleConfigSO config)
		{
			Config = config;
			ResetRuntimeState();
		}
		
		public void ResetRuntimeState()
		{
			TargetLateralOffset = 0f;
			TargetSpeed = 0f;
			WorldTargetOverride = null;
			PendingPushImpulse = null;

			YawControllerInitialized = false;
			PrevYawRate = 0f;
			FilteredYawAlpha = 0f;
			StuckTimer = 0f;

			LastSteerAngleRad = 0f;
			LastThrottle = 0f;

			CurrentGear = 0;
			ShiftCooldownTimer = 0f;
			CurrentRpm = Config.Drivetrain.IdleRpm;
			EngineLoad = 0f;
			ForwardSpeed = 0f;
			LateralSlipRadians = 0f;
		}

		public void NotifyGearChanged(int newGear) => OnGearChanged?.Invoke(newGear);
	}
}