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

        public float GetRpm(float currentSpeed)
        {
            return currentSpeed * GetGearData().GearRatio * _gearDataRpm.SpeedToRpmFactor;
        }

        public float GetAcceleration(float currentSpeed)
        {
            float rpm = GetRpm(currentSpeed);
            if (rpm > _gearDataRpm.RpmRedline)
            {
                //TODO: brake
                return -1;
            }
            else
            {
                float normalized = Mathf.InverseLerp(0,  _gearDataRpm.RpmRedline, rpm);
                return _gearDataRpm.AccelerationModifier * _gearDataRpm.AccelerationCurve.Evaluate(normalized);
            }
        }

        public GearDataRpm.Gear GetGearData()
        {
            return _gearDataRpm.GetGear(SelectedGear);
        }
    }
}