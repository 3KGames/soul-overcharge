using System;
using UnityEngine;
using Common.Runtime;

namespace Car.Controller.CarPhysics.Engine
{
	public class EngineService
	{
		private EngineModel _engineModel;

		private float angularSpeed = 0;
		private float _revLimiterCutLeft = 0f;

		private const float RevLimiterSafetyFactor = 1.15f;

		public float AngularSpeed => angularSpeed;
		public float PeakTorque { get; private set; }

		public float Inertia => _engineModel.InertiaCoef * _engineModel.Volume;
		public float EngineMass => _engineModel.BlockMass + (_engineModel.Volume * 20f)
								+ (_engineModel.CylinderNum * _engineModel.CylinderMass);

		public float MaxOmega => 1000f * Mathf.PI * _engineModel.PistonMaxSpeed / _engineModel.PistonStroke;

		public float MaxRPM => MaxOmega * 30f / Mathf.PI;

		public float IdleOmega => _engineModel.IdleRPM * Mathf.PI / 30f;

		public float StallOmega => _engineModel.StallRPM * Mathf.PI / 30f;

		public float IdleRPM => _engineModel.IdleRPM;
		public float StallRPM => _engineModel.StallRPM;

		public float RPM => angularSpeed * 30f / Mathf.PI;

		public EngineService(EngineModel engineModel)
		{
			_engineModel = engineModel;

			PeakTorque = CalculateTorqueAt(1f, MaxOmega);
		}

		public bool UpdateRevLimiter(float dt, float throttle)
		{
			_revLimiterCutLeft -= dt;

			if (_revLimiterCutLeft > 0f) return true;

			if (throttle > 0.01f && angularSpeed >= MaxOmega)
			{
				_revLimiterCutLeft = _engineModel.RevLimiterCutTime;
				return true;
			}

			return false;
		}

		public float CalculateTorque(float throttle)
		{
			float error = IdleOmega - angularSpeed;
			float idleThrottle = Mathf.Clamp01(error * _engineModel.IdleThrottleGain);
			float effectiveThrottle = Mathf.Clamp01(Mathf.Max(throttle, idleThrottle));

			return CalculateTorqueAt(effectiveThrottle, angularSpeed);
		}

		public float CalculateTorqueAt(float throttle, float omega)
		{
			float throttleEff = Mathf.Pow(Mathf.Clamp01(throttle), _engineModel.ThrottlePow);

			float airMass = (_engineModel.Volume / 1000f) * Consts.AirPressure *
							_engineModel.VolumetricEfficiency * throttleEff;

			float fuelMass = airMass / _engineModel.AirToFuelRatio;
			float arbeit = fuelMass * _engineModel.FuelEnergy * _engineModel.ThermalEfficiency;

			float idealTorque = arbeit / (4f * Mathf.PI);

			float rpmNorm = Mathf.Clamp01(omega * (30f / Mathf.PI) / MaxRPM);

			float leidermannFactor =
				_engineModel.A +
				_engineModel.B * rpmNorm +
				_engineModel.C * (rpmNorm * rpmNorm);

			leidermannFactor = Mathf.Max(0f, leidermannFactor);

			return idealTorque * leidermannFactor;
		}

		public float CalculateFriction()
		{
			return CalculateFrictionAt(angularSpeed);
		}

		public float CalculateFrictionAt(float omega)
		{
			return _engineModel.FrictionC0
				 + _engineModel.FrictionC1 * omega
				 + _engineModel.FrictionC2 * (omega * omega);
		}

		public void UpdateAngularSpeed(float dt, float effectiveTorque)
		{
			angularSpeed += (effectiveTorque / Inertia) * dt;

			angularSpeed = Mathf.Max(StallOmega, angularSpeed);

			angularSpeed = Mathf.Min(MaxOmega * RevLimiterSafetyFactor, angularSpeed);
		}
	}
}
