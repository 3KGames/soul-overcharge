using Car.Health.Services;
using UnityEngine;
using VContainer;

namespace Car.Health
{
    public class CarHealthBridge : MonoBehaviour
    {
        [Inject] private HealthService _healthService;

        public void TakeDamage(float amount)
        {
            _healthService?.TakeDamage(amount);
        }
    }
}