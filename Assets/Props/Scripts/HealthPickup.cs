using System;
using Car.Health.Data;
using UnityEngine;

namespace Car.Health.Pickups
{
    [RequireComponent(typeof(Collider))]
    public class HealthPickup : MonoBehaviour
    {
        [SerializeField] private HealthPickupData _data;

        private bool _collected;

		public Action OnCollected;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected) return;
            if (!other.CompareTag("Player")) return;

            var bridge = other.GetComponentInParent<CarHealthBridge>();
            if (bridge == null)
            {
                Debug.LogWarning("[HealthPickup] CarHealthBridge не найден на машине!");
                return;
            }

            Collect(bridge);
        }

        private void Collect(CarHealthBridge bridge)
        {
            _collected = true;

            bridge.Heal(_data.HealAmount);
			OnCollected?.Invoke();
			
            Debug.Log($"[HealthPickup] Подобран: +{_data.HealAmount} HP");

            Destroy(gameObject);
        }
    }
}