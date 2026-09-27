using System;
using UnityEngine;

namespace Car.Controller.CarPhysics.Drivetrain
{
	public class DrivetrainService
	{
		private DrivetrainModel _drivetrainModel;

		public float DrivenWheelsOmega { get; private set; }

		public float WheelR       => _drivetrainModel.WheelRadius;
		public float Wheelbase    => _drivetrainModel.Wheelbase;
		public float WheelInertia => _drivetrainModel.WheelInertia;
		public float TireStiffness => _drivetrainModel.TireStiffness;
		public float TireSubsteps => _drivetrainModel.TireSubsteps;
		public float WheelInternalFriction => _drivetrainModel.WheelInternalFriction;
		public float MaxLongitudinalGrip   => _drivetrainModel.MaxLongitudinalGrip;

		public DrivetrainService(DrivetrainModel drivetrainModel)
		{
			_drivetrainModel = drivetrainModel;
		}

		public void UpdateWheelOmega(float dt, float netTorque)
		{
			float frictionTorque = DrivenWheelsOmega * WheelInternalFriction;
			float finalTorque = netTorque - frictionTorque;

			DrivenWheelsOmega += (finalTorque / WheelInertia) * dt;

			if (Mathf.Abs(DrivenWheelsOmega) < 0.1f && Mathf.Abs(finalTorque) < 1f)
				DrivenWheelsOmega = 0f;
		}

		public float StepTireAndWheel(float dt, float driveTorque, float brakeTorque, float forwardSpeed)
		{
			int steps = Mathf.Max(1, Mathf.RoundToInt(TireSubsteps));
			float subDt = dt / steps;
			float stiffness = TireStiffness;
			float radius = WheelR;

			float maxBrakeTorque = MaxLongitudinalGrip * radius;
			float appliedBrake = Mathf.Clamp(brakeTorque, 0f, maxBrakeTorque);

			float tireForce = 0f;
			float tireForceSum = 0f;

			for (int i = 0; i < steps; i++)
			{
				float tirePatchSpeed = DrivenWheelsOmega * radius;
				float slipSpeed = tirePatchSpeed - forwardSpeed;

				tireForce = slipSpeed * stiffness;
				tireForceSum += tireForce;

				float roadFeedbackTorque = -tireForce * radius;

				float brakeDir = Mathf.Sign(DrivenWheelsOmega);
				if (brakeDir == 0f)
					brakeDir = Mathf.Sign(forwardSpeed);

				float brakeCap = Mathf.Abs(DrivenWheelsOmega) / subDt * WheelInertia;
				float brakeThisStep = Mathf.Min(appliedBrake, brakeCap);

				float netTorque = driveTorque + roadFeedbackTorque - brakeDir * brakeThisStep;

				UpdateWheelOmega(subDt, netTorque);
			}

			return tireForceSum / steps;
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
