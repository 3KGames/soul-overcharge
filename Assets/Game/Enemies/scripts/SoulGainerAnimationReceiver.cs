using FMODUnity;
using Pickups;
using UnityEngine;

namespace Enemies.Biker.Runtime
{
	public class SoulGainerAnimationReceiver : MonoBehaviour
	{
		[SerializeField]
		private EventReference soulGainerDeathEvent;
		
		private SoulGainer _context;

		private void Awake()
		{
			_context = GetComponentInParent<SoulGainer>();
		}

		public void OnDeathAnimationComplete()
		{
			_context.OnDeathAnimationComplete();
		}
		
		public void PlaySound_Destroy()
		{
			RuntimeManager.PlayOneShot(soulGainerDeathEvent, transform.position);
		}
	}
}