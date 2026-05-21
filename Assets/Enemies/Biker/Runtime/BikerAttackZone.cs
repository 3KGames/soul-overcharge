using Car.Health;
using UnityEngine;

namespace Enemies.Biker.Runtime
{
	public class BikerAttackZone : MonoBehaviour
	{
		private float _damage;
		private bool _isActive;

		public void Setup(float damage)
		{
			_damage = damage;
		}

		public void SetActive(bool active)
		{
			_isActive = active;
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!_isActive) return;

			if (other.TryGetComponent<CarHealthBridge>(out var playerHealth))
			{
				playerHealth.TakeDamage(_damage);
                
				_isActive = false; 
			}
		}
	}
}