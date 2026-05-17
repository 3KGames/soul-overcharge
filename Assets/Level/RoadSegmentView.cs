using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LaneTopology
{
	public int laneIndex;
	public List<Vector2Int> segments = new List<Vector2Int>();
}

public class RoadSegmentView : MonoBehaviour
{
	[Header("Связи с другими дорогами")]
	public RoadSegmentView previousRoad;
	public RoadSegmentView nextRoad;

	[Header("Кэшированные данные")]
	public int laneCount;
	public float roadLength;
    
	[HideInInspector]
	public List<LaneTopology> serializedTopology = new List<LaneTopology>();

	public Dictionary<int, List<Vector2Int>> GetTopologyMap()
	{
		var map = new Dictionary<int, List<Vector2Int>>();
		foreach (var t in serializedTopology)
		{
			map[t.laneIndex] = t.segments;
		}
		return map;
	}

	public void SetTopologyMap(Dictionary<int, List<Vector2Int>> map)
	{
		serializedTopology.Clear();
		foreach (var kvp in map)
		{
			serializedTopology.Add(new LaneTopology { laneIndex = kvp.Key, segments = new List<Vector2Int>(kvp.Value) });
		}
	}
}