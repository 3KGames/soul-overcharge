using System;
using Car.Gears;
using UnityEngine;

namespace Car.Controller.CarPhysics.States
{
	public class DriftCarState: BaseCarState, ITransitionPayload<int>
	{
		private CarPhysicsData _physicsData;
		private TransmissionService _transmission;
		
		private float  _driftTimer;
		
		public int  DriftDir { get; private set; }
		
		public override CarState Kind => CarState.Drift;
		
		/// <param name="dir">Drift direction (-1 = left,  +1 = right)</param>
		public delegate void DriftStartedHandler(int dir);
		public event DriftStartedHandler OnDriftStarted;
		/// <param name="duration">Drift duration (sec.)</param>
		public delegate void DriftEndedHandler(float duration);
		public event DriftEndedHandler OnDriftEnded;


		public DriftCarState(CarPhysicsData physicsData, TransmissionService transmission)
		{
			_physicsData = physicsData;
			_transmission = transmission;
		}
		
		public void ApplyPayload(int driftDir)
		{
			DriftDir = driftDir;
		}
		
		public override void Enter()
		{
			_driftTimer = 0f;
			OnDriftStarted?.Invoke(DriftDir); 
		}

		public override ITransition EvaluateTransition(float dt, Rigidbody rb, CarPhysicsInput inputData)
		{
			if (!inputData.Drift)
			{
				OnDriftEnded?.Invoke(_driftTimer);
				return new Transition<NoPayload>(CarState.Drive, new NoPayload());
			}

			return null;
		}

		public override void Exit()
		{
				
		}

		public override void Tick(float dt, Rigidbody rb, CarPhysicsInput inputData)
		{
			float forwardSpeed = CarPhysicsService.GetForwardSpeed(rb);

			float speedModifier = (inputData.IsOffroad ? _physicsData.OffroadSpeedMultiplier : 1f) *
								  (inputData.HasSouls ? 1f : _physicsData.NoSoulsSpeedMultiplier);
                          
			float gripModifier  = (inputData.IsOffroad ? _physicsData.OffroadGripMultiplier : 1f) *
								  (inputData.HasSouls ? 1f : _physicsData.NoSoulsGripMultiplier);

			float safeSpeedModifier = Mathf.Max(0.01f, speedModifier);

			float fakeSpeedForTransmission = (forwardSpeed * 3.6f / _physicsData.DriftMaxSpeedCoefficient) / safeSpeedModifier;

			float accel = _transmission.GetAcceleration(fakeSpeedForTransmission, inputData)
						  * inputData.TorqueMultiplier
						  * _physicsData.DriftAccelerationCoefficient
						  * speedModifier;
			
			rb.AddForce(rb.transform.forward * accel, ForceMode.Acceleration);

			// Steering
			float t = (inputData.Steer * DriftDir + 1f) * 0.5f;              
			float driftAngleCoef = Mathf.Lerp(
				_physicsData.MinDriftAngleCoefficient,
				_physicsData.MaxDriftAngleCoefficient,
				t);

			// Применяем штраф к повороту в дрифте
			float steerAngle = _transmission.GetGearData().MaxSteerAngle * driftAngleCoef * DriftDir * (inputData.HasSouls ? 1f : _physicsData.NoSoulsGripMultiplier);
			Quaternion delta = Quaternion.Euler(0f, steerAngle * Time.fixedDeltaTime, 0f);
			rb.MoveRotation(rb.rotation * delta);

			// Downforce
			rb.AddForce(-rb.transform.up * _physicsData.Downforce, ForceMode.Acceleration);

			// Боковое трение
			CarPhysicsService.ApplyLateralFriction(rb, _physicsData.DriftSideFrictionCoefficient * gripModifier);

			_driftTimer += dt;
		}
	}
}