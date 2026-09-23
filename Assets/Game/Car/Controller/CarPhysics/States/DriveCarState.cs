using System;
using Car.Gears;
using UnityEngine;

namespace Car.Controller.CarPhysics.States
{
	public class DriveCarState: BaseCarState
	{
		private CarPhysicsData _physicsData;
		private TransmissionService _transmission;
		
		public override CarState Kind => CarState.Drive;

		public DriveCarState(CarPhysicsData physicsData, TransmissionService transmission)
		{
			_transmission = transmission;
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
			float forwardSpeed = CarPhysicsService.GetForwardSpeed(rb);

			// Высчитываем модификаторы, комбинируя бездорожье и штраф за отсутствие душ
			float speedModifier = (inputData.IsOffroad ? _physicsData.OffroadSpeedMultiplier : 1f) *
								  (inputData.HasSouls ? 1f : _physicsData.NoSoulsSpeedMultiplier);
			
			float safeSpeedModifier = Mathf.Max(0.01f, speedModifier);

			float fakeSpeedForTransmission = (forwardSpeed * 3.6f) / safeSpeedModifier;
                          
			float gripModifier  = (inputData.IsOffroad ? _physicsData.OffroadGripMultiplier : 1f) *
								  (inputData.HasSouls ? 1f : _physicsData.NoSoulsGripMultiplier);

			float accel = _transmission.GetAcceleration(fakeSpeedForTransmission, inputData)
						  * inputData.TorqueMultiplier
						  * speedModifier;
			rb.AddForce(rb.transform.forward * accel, ForceMode.Acceleration);

			// Ухудшаем управляемость (руль становится "ватным"), умножая максимальный угол поворота на gripModifier
			float steerAngle = _transmission.GetGearData().MaxSteerAngle * inputData.Steer * (inputData.HasSouls ? 1f : _physicsData.NoSoulsGripMultiplier);
			Quaternion delta = Quaternion.Euler(0f, steerAngle * Time.fixedDeltaTime, 0f);
			rb.MoveRotation(rb.rotation * delta);

			// Downforce
			rb.AddForce(-rb.transform.up * _physicsData.Downforce, ForceMode.Acceleration);

			// Сниженное боковое трение приведет к сильному скольжению
			CarPhysicsService.ApplyLateralFriction(rb, _physicsData.SideFrictionCoefficient * gripModifier);
		}	}
}