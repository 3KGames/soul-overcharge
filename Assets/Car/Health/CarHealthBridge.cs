using Car.Health.Services;
using Level.Runtime;
using UnityEngine;
using VContainer;

namespace Car.Health
{
    public class CarHealthBridge : MonoBehaviour
    {
        private HealthService   _healthService;
        private GameOverService _gameOverService;

        [Inject]
        public void Construct(HealthService healthService, GameOverService gameOverService)
        {
            _healthService   = healthService;
            _gameOverService = gameOverService;
        }

        private void Start()
        {
            if (_healthService != null)
                _healthService.Died += OnDied;
        }

        private void OnDied()
        {
            _gameOverService?.TriggerGameOver();
        }

        public void TakeDamage(float amount)
        {
            _healthService?.TakeDamage(amount);
        }

        public void Heal(float amount)
        {
            _healthService?.Heal(amount);
        }

        private void OnDestroy()
        {
            if (_healthService != null)
                _healthService.Died -= OnDied;
        }
    }
}