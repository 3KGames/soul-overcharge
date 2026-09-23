using System;
using Car.Controller;
using Car.Gears;
using Car.Souls.Data;
using UnityEngine;
using VContainer.Unity;

namespace Car.Souls.Services
{
    public class SoulDrainService : IInitializable, IDisposable
    {
        private readonly SoulService         _souls;
        private readonly SoulData            _data;
        private readonly CarService          _car;
        private readonly TransmissionService _transmission;

        public SoulDrainService(
            SoulService souls,
            SoulData data,
            CarService car,
            TransmissionService transmission)
        {
            _souls        = souls;
            _data         = data;
            _car          = car;
            _transmission = transmission;
        }

        public void Initialize()
        {
            _car.PhysicsUpdated += OnPhysicsUpdated;
        }

        public void Dispose()
        {
            if (_car != null)
                _car.PhysicsUpdated -= OnPhysicsUpdated;
        }

        private void OnPhysicsUpdated()
        {
            float speed = Mathf.Abs(_car.CurrentSpeed);
            int   gear  = _transmission.SelectedGear;

            float baseDrainPerSec;
            if (speed > 0.01f)
            {
                float norm = Mathf.Clamp01(speed / _data.MaxSpeedForCurve);
                baseDrainPerSec = _data.DrainBySpeedCurve.Evaluate(norm);
            }
            else
            {
                if (gear <= 0) return;
                baseDrainPerSec = _data.IdleDrainPerSecond;
            }

            float mult  = _data.GetGearDrainMultiplier(gear);
            float drain = baseDrainPerSec * mult * Time.fixedDeltaTime;
            if (drain <= 0f) return;

            _souls.Spend(drain, SoulSpendReason.MotionDrain);
        }
    }
}