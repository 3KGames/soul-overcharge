using System;

namespace Car.Gears
{
    public class TransmissionService
    {
        private GearDataRpm _gearDataRpm;
        
        public Action TransmissionChanged;
        
        public int SelectedGear { get; private set; }

        public TransmissionService(GearDataRpm gearDataRpm)
        {
            SelectedGear = 0;
            this._gearDataRpm = gearDataRpm;
        }

        public bool CanShiftUp()
        {
            return false;
        }
        
        public void ShiftUp()
        {
            
        }

        public bool CanShiftDown()
        {
            return false;
        }
        
        public void ShiftDown()
        {
            
        }

        public float GetRpm(float currentSpeed)
        {
            return 0f;
        }

        public float GetAcceleration(float currentSpeed)
        {
            return 0f;
        }
    }
}