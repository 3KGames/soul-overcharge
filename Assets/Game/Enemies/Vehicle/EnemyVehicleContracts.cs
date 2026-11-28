using System;
using UnityEngine;

namespace Game.Enemies.Vehicle
{
	public interface IVehicleTelemetry
	{
		float CurrentRpm { get; }
		float NormalizedRpm { get; }
		int CurrentGear { get; }
		float EngineLoad { get; }
		float ForwardSpeed { get; }
		float LateralSlipRadians { get; }
		event Action<int> OnGearChanged;
	}

	public interface IEnemyBrain
	{
		void Step(EnemyVehicleModel model, Rigidbody rb, float dt);
		void Reset();
	}

	public interface IVehicleCommandModifier
	{
		void Modify(EnemyVehicleModel model, float dt);
		void Reset();
	}
}