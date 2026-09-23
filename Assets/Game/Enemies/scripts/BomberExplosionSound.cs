using FMODUnity;
using UnityEngine;

namespace Enemies.scripts
{
	public class BomberExplosionSound : MonoBehaviour
	{
		[SerializeField] private BomberEnemy bomber;
		
		[SerializeField]
		private EventReference explosionSound;

		public void Start()
		{
			bomber.OnExplode += Play;
		}

		public void Play()
		{
			RuntimeManager.PlayOneShot(explosionSound, transform.position);
		}
	}
}