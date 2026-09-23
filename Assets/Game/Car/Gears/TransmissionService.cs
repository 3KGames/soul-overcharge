using System;
using Car.Controller;
using Car.Controller.CarPhysics;
using UnityEngine;

namespace Car.Gears
{
    public class TransmissionService
    {
        private readonly GearDataRpm _gearDataRpm;
		private readonly CarPhysicsData _carPhysicsData;
        
        public event Action GearChanged;
		public event Action<float> RpmChanged;
        
        public int SelectedGear { get; private set; }
		
		public bool IsAutoTransmission { get; set; } = true;

        public TransmissionService(CarPhysicsData carPhysicsData, GearDataRpm gearDataRpm)
        {
            SelectedGear = 0;
            _gearDataRpm = gearDataRpm;
			_carPhysicsData = carPhysicsData;
        }

        public bool CanShiftUp()
        {
            return _gearDataRpm.GearsCount > (SelectedGear + 1);
        }
        
        public void ShiftUpSafe()
        {
            if (!CanShiftUp()) return;
            SelectedGear++;
            GearChanged?.Invoke();
        }

        public bool CanShiftDown()
        {
            return SelectedGear > 0;
        }
        
        public void ShiftDownSafe()
        {
            if (!CanShiftDown()) return;
            SelectedGear--;
            GearChanged?.Invoke();
        }
		
		private void UpdateAutoShift(float speed)
		{
			if (!IsAutoTransmission) return;

			float currentRpm = GetRpm(speed);
			if (currentRpm >= 6000f && CanShiftUp())
			{
				ShiftUpSafe();
				return;
			}

			if (CanShiftDown())
			{
				var prevGear = _gearDataRpm.GetGear(SelectedGear - 1);
        
				float clampedSpeed = speed < 0 ? _gearDataRpm.SpeedShift : speed + _gearDataRpm.SpeedShift;
				float prevGearRpm = clampedSpeed * prevGear.GearRatio * _gearDataRpm.SpeedToRpmFactor;

				if (prevGearRpm < 5800f) 
				{
					ShiftDownSafe();
				}
			}
		}

        public float GetRpm(float speed)
        {
            var gear = GetGearData();
            float clampedSpeed = speed < 0 ? _gearDataRpm.SpeedShift : speed + _gearDataRpm.SpeedShift;
            return clampedSpeed * gear.GearRatio * _gearDataRpm.SpeedToRpmFactor;
        }

        public float GetAcceleration(float speed, CarPhysicsInput input)
		{
			if (speed < 0)
				speed = 0;
			
			UpdateAutoShift(speed);
			
            var gear = GetGearData();
            float clampedSpeed = speed < 0 ? _gearDataRpm.SpeedShift : speed + _gearDataRpm.SpeedShift;
            float rpm = clampedSpeed * gear.GearRatio * _gearDataRpm.SpeedToRpmFactor;
			//Debug.Log($"Rpm: {rpm}");
			RpmChanged?.Invoke(rpm);

            // Normalize RPM
            float normalizedRpm = Mathf.InverseLerp(_gearDataRpm.RpmIdle, _gearDataRpm.RpmRedline, rpm);
            normalizedRpm = Mathf.Clamp01(normalizedRpm);

            // Get torque from a curve (0..1)
            float torque = _gearDataRpm.AccelerationCurve.Evaluate(normalizedRpm);
			float pushForce = torque * Mathf.Pow(gear.GearRatio, _gearDataRpm.GearRatioPow) * _gearDataRpm.AccelerationModifier * input.Throttle;
			
			
			// Engine drag
			float desiredEngineDrag = (_gearDataRpm.BaseEngineDrag + (normalizedRpm * _gearDataRpm.HighRpmDragMultiplier)) * (1f - input.Throttle);
			float maxAllowedDeceleration = speed / Time.fixedDeltaTime;
			float engineDragForce = Mathf.Min(desiredEngineDrag, maxAllowedDeceleration);
			
            // Calculate acceleration
			float accel = pushForce - engineDragForce;
				
            // Too low rpm
            if (rpm < _gearDataRpm.RpmIdle)
            {
                float lackRpm   = _gearDataRpm.RpmIdle - rpm;
                float lugPenalty = lackRpm * _gearDataRpm.LowRpmBrakeFactor;
                accel -= lugPenalty;
                if (accel < 0)
                    accel = 0;
            }
				
            // Rpm overshoot
            if (rpm > _gearDataRpm.RpmRedline)
            {
                float excessRpm = rpm - _gearDataRpm.RpmRedline;
                float brake = -excessRpm * _gearDataRpm.EngineBrakeFactor;
                accel += brake;
            }
            //Debug.Log($"Speed: {speed};   Rpm: {rpm};   Accel: {accel}");
            return accel;
        }

        public GearDataRpm.Gear GetGearData()
        {
            return _gearDataRpm.GetGear(SelectedGear);
        }
    }
}