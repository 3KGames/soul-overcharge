using System;
using Car.Controller.CarPhysics.Drivetrain;
using Car.Controller.CarPhysics.Engine;
using Car.Controller.CarPhysics.Transmission;
using UnityEngine;

namespace Car.Controller.CarPhysics.States
{
    public class DriftRecoveryCarState : BaseCarState, ITransitionPayload<NoPayload>
    {
        private CarPhysicsData _physicsData;
        private readonly EngineService _engine;
        private TransmissionService _transmission;
        private readonly DrivetrainService _drivetrain;

        private const float AlignmentThreshold = 3.0f; 

        public override CarState Kind => CarState.DriftRecovery; 
		
		public event Action OnDriftRecoveryStarted;
		public event Action OnDriftRecoveryEnded;
		
		public float VirtualYawRate { get; private set; }

        public DriftRecoveryCarState(CarPhysicsData physicsData, EngineService engine, DrivetrainService drivetrain, TransmissionService transmission)
        {
            _physicsData = physicsData;
            _engine = engine;
            _drivetrain = drivetrain;
            _transmission = transmission;
        }

        public void ApplyPayload(NoPayload payload)
        {
			
        }

		public override void Enter()
		{
			OnDriftRecoveryStarted?.Invoke();
		}
		
        public override void Exit() { }

		public override ITransition EvaluateTransition(float dt, Rigidbody rb, CarPhysicsInput inputData)
		{
			Vector3 flatVel = rb.linearVelocity;
			flatVel.y = 0;

			Vector3 forwardDir = rb.transform.forward;
			forwardDir.y = 0;

			float slipAngle = Vector3.Angle(forwardDir, flatVel);
    
			float turnRate = Mathf.Abs(rb.angularVelocity.y * Mathf.Rad2Deg);

			if (flatVel.sqrMagnitude < 1f || (slipAngle <= AlignmentThreshold && turnRate < 10f))
			{
				OnDriftRecoveryEnded?.Invoke();
				return new Transition<NoPayload>(CarState.Drive, new NoPayload());
			}

			if (inputData.Drift && flatVel.magnitude > _physicsData.MinSpeedForDrift && Mathf.Abs(inputData.Steer) > 0.1f)
			{
				OnDriftRecoveryEnded?.Invoke();
				int driftDir = inputData.Steer > 0 ? 1 : -1;
				return new Transition<int>(CarState.Drift, driftDir);
			}

			return null;
		}
		
		public override void Tick(float dt, Rigidbody rb, CarPhysicsInput inputData)
		{
		    float forwardSpeed = CarPhysicsService.GetForwardSpeed(rb);
		    float omegaWheels = inputData.IsGrounded
		        ? _drivetrain.GripWheelsOmega
		        : _drivetrain.SpinWheelsOmega;
		
		    _transmission.Tick(dt, forwardSpeed, inputData.Throttle, inputData.Brake);
		    bool fuelCut = _engine.UpdateRevLimiter(dt, inputData.Throttle);
		    float engineTorque = fuelCut ? 0f : _engine.CalculateTorque(inputData.Throttle);
		    float engineFriction = _engine.CalculateFriction();
		    float clutchTorque = _transmission.CalculateClutchTorque(omegaWheels, _engine.AngularSpeed, _transmission.Engagement);
		    
		    float torqueEffective = engineTorque - engineFriction - clutchTorque;
		    _engine.UpdateAngularSpeed(dt, torqueEffective);
		    
		    float torqueDrive = _transmission.CalculateDriveTorque(clutchTorque);
		    float brakeTorque = inputData.Brake * _physicsData.BrakeTorqueNm;
		    float tireForce = _drivetrain.StepTireAndWheel(dt, torqueDrive, brakeTorque, forwardSpeed, inputData.IsGrounded);
		    float effMass = _physicsData.BaseMass + _engine.EngineMass;
		
		    Vector3 flatVelocity = rb.linearVelocity;
		    flatVelocity.y = 0f;
		    float currentSpeed = flatVelocity.magnitude;
		
		    // Virtual handling
		    Vector3 virtualForward = currentSpeed > 0.1f ? flatVelocity.normalized : rb.transform.forward;
			VirtualYawRate = 0f;

			if (inputData.IsGrounded)
			{
				float alignment = Mathf.Clamp01(Vector3.Dot(rb.transform.up, inputData.GroundNormal));
				float acceleration = tireForce * inputData.DriveGroundedFactor * alignment / effMass;

			    if (currentSpeed > 1f && Mathf.Abs(inputData.Steer) > 0.01f)
			    {
			        float turnRadius = Mathf.Max(0.1f, _drivetrain.GetTurnRadius(currentSpeed));
					
					VirtualYawRate = (currentSpeed / turnRadius) * inputData.Steer;
			        float turnAngle = VirtualYawRate * Mathf.Rad2Deg * dt;
			        
			        virtualForward = Quaternion.AngleAxis(turnAngle, inputData.GroundNormal) * virtualForward;
			    }
				
				rb.AddForce(virtualForward * acceleration, ForceMode.Acceleration);
				
				// Lateral friction — в плоскости дороги.
			    Vector3 virtualRight = Vector3.Cross(inputData.GroundNormal, virtualForward).normalized;
			    float lateralSpeed = Vector3.Dot(rb.linearVelocity, virtualRight);
				
			    float gripModifier = (inputData.IsOffroad ? _physicsData.OffroadGripMultiplier : 1f) *
			                         (inputData.HasSouls ? 1f : _physicsData.NoSoulsGripMultiplier);
			    float frictionCoeff = _physicsData.SideFrictionCoefficient * gripModifier * inputData.GroundedFactor * alignment;
			    rb.AddForce(-virtualRight * lateralSpeed * frictionCoeff, ForceMode.Acceleration);
			
				// Body alignment — вокруг нормали дороги.
				if (currentSpeed > 1f)
				{
					Vector3 visualForward = Vector3.ProjectOnPlane(rb.transform.forward, inputData.GroundNormal);
					if (visualForward.sqrMagnitude < 0.001f)
						visualForward = virtualForward;
					else
						visualForward.Normalize();

					Vector3 moveDir = flatVelocity.normalized;
					float slipAngle = Vector3.SignedAngle(visualForward, moveDir, inputData.GroundNormal);
					float currentTurnRate = Vector3.Dot(rb.angularVelocity, inputData.GroundNormal) * Mathf.Rad2Deg;

					float pPower = 0.5f;
					float dPower = 0.1f;

					float alignTorque = (slipAngle * pPower) - (currentTurnRate * dPower);
					rb.AddTorque(inputData.GroundNormal * alignTorque * inputData.SteerGroundedFactor * alignment, ForceMode.Acceleration);
				}
			}

		    _transmission.EndStep();
		}    
	}
}