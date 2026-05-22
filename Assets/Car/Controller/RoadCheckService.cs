// Controller/RoadCheckService.cs
using UnityEngine;

namespace Car.Controller
{
	public class RoadCheckService
	{
		private readonly LayerMask _roadMask;
		private readonly LayerMask _offroadMask;
		private const float RayDistance = 3f;

		public RoadCheckService(LayerMask roadMask, LayerMask offroadMask) 
		{
			_roadMask = roadMask;
			_offroadMask = offroadMask;
		}

		public (Vector3 normal, bool isOffroad) GetRoadInfo(Vector3 position)
		{
			int combinedMask = _roadMask | _offroadMask;
    
			Vector3 origin = position + Vector3.up;
			Vector3 direction = Vector3.down;

			if (Physics.Raycast(origin, direction, out var hit, RayDistance, combinedMask))
			{
				bool isOffroad = ((1 << hit.collider.gameObject.layer) & _offroadMask) != 0;
        
				Color rayColor = isOffroad ? Color.red : Color.green;
				Debug.DrawRay(origin, direction * hit.distance, rayColor);

				Debug.LogWarning(isOffroad ? "Offroad" : "Road", hit.collider.gameObject);
				return (hit.normal, isOffroad);
			}

			Debug.DrawRay(origin, direction * RayDistance, Color.gray);
    
			return (Vector3.up, false);
		}
	}
}