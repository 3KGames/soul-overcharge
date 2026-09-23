using Car.Souls.Data;
using Car.Souls.Services;
using Car.Health.Services;
using Common.Runtime;
using UnityEngine;
using VContainer;

namespace Enemies
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 30f;

        [SerializeField] private float soulsRewardOverride = -1f;

        [SerializeField] private float healthReward = 0f;

        private float _currentHealth;
        private bool  _isDead;

        private SoulService   _souls;
        private SoulData      _soulData;
        private HealthService _health;

        [Inject]
        public void Construct(SoulService souls, SoulData soulData, HealthService health)
        {
            _souls    = souls;
            _soulData = soulData;
            _health   = health;
        }

        private void Awake()
        {
            _currentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (_isDead || amount <= 0f) return;
            _currentHealth -= amount;
            if (_currentHealth <= 0f)
                Die();
        }

        private void Die()
        {
            _isDead = true;

            float soulsReward = soulsRewardOverride >= 0f
                ? soulsRewardOverride
                : (_soulData != null ? _soulData.SoulsPerKill : 0f);

            if (_souls != null && soulsReward > 0f)
                _souls.Add(soulsReward, SoulSource.EnemyKill);

            if (_health != null && healthReward > 0f)
                _health.Heal(healthReward);

            Destroy(gameObject);
        }
    }
}