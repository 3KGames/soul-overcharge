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
		public delegate void DriftStartedHandler(float dir);
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
			CarPhysicsService.AlignToRoad(rb, inputData.RoadNormal);
            
			// Auto‑acceleration
			float accel = _transmission.GetAcceleration(rb.linearVelocity.magnitude / _physicsData.DriftMaxSpeedCoefficient) 
						  * inputData.TorqueMultiplier 
						  * _physicsData.DriftAccelerationCoefficient;
			rb.AddForce(rb.transform.forward * accel, ForceMode.Acceleration);

			// Drift steering
			float t = (inputData.Steer * DriftDir + 1f) * 0.5f;              // -1 → 0, 0 → 0.5, 1 → 1
			float driftAngleCoef = Mathf.Lerp(
				_physicsData.MinDriftAngleCoefficient,
				_physicsData.MaxDriftAngleCoefficient,
				t
			);
			float steerAngle = _transmission.GetGearData().MaxSteerAngle * driftAngleCoef * DriftDir;
			Quaternion delta = Quaternion.Euler(0f, steerAngle * Time.fixedDeltaTime, 0f);
			rb.MoveRotation(rb.rotation * delta);
			
			// Downforce
			rb.AddForce(-rb.transform.up * _physicsData.Downforce, ForceMode.Acceleration);
			
			// Lateral friction
			CarPhysicsService.ApplyLateralFriction(rb, _physicsData.DriftSideFrictionCoefficient);
			
			
			_driftTimer += dt;
		}
	}
}