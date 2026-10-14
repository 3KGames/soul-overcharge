using System;
using UnityEngine;

namespace Car.Controller.CarPhysics.Drivetrain
{
	public class DrivetrainService
	{
		private DrivetrainModel _drivetrainModel;

		private float _gripWheelsOmega;
		private float _spinWheelsOmega;

		public float GripWheelsOmega => _gripWheelsOmega;
		public float SpinWheelsOmega => _spinWheelsOmega;

		public float WheelR       => _drivetrainModel.WheelRadius;
		public float Wheelbase    => _drivetrainModel.Wheelbase;
		public float WheelInertia => _drivetrainModel.WheelInertia;
		public float TireStiffness => _drivetrainModel.TireStiffness;
		public float WheelSpinStiffness => _drivetrainModel.WheelSpinStiffness;
		public float WheelInternalFriction => _drivetrainModel.WheelInternalFriction;
		public float MaxLongitudinalGrip   => _drivetrainModel.MaxLongitudinalGrip;

		public DrivetrainService(DrivetrainModel drivetrainModel)
		{
			_drivetrainModel = drivetrainModel;
		}

		public float StepTireAndWheel(float dt, float driveTorque, float brakeTorque, float forwardSpeed, float? spinDriveTorque = null)
		{
			StepWheel(ref _spinWheelsOmega, dt, spinDriveTorque ?? driveTorque, brakeTorque, forwardSpeed, WheelSpinStiffness);
			float gripForce = StepWheel(ref _gripWheelsOmega, dt, driveTorque, brakeTorque, forwardSpeed, TireStiffness);
			return gripForce;
		}

		private float StepWheel(ref float omegaState, float dt, float driveTorque, float brakeTorque, float forwardSpeed, float stiffness)
		{
			float radius = WheelR;
			float I = WheelInertia;
			float friction = WheelInternalFriction;

			// dω/dt = a - k * ω
			// k = (stiffness * R^2 + friction) / I
			// a = (driveTorque - brakeTerm + stiffness * R * v) / I
			// ω(dt) = ω_inf + (ω_0 - ω_inf) * exp(-k * dt)
			// ω_inf = a / k

			float k = (stiffness * radius * radius + friction) / I;

			float maxBrakeTorque = MaxLongitudinalGrip * radius;
			float appliedBrake = Mathf.Clamp(brakeTorque, 0f, maxBrakeTorque);

			float maxBrakeForce = (driveTorque + stiffness * radius * forwardSpeed) + (omegaState * I / dt);
			float actualBrake = Mathf.Clamp(appliedBrake, 0f, Mathf.Max(0f, maxBrakeForce));

			float a = (driveTorque - actualBrake + stiffness * radius * forwardSpeed) / I;
			float omega0 = omegaState;
			float omegaEnd;

			if (k > 1e-6f)
			{
				float omegaInf = a / k;
				float expTerm = Mathf.Exp(-k * dt);
				omegaEnd = omegaInf + (omega0 - omegaInf) * expTerm;
			}
			else
			{
				omegaEnd = omega0 + a * dt;
			}

			if (omegaEnd < 0f) 
				omegaEnd = 0f;

			omegaState = omegaEnd;

			float omegaAvg = (omega0 + omegaEnd) * 0.5f;

			return (omegaAvg * radius - forwardSpeed) * stiffness;
		}

		public float GetSteeringAngle(float speed)
		{
			if (_drivetrainModel.HighSpeedThreshold <= 0.001f)
				return _drivetrainModel.MaxSteerAngleHighSpeed;

			AnimationCurve curve = _drivetrainModel.SteerAngleCurve;

			float speedKMH = Mathf.Clamp(speed * 3.6f, 0f, _drivetrainModel.HighSpeedThreshold);
			float normalized = speedKMH / _drivetrainModel.HighSpeedThreshold;

			float speedCoef = curve == null ? 1f : Mathf.Clamp01(curve.Evaluate(normalized));

			return Mathf.Lerp(
				_drivetrainModel.MaxSteerAngleLowSpeed,
				_drivetrainModel.MaxSteerAngleHighSpeed,
				speedCoef);
		}

		public float GetTurnRadius(float speed)
		{
			float steeringAngle = GetSteeringAngle(speed);
			float tan = Mathf.Tan(steeringAngle * Mathf.Deg2Rad);

			if (Mathf.Abs(tan) < 0.0001f)
				return 1e6f;

			return _drivetrainModel.Wheelbase / tan;
		}
	}
}
