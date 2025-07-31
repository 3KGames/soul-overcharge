using System;
using Car.Gears;
using UnityEngine;

namespace Car.Controller
{
    public class CarPhysicsService
    {
		private CarPhysicsData _physicsData;

		private float  _driftTimer;
		private float  _turboTimer; 

		public bool IsDrifting { get; private set; }
		/// <summary>
		/// Current drift direction
		/// -1 = left,  +1 = right.
		/// </summary>
		public int  DriftDir { get; private set; }
		
		/// <param name="duration">Drift duration (sec.)</param>
		public delegate void DriftStartedHandler(float dir);
		public event DriftStartedHandler OnDriftStarted;
		/// <param name="duration">Drift duration (sec.)</param>
		public delegate void DriftEndedHandler(float duration);
		public event DriftEndedHandler OnDriftEnded;

		public CarPhysicsService(CarPhysicsData physicsData)
		{
			_physicsData = physicsData;
		}

        public void UpdatePhysics(Rigidbody rb, CarPhysicsInput inputData)
		{
            IGear gear = inputData.Gear;
            
            AlignToRoad(rb, inputData.RoadNormal);
            
            // Auto‑acceleration
            float speed = rb.linearVelocity.magnitude;
            float accel = gear.EvaluateAcceleration(speed) * inputData.TorqueMultiplier;
            rb.AddForce(rb.transform.forward * accel, ForceMode.Acceleration);

            // Steering
			if (!IsDrifting)
			{
				float steerAngle = gear.MaxSteerAngle * inputData.Steer;
				Quaternion delta = Quaternion.Euler(0f, steerAngle * Time.fixedDeltaTime, 0f);
				rb.MoveRotation(rb.rotation * delta);
			}
            

			// Drift
			HandleDriftState(rb, inputData);
			
            // Downforce
            rb.AddForce(-rb.transform.up * _physicsData.Downforce, ForceMode.Acceleration);

            // Lateral friction
            // ApplyLateralFriction(rb, inputData.Drift ? DriftFrictionCoefficient : SideFrictionCoefficient);
        }

		public float GetForwardSpeed(Rigidbody rb)
		{
			return Vector3.Dot(rb.linearVelocity, rb.transform.forward);
		}

        
        private void AlignToRoad(Rigidbody rb, Vector3 roadNormal)
        {
            if (roadNormal == Vector3.zero) return;
            Quaternion toRoad = Quaternion.FromToRotation(rb.transform.up, roadNormal);
            rb.MoveRotation(toRoad * rb.rotation);
        }

        private void ApplyLateralFriction(Rigidbody rb, float coefficient)
        {
            Vector3 rightVel = Vector3.Project(rb.linearVelocity, rb.transform.right);
            rb.AddForce(-rightVel * coefficient, ForceMode.Acceleration);
        }

		private void HandleDriftState(Rigidbody rb, CarPhysicsInput inputData)
		{
			float speed = GetForwardSpeed(rb);
			
			// Drift start
			if (!IsDrifting
				&& inputData.Drift
				&& speed > _physicsData.MinSpeedForDrift
				&& Mathf.Abs(inputData.Steer) > 0.1f)
			{
				IsDrifting = true;
				_driftTimer = 0f;
				DriftDir = inputData.Steer > 0 ? 1 : -1;
				OnDriftStarted?.Invoke(DriftDir);
			}
			
			// Drift update
			if (IsDrifting)
			{
				_driftTimer += Time.fixedDeltaTime;
				
				//ApplyLateralFriction(rb, DriftFrictionCoefficient);
				
				//rb.AddTorque(DriftYawTorque * _driftDir * Vector3.up, ForceMode.Acceleration);
				
				// Нормализуем inputData.Steer в диапазон 0..1
				float t = (inputData.Steer * DriftDir + 1f) * 0.5f;              // -1 → 0, 0 → 0.5, 1 → 1
				
				// Берём коэффициент из интервала [0.5; 1.5]
				float driftAngleCoef = Mathf.Lerp(
					_physicsData.MinDriftAngleCoefficient,
					_physicsData.MaxDriftAngleCoefficient,
					t
				);
				Debug.Log(inputData.Gear.MaxSteerAngle * driftAngleCoef * DriftDir);
				float steerAngle = inputData.Gear.MaxSteerAngle * driftAngleCoef * DriftDir;
				Quaternion delta = Quaternion.Euler(0f, steerAngle * Time.fixedDeltaTime, 0f);
				rb.MoveRotation(rb.rotation * delta);

				if (!inputData.Drift)
				{
					IsDrifting = false;
					/*_turboTimer = MiniTurboDuration;
					float boost = _driftTimer switch
					{
						< 1.0f => MiniTurboSmall,
						< 2.0f => MiniTurboMedium,
						_	   => MiniTurboLarge
					};*/
					OnDriftEnded?.Invoke(_driftTimer);
					//rb.AddForce(rb.transform.forward * boost, ForceMode.VelocityChange); //TODO: Velocity change?
				}
			}
			
			// DUNNO
			/*else if (_turboTimer > 0f)
			{
				_turboTimer -= Time.fixedDeltaTime;
				ApplyLateralFriction(rb, DriftFrictionCoefficient); //?
			}

			//TODO: Refactor this file
			else
			{
				ApplyLateralFriction(rb, SideFrictionCoefficient);
			}*/
			ApplyLateralFriction(rb, _physicsData.SideFrictionCoefficient);
		}
    }
}
