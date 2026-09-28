using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;

[System.Serializable]
public class LaneTopology
{
	public int laneIndex;
	public List<Vector2Int> segments = new List<Vector2Int>();
}

public class RoadSegmentView : MonoBehaviour
{
	public SplineComputer spline;

	[Header("Связи с другими дорогами")]
	public RoadSegmentView previousRoad;

	[Tooltip("Первая живая дорога из nextRoads. Для обычных сегментов — единственная следующая")]
	public RoadSegmentView nextRoad;

	[Tooltip("Следующая дорога для каждого выхода (индекс = индекс в RoadSegment.exits). У развилки их несколько")]
	public RoadSegmentView[] nextRoads = Array.Empty<RoadSegmentView>();

	[Tooltip("Для каждого выхода: первая полоса входа, которая ведёт в него (заполняет генератор)")]
	public int[] exitFirstLane = Array.Empty<int>();

	[Tooltip("Для каждого выхода: сколько полос входа ведёт в него (заполняет генератор)")]
	public int[] exitLaneCount = Array.Empty<int>();

	[Header("Кэшированные данные")]
	public int laneCount;
	public float roadLength;

	[HideInInspector]
	public List<LaneTopology> serializedTopology = new List<LaneTopology>();

	public void Awake()
	{
		spline = GetComponent<SplineComputer>();
	}

	public void SetNextRoad(int exitIndex, int exitCount, RoadSegmentView road)
	{
		if (nextRoads == null || nextRoads.Length != exitCount)
			Array.Resize(ref nextRoads, exitCount);

		nextRoads[exitIndex] = road;
		RefreshMainNext();
	}

	public void ClearNextRoad(int exitIndex)
	{
		if (nextRoads != null && exitIndex >= 0 && exitIndex < nextRoads.Length)
			nextRoads[exitIndex] = null;
		RefreshMainNext();
	}

	public void SetExitLanes(int[] firstLanes, int[] laneCounts)
	{
		exitFirstLane = firstLanes ?? Array.Empty<int>();
		exitLaneCount = laneCounts ?? Array.Empty<int>();
	}

	public RoadSegmentView GetNextRoadForLane(int lane, out int laneInNextRoad)
	{
		laneInNextRoad = lane;
		if (nextRoads == null || nextRoads.Length <= 1)
			return nextRoad;

		for (int i = 0; i < nextRoads.Length; i++)
		{
			if (nextRoads[i] == null || i >= exitFirstLane.Length) continue;

			int first = exitFirstLane[i];
			int count = i < exitLaneCount.Length ? exitLaneCount[i] : 1;
			if (lane >= first && lane < first + count)
			{
				laneInNextRoad = lane - first;
				return nextRoads[i];
			}
		}

		return nextRoad;
	}

	private void RefreshMainNext()
	{
		nextRoad = null;
		if (nextRoads == null) return;
		foreach (var road in nextRoads)
		{
			if (road != null)
			{
				nextRoad = road;
				return;
			}
		}
	}

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
