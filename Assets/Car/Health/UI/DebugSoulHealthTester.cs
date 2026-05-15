using Car.Souls.Services;
using Car.Health.Services;
using UnityEngine;
using VContainer;

namespace Car.UI
{
    public class DebugSoulHealthTester : MonoBehaviour
    {
        [SerializeField] private float soulStep   = 10f;
        [SerializeField] private float damageStep = 10f;
        [SerializeField] private float healStep   = 10f;

        private SoulService   _souls;
        private HealthService _health;

        [Inject]
        public void Construct(SoulService souls, HealthService health)
        {
            _souls  = souls;
            _health = health;
        }

        private void Start()
        {
            Debug.Log($"[Debug] START → " +
                      $"Souls: {_souls.CurrentSouls:0}/{_souls.MaxSouls} ({_souls.Normalized * 100f:0}%) | " +
                      $"CurrentHP: {_health.CurrentHealth:0} | " +
                      $"TargetMaxHP: {_health.TargetMaxHp:0} | " +
                      $"AbsoluteMax: {_health.MaxHealth:0}");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                _souls.Add(soulStep, SoulSource.Cheat);
                LogState("1 — Souls +");
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                _souls.Spend(soulStep, SoulSpendReason.AbilityCost);
                LogState("2 — Souls -");
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                _health.Heal(healStep);
                LogState("3 — Heal +");
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                _health.TakeDamage(damageStep);
                LogState("4 — Damage -");
            }
        }

        private void LogState(string action)
        {
            Debug.Log($"[Debug] Нажато: {action} → " +
                      $"Souls: {_souls.CurrentSouls:0}/{_souls.MaxSouls} ({_souls.Normalized * 100f:0}%) | " +
                      $"CurrentHP: {_health.CurrentHealth:0} | " +
                      $"TargetMaxHP: {_health.TargetMaxHp:0} | " +
                      $"AbsoluteMax: {_health.MaxHealth:0}");
        }
    }
}