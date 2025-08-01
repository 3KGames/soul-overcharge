using Car.Gears;
using UnityEngine;

namespace Car.Controller.CarPhysics.States
{
	public class DriveCarState: BaseCarState
	{
		private CarPhysicsData _physicsData;
		
		public override CarState Kind => CarState.Drive;

		public DriveCarState(CarPhysicsData physicsData)
		{
			_physicsData = physicsData;
		}
		
		public override void Enter()
		{
			
		}

		public override void Exit()
		{
			
		}

		public override ITransition EvaluateTransition(float dt, Rigidbody rb, CarPhysicsInput inputData)
		{
			float speed = CarPhysicsService.GetForwardSpeed(rb);
			
			if (inputData.Drift
				&& speed > _physicsData.MinSpeedForDrift
				&& Mathf.Abs(inputData.Steer) > 0.1f)
			{
				int driftDir = inputData.Steer > 0 ? 1 : -1;
				//rb.AddForce(-rb.transform.forward * 10f, ForceMode.Acceleration);
				return new Transition<int>(CarState.Drift, driftDir);
			}

			return null;
		}

		public override void Tick(float dt, Rigidbody rb, CarPhysicsInput inputData)
		{
			IGear gear = inputData.Gear;
            
			CarPhysicsService.AlignToRoad(rb, inputData.RoadNormal);
            
			// Auto‑acceleration
			float speed = CarPhysicsService.GetForwardSpeed(rb);
			float accel = gear.EvaluateAcceleration(speed) * inputData.TorqueMultiplier;
			rb.AddForce(rb.transform.forward * accel, ForceMode.Acceleration);
			
			Debug.Log($"Speed: {speed};   Accel: {accel}");

			// Steering
			float steerAngle = gear.MaxSteerAngle * inputData.Steer;
			Quaternion delta = Quaternion.Euler(0f, steerAngle * Time.fixedDeltaTime, 0f);
			rb.MoveRotation(rb.rotation * delta);

			// Downforce
			rb.AddForce(-rb.transform.up * _physicsData.Downforce, ForceMode.Acceleration);
			
			// Lateral friction
			CarPhysicsService.ApplyLateralFriction(rb, _physicsData.SideFrictionCoefficient);
		}
	}
}