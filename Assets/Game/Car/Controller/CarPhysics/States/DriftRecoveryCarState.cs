using System;
using Car.Controller.CarPhysics.Drivetrain;
using Car.Controller.CarPhysics.Engine;
using Car.Controller.CarPhysics.Transmission;
using Common.Runtime;
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

        public override void Enter() { }
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
				return new Transition<NoPayload>(CarState.Drive, new NoPayload());
			}

			if (inputData.Drift && flatVel.magnitude > _physicsData.MinSpeedForDrift && Mathf.Abs(inputData.Steer) > 0.1f)
			{
				int driftDir = inputData.Steer > 0 ? 1 : -1;
				return new Transition<int>(CarState.Drift, driftDir);
			}

			return null;
		}
		
		public override void Tick(float dt, Rigidbody rb, CarPhysicsInput inputData)
		{
		    float forwardSpeed = CarPhysicsService.GetForwardSpeed(rb);
		    float omegaWheels = _drivetrain.DrivenWheelsOmega;
		
		    _transmission.Tick(dt, forwardSpeed, inputData.Throttle, inputData.Brake);
		    bool fuelCut = _engine.UpdateRevLimiter(dt, inputData.Throttle);
		    float engineTorque = fuelCut ? 0f : _engine.CalculateTorque(inputData.Throttle);
		    float engineFriction = _engine.CalculateFriction();
		    float clutchTorque = _transmission.CalculateClutchTorque(omegaWheels, _engine.AngularSpeed, _transmission.Engagement);
		    
		    float torqueEffective = engineTorque - engineFriction - clutchTorque;
		    _engine.UpdateAngularSpeed(dt, torqueEffective);
		    
		    float torqueDrive = _transmission.CalculateDriveTorque(clutchTorque);
		    float brakeTorque = inputData.Brake * _physicsData.BrakeTorqueNm;
		    float tireForce = _drivetrain.StepTireAndWheel(dt, torqueDrive, brakeTorque, forwardSpeed);
		    float forceDrag = Consts.AirDragForce(_physicsData.AeroEfficiency, _physicsData.FrontArea, forwardSpeed);
		    
		    float effMass = _physicsData.BaseMass + _engine.EngineMass;
		    float acceleration = (tireForce - forceDrag) / effMass;
		
		    Vector3 flatVelocity = rb.linearVelocity;
		    flatVelocity.y = 0f;
		    float currentSpeed = flatVelocity.magnitude;
		
		    // Virtual handling
		    Vector3 virtualForward = currentSpeed > 0.1f ? flatVelocity.normalized : rb.transform.forward;
		    if (currentSpeed > 1f && Mathf.Abs(inputData.Steer) > 0.01f)
		    {
		        float turnRadius = Mathf.Max(0.1f, _drivetrain.GetTurnRadius(currentSpeed));
		        float turnAngle = (currentSpeed / turnRadius) * inputData.Steer * Mathf.Rad2Deg * dt;
		        
		        virtualForward = Quaternion.Euler(0, turnAngle, 0) * virtualForward;
		    }
			
			rb.AddForce(virtualForward * acceleration, ForceMode.Acceleration);
			
			// Lateral friction
		    Vector3 virtualRight = Vector3.Cross(Vector3.up, virtualForward).normalized;
		    float lateralSpeed = Vector3.Dot(rb.linearVelocity, virtualRight);
			
		    float gripModifier = (inputData.IsOffroad ? _physicsData.OffroadGripMultiplier : 1f) *
		                         (inputData.HasSouls ? 1f : _physicsData.NoSoulsGripMultiplier);
		    float frictionCoeff = _physicsData.SideFrictionCoefficient * gripModifier;
		    rb.AddForce(-virtualRight * lateralSpeed * frictionCoeff, ForceMode.Acceleration);
		
			// Body alignment
			if (currentSpeed > 1f)
			{
				Vector3 visualForward = rb.transform.forward;
				visualForward.y = 0f;
				visualForward.Normalize();

				Vector3 moveDir = flatVelocity.normalized;
				float slipAngle = Vector3.SignedAngle(visualForward, moveDir, Vector3.up);
				float currentTurnRate = rb.angularVelocity.y * Mathf.Rad2Deg;

				float pPower = 0.5f;
				float dPower = 0.1f;

				float alignTorque = (slipAngle * pPower) - (currentTurnRate * dPower);
				rb.AddRelativeTorque(0f, alignTorque, 0f, ForceMode.Acceleration);
			}
		
		    _transmission.EndStep();
		}    
	}
}