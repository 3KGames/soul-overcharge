using System;
using Car.Controller.CarPhysics.Drivetrain;
using Car.Controller.CarPhysics.Engine;
using Car.Controller.CarPhysics.Transmission;
using Common.Runtime;
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
			float omegaWheels = _drivetrain.DrivenWheelsOmega;

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
			float tireForce = _drivetrain.StepTireAndWheel(dt, torqueDrive, brakeTorque, forwardSpeed);

			float forceDrag = Consts.AirDragForce(_physicsData.AeroEfficiency, _physicsData.FrontArea, forwardSpeed); // TODO (?): Not forward speed

			/*// TODO (later): Add Brakes, add tire grip
			
			_drivetrain.ApplyTorque(dt, torqueDrive);
			
			// TODO (learn): Why 1/R
			float effMassC = _transmission.CurGearRatio / _drivetrain.WheelR;
			float effMass = _physicsData.BaseMass + _engine.EngineMass + _engine.Inertia * (effMassC * effMassC);
			
			float forceDrive = torqueDrive / _drivetrain.WheelR;
			
			float acceleration = (forceDrive - forceDrag) / effMass;

			rb.AddForce(rb.transform.forward * acceleration, ForceMode.Acceleration);*/

			float effMass = _physicsData.BaseMass + _engine.EngineMass;
			float acceleration = (tireForce - forceDrag) / effMass;
			rb.AddForce(rb.transform.forward * acceleration, ForceMode.Acceleration);

			float turnRadius = _drivetrain.GetTurnRadius(forwardSpeed);
			float turnRate = (rb.linearVelocity.magnitude / turnRadius) * inputData.Steer;

			Quaternion deltaRotation = Quaternion.Euler(0f, turnRate * Mathf.Rad2Deg * dt, 0f);
			rb.MoveRotation(rb.rotation * deltaRotation);

			rb.AddForce(-rb.transform.up * _physicsData.Downforce, ForceMode.Acceleration);

			float gripModifier  = (inputData.IsOffroad ? _physicsData.OffroadGripMultiplier : 1f) *
								  (inputData.HasSouls ? 1f : _physicsData.NoSoulsGripMultiplier);
			CarPhysicsService.ApplyLateralFriction(rb, _physicsData.SideFrictionCoefficient * gripModifier);

			_transmission.EndStep();
		}
	}
}
