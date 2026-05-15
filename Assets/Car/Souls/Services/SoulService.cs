using System;
using Car.Souls.Data;
using UnityEngine;

namespace Car.Souls.Services
{
    public enum SoulSource
    {
        EnemyKill,
        Pickup,
        Cheat
    }

    public enum SoulSpendReason
    {
        Shot,
        MotionDrain,
        AbilityCost
    }

    public class SoulService
    {
        private readonly SoulData _data;

        public float CurrentSouls { get; private set; }
        public float MaxSouls => _data.MaxSouls;
        public float Normalized => MaxSouls > 0f ? CurrentSouls / MaxSouls : 0f;

        public event Action<float, float> SoulsChanged;
        public event Action SoulsDepleted;
        public event Action SoulsFull;

        private bool _wasDepleted;
        private bool _wasFull;

        public SoulService(SoulData data)
        {
            _data = data;
            CurrentSouls = Mathf.Clamp(data.StartSouls, 0f, data.MaxSouls);
            _wasDepleted = CurrentSouls <= 0f;
            _wasFull     = CurrentSouls >= _data.MaxSouls;
        }

        public void Add(float amount, SoulSource source)
        {
            if (amount <= 0f) return;
            CurrentSouls = Mathf.Min(CurrentSouls + amount, _data.MaxSouls);
            RaiseChanged();
            CheckBoundaries();
        }

        public bool TrySpend(float amount, SoulSpendReason reason)
        {
            if (amount < 0f) return false;
            if (CurrentSouls < amount) return false;
            CurrentSouls -= amount;
            RaiseChanged();
            CheckBoundaries();
            return true;
        }

        public void Spend(float amount, SoulSpendReason reason)
        {
            if (amount <= 0f) return;
            CurrentSouls = Mathf.Max(0f, CurrentSouls - amount);
            RaiseChanged();
            CheckBoundaries();
        }

        private void RaiseChanged()
        {
            SoulsChanged?.Invoke(CurrentSouls, _data.MaxSouls);
        }

        private void CheckBoundaries()
        {
            bool depletedNow = CurrentSouls <= 0f;
            if (depletedNow && !_wasDepleted)
                SoulsDepleted?.Invoke();
            _wasDepleted = depletedNow;

            bool fullNow = CurrentSouls >= _data.MaxSouls;
            if (fullNow && !_wasFull)
                SoulsFull?.Invoke();
            _wasFull = fullNow;
        }
    }
}