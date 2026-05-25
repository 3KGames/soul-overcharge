using Car.Health;
using UnityEngine;

namespace Enemies.Biker.Runtime
{
	public class BikerAttackZone : MonoBehaviour
	{
		private float _damage;
		private bool _isActive;
		
		private Collider _collider;

		private void Awake()
		{
			_collider = GetComponent<Collider>();
		}

		public void Setup(float damage)
		{
			_damage = damage;
		}

		public void SetActive(bool active)
		{
			_isActive = active;
			_collider.enabled = active;
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!_isActive) return;
			Debug.LogWarning("111");

			if (other.TryGetComponent<CarHealthBridge>(out var playerHealth))
			{
				Debug.LogWarning("222");
				playerHealth.TakeDamage(_damage);
                
				_isActive = false; 
			}
		}
	}
}