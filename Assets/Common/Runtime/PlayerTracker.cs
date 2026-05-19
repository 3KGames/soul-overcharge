using UnityEngine;

namespace Common.Runtime
{
	public class PlayerTracker
	{
		public Transform PlayerTransform { get; private set; }
		public Rigidbody PlayerRb { get; private set; }

		public void RegisterPlayer(Transform transform, Rigidbody rb)
		{
			PlayerTransform = transform;
			PlayerRb = rb;
		}
	}
}