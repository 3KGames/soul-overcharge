using UnityEngine;

namespace degenerate
{
	public class ConnectionPoint: MonoBehaviour
	{
		[Range(0, 15)]
		[SerializeField] private int numLanes = 3;
		
		public int NumLanes => numLanes;
	}
}