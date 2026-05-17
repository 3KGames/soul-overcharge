using Car.Souls.Services;
using UnityEngine;

namespace Car.Controller
{
    public class NitroService
    {
        private readonly NitroData _data;

        private float _boostTimer;
        private float _cooldownTimer;

        public bool  IsBoosting   => _boostTimer > 0f;
        public bool  IsOnCooldown => _cooldownTimer > 0f;

        public float CooldownProgress => _data.CooldownDuration > 0f
            ? Mathf.Clamp01(_cooldownTimer / _data.CooldownDuration)
            : 0f;

        public float CooldownTimeLeft => _cooldownTimer;

        public NitroService(NitroData nitroData)
        {
            _data = nitroData;
        }

        public void TryActivate(SoulService souls)
        {
            if (IsBoosting) return;
            if (IsOnCooldown) return;
            if (souls == null || souls.CurrentSouls <= 0f) return;

            _boostTimer = _data.Duration;
        }

        public void Tick(float dt, SoulService souls)
        {
            if (IsBoosting)
            {
                float drained = _data.SoulDrainPerSecond * dt;
                souls?.Spend(drained, SoulSpendReason.MotionDrain);

                _boostTimer -= dt;

                if (!IsBoosting)
                {
                    _cooldownTimer = _data.CooldownDuration;
                }
            }
            else if (IsOnCooldown)
            {
                _cooldownTimer -= dt;
            }
        }

        public float GetTorqueMultiplier()
        {
            if (!IsBoosting) return 1f;

            float t = 1f - (_boostTimer / _data.Duration);
            return Mathf.Lerp(1f, _data.NitroMultiplier, _data.Ramp.Evaluate(t));
        }
    }
}