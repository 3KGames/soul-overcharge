using System;
using Car.Health.Data;
using Car.Souls.Services;
using UnityEngine;
using VContainer.Unity;

namespace Car.Health.Services
{
    public class HealthService : IInitializable, IDisposable
    {
        private readonly SoulService _souls;
        private readonly HealthData  _data;

        public float CurrentHealth { get; private set; }
        public float TargetMaxHp   { get; private set; }
        public float MaxHealth     => _data.MaxHealth;

        private float HardHpCap => _data.MaxHealth * 0.5f;

        private float MinHp => _data.MinHealth;

        public event Action<float, float> HealthChanged;
        public event Action               Died;

        private bool _isDead;

        public HealthService(SoulService souls, HealthData data)
        {
            _souls = souls;
            _data  = data;
            RecalculateMaxHp();
            CurrentHealth = TargetMaxHp;
        }

        public void Initialize()
        {
            _souls.SoulsChanged += OnSoulsChanged;
            Debug.Log($"[HealthService] Initialize → CurrentHP: {CurrentHealth} | TargetMaxHp: {TargetMaxHp} | Min: {MinHp} | HardCap: {HardHpCap}");
        }

        public void Dispose()
        {
            if (_souls != null)
                _souls.SoulsChanged -= OnSoulsChanged;
        }

        public void TakeDamage(float amount)
        {
            if (_isDead || amount <= 0f) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Debug.Log($"[HealthService] TakeDamage: -{amount} | CurrentHP: {CurrentHealth} | TargetMaxHp: {TargetMaxHp}");
            HealthChanged?.Invoke(CurrentHealth, _data.MaxHealth);
            CheckDeath();
        }

        public void Heal(float amount)
        {
            if (_isDead || amount <= 0f) return;
            CurrentHealth = Mathf.Min(TargetMaxHp, CurrentHealth + amount);
            Debug.Log($"[HealthService] Heal: +{amount} | CurrentHP: {CurrentHealth} | TargetMaxHp: {TargetMaxHp}");
            HealthChanged?.Invoke(CurrentHealth, _data.MaxHealth);
        }

        private void OnSoulsChanged(float current, float max)
        {
            RecalculateMaxHp();
            CurrentHealth = Mathf.Min(CurrentHealth, TargetMaxHp);
            HealthChanged?.Invoke(CurrentHealth, _data.MaxHealth);
            CheckDeath();
        }

        private void RecalculateMaxHp()
        {
            float t        = _souls != null && _souls.MaxSouls > 0f
                ? Mathf.Clamp01(_souls.CurrentSouls / _souls.MaxSouls)
                : 0f;

            float fraction = Mathf.Clamp01(_data.HealthBySoulsCurve.Evaluate(t));

            TargetMaxHp = Mathf.Clamp(
                fraction * _data.MaxHealth,
                MinHp,
                HardHpCap
            );
        }

        private void CheckDeath()
        {
            if (!_isDead && CurrentHealth <= 0f)
            {
                _isDead = true;
                Died?.Invoke();
            }
        }
    }
}