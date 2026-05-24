using FMODUnity;
using UnityEngine;

namespace Enemies.scripts
{
	public class BombSound : MonoBehaviour
	{
		[SerializeField] private BombProjectile bomb;
		
		[SerializeField]
		private EventReference explosionSound;

		public void Start()
		{
			bomb.OnExplode += Play;
		}

		public void Play()
		{
			RuntimeManager.PlayOneShot(explosionSound, transform.position);
		}
	}
}