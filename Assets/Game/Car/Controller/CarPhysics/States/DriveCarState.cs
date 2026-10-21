using System;
using Car.Controller.CarPhysics.Drivetrain;
using Car.Controller.CarPhysics.Engine;
using Car.Controller.CarPhysics.Transmission;
using UnityEngine;

namespace Car.Controller.CarPhysics.States
{
	public class DriveCarState: BaseCarState
	{
		private CarPhysicsData _physicsData;
		private readonly EngineService _engine;

		private TransmissionService _transmission;
		private readonly DrivetrainService _drivetrain;

		public override CarState Kind => CarState.Drive;

		public DriveCarState(CarPhysicsData physicsData, EngineService engine, DrivetrainService drivetrain, TransmissionService transmission)
		{
			_transmission = transmission;
			_engine = engine;
			_drivetrain = drivetrain;
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
				return new Transition<int>(CarState.Drift, driftDir);
			}

			return null;
		}

		public override void Tick(float dt, Rigidbody rb, CarPhysicsInput inputData)
		{
			float forwardSpeed = CarPhysicsService.GetForwardSpeed(rb);

			// TODO: Change name
			float omegaWheels = inputData.IsGrounded
				? _drivetrain.GripWheelsOmega
				: _drivetrain.SpinWheelsOmega;

			_transmission.Tick(dt, forwardSpeed, inputData.Throttle, inputData.Brake);
			float engagement = _transmission.Engagement;

			bool fuelCut = _engine.UpdateRevLimiter(dt, inputData.Throttle);
			float engineTorque = _engine.CalculateTorque(inputData.Throttle);
			if (fuelCut) engineTorque = 0f;
			float engineFriction = _engine.CalculateFriction();
			float clutchTorque = _transmission.CalculateClutchTorque(omegaWheels, _engine.AngularSpeed, engagement);
			float torqueEffective = engineTorque - engineFriction - clutchTorque;
			_engine.UpdateAngularSpeed(dt, torqueEffective);
			float torqueDrive = _transmission.CalculateDriveTorque(clutchTorque);

			float brakeTorque = inputData.Brake * _physicsData.BrakeTorqueNm;
			float tireForce = _drivetrain.StepTireAndWheel(dt, torqueDrive, brakeTorque, forwardSpeed, inputData.IsGrounded);

			float effMass = _physicsData.BaseMass + _engine.EngineMass;

			if (inputData.IsGrounded)
			{
				float alignment = Mathf.Clamp01(Vector3.Dot(rb.transform.up, inputData.GroundNormal));

				Vector3 groundForward = Vector3.ProjectOnPlane(rb.transform.forward, inputData.GroundNormal);
				if (groundForward.sqrMagnitude > 0.001f)
				{
					groundForward.Normalize();
					float driveFactor = inputData.DriveGroundedFactor * alignment;
					float acceleration = tireForce * driveFactor / effMass;
					rb.AddForce(groundForward * acceleration, ForceMode.Acceleration);
				}

				float turnRadius = Mathf.Max(0.1f, _drivetrain.GetTurnRadius(forwardSpeed));
				float targetTurnRate = (rb.linearVelocity.magnitude / turnRadius) * inputData.Steer;
				float currentTurnRate = Vector3.Dot(rb.angularVelocity, inputData.GroundNormal);
				float turnError = targetTurnRate - currentTurnRate;

				float torque = turnError * _physicsData.SteeringPower
							   * inputData.SteerGroundedFactor * alignment;
				rb.AddTorque(inputData.GroundNormal * torque, ForceMode.Acceleration);

				float gripModifier  = (inputData.IsOffroad ? _physicsData.OffroadGripMultiplier : 1f) *
									  (inputData.HasSouls ? 1f : _physicsData.NoSoulsGripMultiplier);
				CarPhysicsService.ApplyLateralFriction(rb, inputData.GroundNormal,
					_physicsData.SideFrictionCoefficient * gripModifier * inputData.GroundedFactor * alignment);
			}

			_transmission.EndStep();
		}
	}
}
