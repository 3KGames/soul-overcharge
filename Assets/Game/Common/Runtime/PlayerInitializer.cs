using UnityEngine;
using VContainer;

namespace Common.Runtime
{
	[RequireComponent(typeof(Rigidbody))]
	public class PlayerInitializer : MonoBehaviour
	{
		private PlayerTracker _playerTracker;

		[Inject]
		public void Construct(PlayerTracker playerTracker)
		{
			_playerTracker = playerTracker;
		}

		private void Awake()
		{
			_playerTracker.RegisterPlayer(transform, GetComponent<Rigidbody>());
		}
	}
}