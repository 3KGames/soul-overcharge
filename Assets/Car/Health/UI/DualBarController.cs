using Car.Souls.Services;
using Car.Health.Services;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Car.UI
{
    public class DualBarController : MonoBehaviour
    {
        [SerializeField] private Image hpFill;
        [SerializeField] private Image soulFill;

        private const float HP_ZONE_MAX = 0.5f;

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
            if (_souls == null || _health == null)
            {
                Debug.LogError("[DualBarController] Зависимости null — инжекция не произошла!");
                return;
            }

            Debug.Log($"[DualBarController] Start | HP={_health.CurrentHealth}/{_health.TargetMaxHp} | Souls={_souls.Normalized}");

            _souls.SoulsChanged   += OnSoulsChanged;
            _health.HealthChanged += OnHealthChanged;

            UpdateView();
        }

        private void OnDestroy()
        {
            if (_souls  != null) _souls.SoulsChanged   -= OnSoulsChanged;
            if (_health != null) _health.HealthChanged -= OnHealthChanged;
        }

        private void OnSoulsChanged(float current, float max)
        {
            //Debug.Log($"[DualBarController] SoulsChanged: {current}/{max}");
            UpdateView();
        }

        private void OnHealthChanged(float current, float max)
        {
            //Debug.Log($"[DualBarController] HealthChanged: {current}/{max}");
            UpdateView();
        }

        private void UpdateView()
        {
            if (_souls == null || _health == null) return;

            float soulsNorm  = _souls.Normalized;
            float normalZone = Mathf.Min(HP_ZONE_MAX, 1f - soulsNorm);
            float minZone    = _health.MaxHealth > 0f
                ? _health.TargetMaxHp / _health.MaxHealth
                : 0f;
            float hpZone = Mathf.Max(normalZone, minZone);

            Debug.Log($"[DualBarController] UpdateView | soulsNorm={soulsNorm:0.00} | hpZone={hpZone:0.00} | HP={_health.CurrentHealth:0}/{_health.TargetMaxHp:0}");

            if (soulFill != null)
                soulFill.fillAmount = Mathf.Min(soulsNorm, 1f - hpZone);

            if (hpFill != null)
            {
                float hpFraction = _health.TargetMaxHp > 0f
                    ? Mathf.Clamp01(_health.CurrentHealth / _health.TargetMaxHp)
                    : 0f;
                hpFill.fillAmount = hpFraction * hpZone;
            }
        }
    }
}