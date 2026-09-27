using System;
using Car.Controller.CarPhysics.Engine;
using UnityEngine;

namespace Car.Controller.CarPhysics.Transmission
{
	public class TransmissionService
	{
		private TransmissionModel _transmissionModel;
		private EngineService _engine;

		private float _shiftCooldown;

		private float _engagement = 0f;

		public event Action        GearChanged;
		public event Action<float> RpmChanged;

		public int  SelectedGear { get; private set; }
		public bool IsAutoTransmission { get; set; } = true;

		public float ClutchMax { get; private set; }
		public float ClutchProportion { get; private set; }

		public float Engagement => _engagement;

		public int GearsCount => _transmissionModel.Gears?.Length ?? 0;

		public float CurGearRatio
		{
			get
			{
				if (GearsCount == 0) return 1f;
				int i = Mathf.Clamp(SelectedGear, 0, GearsCount - 1);
				return _transmissionModel.Gears[i] * _transmissionModel.FinalGear;
			}
		}

		public TransmissionService(TransmissionModel transmissionModel, EngineService engineService)
		{
			_transmissionModel = transmissionModel;
			_engine = engineService;

			ClutchMax = engineService.PeakTorque * _transmissionModel.ClutchCoef;

			ClutchProportion = ClutchMax / Mathf.Max(0.0001f, _transmissionModel.ClutchError);
		}

		public void Tick(float dt, float forwardSpeed, float throttle, float brake)
		{
			UpdateEngagement(dt, forwardSpeed, throttle, brake);

			AutoShiftTick(dt, throttle, brake);
		}

		public void AutoShiftTick(float dt, float throttle, float brake)
		{
			if (IsAutoTransmission)
				UpdateAutoShift(dt, throttle, brake);
		}

		public void EndStep()
		{
			RpmChanged?.Invoke(_engine.RPM);
		}

		public float CalculateClutchTorque(float angWheelSpeed, float angEngineSpeed, float engagement)
		{
			float wheelSideOmega = angWheelSpeed * CurGearRatio;

			float angDelta = angEngineSpeed - wheelSideOmega;
			float curClutchCapacity = ClutchMax * Mathf.Clamp01(engagement);

			return Mathf.Clamp(ClutchProportion * angDelta, -curClutchCapacity, curClutchCapacity);
		}

		public float CalculateDriveTorque(float clutchTorque)
		{
			return clutchTorque * CurGearRatio * _transmissionModel.Efficiency;
		}

		public bool CanShiftUp()
		{
			return GearsCount > (SelectedGear + 1);
		}

		public void ShiftUpSafe()
		{
			if (!CanShiftUp()) return;
			SelectedGear++;
			OnShifted();
		}

		public bool CanShiftDown()
		{
			return SelectedGear > 0;
		}

		public void ShiftDownSafe()
		{
			if (!CanShiftDown()) return;
			SelectedGear--;
			OnShifted();
		}

		private void OnShifted()
		{
			_shiftCooldown = _transmissionModel.ShiftCooldownTime;
			GearChanged?.Invoke();
		}

		private void UpdateEngagement(float dt, float forwardSpeed, float throttle, float brake)
		{
			if (brake > 0.01f)
			{
				_engagement = 0f;
				return;
			}

			if (Mathf.Abs(forwardSpeed) >= _transmissionModel.LaunchSpeed)
			{
				_engagement = 1f;
				return;
			}

			float rate = _transmissionModel.ClutchEngageTime <= 0.001f
				? 1f
				: dt / _transmissionModel.ClutchEngageTime;

			_engagement = Mathf.MoveTowards(_engagement, throttle, rate);
		}

		private void UpdateAutoShift(float dt, float throttle, float brake)
		{
			_shiftCooldown -= dt;
			if (_shiftCooldown > 0f) return;

			float rpm = _engine.RPM;

			if (rpm >= _transmissionModel.ShiftUpRPM && CanShiftUp())
			{
				ShiftUpSafe();
				return;
			}

			if (!CanShiftDown()) return;

			bool coasting = throttle < 0.01f && brake < 0.01f;
			if (coasting && rpm < _engine.IdleRPM * 1.2f) return;

			if (rpm <= _transmissionModel.ShiftDownRPM)
				ShiftDownSafe();
		}
	}
}
