using Car.Souls.Data;
using Car.Souls.Services;
using UnityEngine;
using VContainer;

namespace Pickups
{
    public class SoulGainer : MonoBehaviour
    {
        [Tooltip("Сколько душ даёт подбор. -1 = брать значение из SoulData.SoulsPerKill")]
        [SerializeField] private float soulsAmount = -1f;
		
		[SerializeField] private Animator _animator;

        private SoulService _soulService;
        private SoulData    _soulData;
		private SoulDrainEffect _drainEffect;
        private bool        _collected;

        [Inject]
        public void Construct(SoulService soulService, SoulData soulData, SoulDrainEffect drainEffect)
        {
            _soulService = soulService;
            _soulData    = soulData;
			_drainEffect = drainEffect;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected) return;
            if (!other.CompareTag("Player")) return;

            _collected = true;

            float amount = soulsAmount >= 0f
                ? soulsAmount
                : _soulData.SoulsPerKill;

            _soulService.Add(amount, SoulSource.Pickup);
			_drainEffect.SetExternalGainPos(transform.position);
			_animator.SetTrigger("Dead");

            //Destroy(gameObject);
        }
		
		public void OnDeathAnimationComplete()
		{
			Destroy(gameObject);
		}

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.3f);
            Gizmos.DrawSphere(transform.position, 1f);
            Gizmos.color = new Color(0.6f, 0.2f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
#endif
    }
}
