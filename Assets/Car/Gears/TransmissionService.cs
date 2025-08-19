using System;
using UnityEngine;

namespace Car.Gears
{
    public class TransmissionService
    {
        private readonly GearDataRpm _gearDataRpm;
        
        public Action GearChanged;
        
        public int SelectedGear { get; private set; }

        public TransmissionService(GearDataRpm gearDataRpm)
        {
            SelectedGear = 0;
            this._gearDataRpm = gearDataRpm;
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

        public float GetRpm(float speed)
        {
            var gear = GetGearData();
            float clampedSpeed = speed < 0 ? _gearDataRpm.SpeedShift : speed + _gearDataRpm.SpeedShift;
            return clampedSpeed * gear.GearRatio * _gearDataRpm.SpeedToRpmFactor;
        }

        public float GetAcceleration(float speed)
        {
            var gear = GetGearData();
            float clampedSpeed = speed < 0 ? _gearDataRpm.SpeedShift : speed + _gearDataRpm.SpeedShift;
            float rpm = clampedSpeed * gear.GearRatio * _gearDataRpm.SpeedToRpmFactor;

            // Normalize RPM
            float normalizedRpm = Mathf.InverseLerp(_gearDataRpm.RpmIdle, _gearDataRpm.RpmRedline, rpm);
            normalizedRpm = Mathf.Clamp01(normalizedRpm);

            // Get torque from a curve (0..1)
            float torque = _gearDataRpm.AccelerationCurve.Evaluate(normalizedRpm);

            // Calculate acceleration
            float accel = torque * gear.GearRatio * _gearDataRpm.AccelerationModifier;
				
            // Too low rpm
            if (rpm < _gearDataRpm.RpmIdle)
            {
                float lackRpm   = _gearDataRpm.RpmIdle - rpm;                    // сколько «не хватает» до холостых
                float lugPenalty = lackRpm * _gearDataRpm.LowRpmBrakeFactor;     // коэффициент подбираете в инспекторе
                accel -= lugPenalty;                                       // отрицательное ускорение
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