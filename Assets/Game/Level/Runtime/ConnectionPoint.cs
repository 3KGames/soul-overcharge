using UnityEngine;

namespace Game.Level.Runtime
{

	public class ConnectionPoint : MonoBehaviour
	{
		[Range(0, 15)]
		[SerializeField] private int numLanes = 3;
		[Min(0)]
		[SerializeField] private int firstEntryLane = 0;

		public int NumLanes => numLanes;
		public int FirstEntryLane => firstEntryLane;
	}
}
