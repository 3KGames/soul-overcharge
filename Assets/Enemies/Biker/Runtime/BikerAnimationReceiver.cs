using FMODUnity;
using UnityEngine;

namespace Enemies.Biker.Runtime
{
	public class BikerAnimationReceiver : MonoBehaviour
	{
		[SerializeField]
		private EventReference shotgunEvent1;
		[SerializeField]
		private EventReference shotgunEvent2; 
		
		private BikerContext _context;

		private void Awake()
		{
			_context = GetComponentInParent<BikerContext>();
		}

		public void OpenAttackZone()
		{
			if (_context != null)
			{
				_context.OpenAttackZone();
			}
		}

		public void CloseAttackZone()
		{
			if (_context != null)
			{
				_context.CloseAttackZone();
			}
		}
		
		public void PlaySound_Shotgun1()
		{
			RuntimeManager.PlayOneShot(shotgunEvent1, transform.position);
		}
		
		public void PlaySound_Shotgun2()
		{
			RuntimeManager.PlayOneShot(shotgunEvent2, transform.position);
		}
	}
}