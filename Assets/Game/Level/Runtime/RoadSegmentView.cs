using System;
using Game.Common.Runtime;
using Game.Level.Runtime;
using System.Collections.Generic;
using System.Linq;
using Dreamteck.Splines;
using UnityEngine;
using NaughtyAttributes;
using UnityEditor;

[System.Serializable]
public class LaneTopology
{
    public int laneIndex;
    public List<Vector2Int> segments = new List<Vector2Int>();
}

public class RoadSegmentView : MonoBehaviour, IRoadPath
{
    public SplineComputer spline;

    [Header("Topology (baked)")]
    [ReadOnly] public float roadLength;
    [ReadOnly] public int laneCount;
    [ReadOnly] public int[] leftEdgeProfile;
    [ReadOnly] public int[] rightEdgeProfile;
    [ReadOnly] public float laneWidth = 3.5f;
    [ReadOnly] public List<LaneTopology> serializedTopology = new List<LaneTopology>();

    [Header("Connections")]
    public RoadSegmentView previousRoad;
    public RoadSegmentView nextRoad;
    public int[] exitFirstLane = Array.Empty<int>();
    public int[] exitLaneCount = Array.Empty<int>();

    [Header("Fork Structure (Variant B)")]
    public RoadSegmentView[] childViews = Array.Empty<RoadSegmentView>();
    public bool isForkEntry = false;

    public RoadSegment Segment { get; private set; }

    public void Awake()
    {
        if (spline == null) spline = GetComponent<SplineComputer>();
        Segment = GetComponentInParent<RoadSegment>();
        if (Segment != null) laneWidth = Segment.laneWidth;
        roadLength = spline != null ? spline.CalculateLength() : 0f;
    }

    public void SetExitLanes(int[] firstLanes, int[] laneCounts)
    {
        exitFirstLane = firstLanes ?? Array.Empty<int>();
        exitLaneCount = laneCounts ?? Array.Empty<int>();
    }


    public SplineSample SampleAtDistance(float distance, Spline.Direction dir = Spline.Direction.Forward)
    {
        if (spline == null) return default;
        double percent = spline.Travel(0, distance, dir);
        return spline.Evaluate(percent);
    }

    public float Length => roadLength;
	public float LaneWidth => laneWidth;

	public (float left, float right) GetRoadWidthAt(float distance)
    {
        float halfLane = laneWidth * 0.5f;
        if (leftEdgeProfile == null || rightEdgeProfile == null || leftEdgeProfile.Length == 0)
        {
            float halfWidth = laneWidth * (laneCount - 1) * 0.5f + halfLane;
            return (halfWidth, halfWidth);
        }

        float tileLen = Length / leftEdgeProfile.Length;
        int tile = Mathf.Clamp(Mathf.FloorToInt(distance / tileLen), 0, leftEdgeProfile.Length - 1);

        int leftmostActive = Mathf.Clamp(leftEdgeProfile[tile], 0, laneCount - 1);
        int rightmostActive = Mathf.Clamp(rightEdgeProfile[tile], 0, laneCount - 1);

        float centerLane = (laneCount - 1) * 0.5f;
        float leftEdge = (leftmostActive - centerLane) * laneWidth - halfLane;
        float rightEdge = (rightmostActive - centerLane) * laneWidth + halfLane;

        return (-leftEdge, rightEdge);
    }

    public float GetLaneOffset(int laneIndex)
    {
        if (laneIndex < 0 || laneIndex >= laneCount)
            throw new System.ArgumentOutOfRangeException(nameof(laneIndex),
                $"laneIndex {laneIndex} out of range [0, {laneCount - 1}]");
        float centerLane = (laneCount - 1) * 0.5f;
        return (laneIndex - centerLane) * laneWidth;
    }

    public int LaneCount => laneCount;

    public System.Collections.Generic.IEnumerable<IRoadPath> GetNextPaths(int laneIndex)
    {
        if (isForkEntry)
        {
            if (childViews == null || exitFirstLane == null) yield break;
            for (int i = 0; i < childViews.Length; i++)
            {
                if (childViews[i] == null) continue;
                if (i >= exitFirstLane.Length) continue;
                int first = exitFirstLane[i];
                int count = i < exitLaneCount.Length ? exitLaneCount[i] : 1;
                if (laneIndex >= first && laneIndex < first + count)
                    yield return childViews[i];
            }
        }
        else
        {
            if (nextRoad != null) yield return nextRoad;
        }
    }

	public bool TryGetNextPath(int laneIndex, out IRoadPath nextPath)
	{
		if (isForkEntry && childViews != null && exitFirstLane != null && exitLaneCount != null)
		{
			// TODO: Clean after refactoring
			int count = Mathf.Min(childViews.Length, Mathf.Min(exitFirstLane.Length, exitLaneCount.Length));

			for (int i = 0; i < count; i++)
			{
				if (laneIndex >= exitFirstLane[i] && laneIndex < exitFirstLane[i] + exitLaneCount[i])
				{
					if (childViews[i] != null)
					{
						nextPath = childViews[i];
						return true;
					}
				}
			}

			nextPath = null;
			return false;
		}

		if (nextRoad != null)
		{
			nextPath = nextRoad;
			return true;
		}

#if UNITY_EDITOR
		Debug.LogError("Next road isn't assigned for: " + this.name);
#endif
		
		nextPath = null;
		return false;
	}

	public IRoadPath PreviousPath => previousRoad;

    public SplineSample GetClosestSample(Vector3 worldPosition)
    {
        if (spline == null) return default;
        return spline.Project(worldPosition);
    }

    public int GetLaneAtPosition(Vector3 worldPosition)
    {
        var sample = GetClosestSample(worldPosition);
        if (sample.forward.sqrMagnitude < 0.001f) return 0;

        Vector3 toPos = worldPosition - sample.position;
        float lateral = Vector3.Dot(toPos, sample.right);

        float distance = (float)(sample.percent * Length);
        int bestLane = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < laneCount; i++)
        {
            if (!IsLaneActiveAt(i, distance)) continue;
            float laneCenter = GetLaneOffset(i);
            float dist = Mathf.Abs(lateral - laneCenter);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestLane = i;
            }
        }
        return bestLane;
    }

    public bool IsLaneActiveAt(int laneIndex, float distance)
    {
        if (laneIndex < 0 || laneIndex >= laneCount) return false;
        if (leftEdgeProfile == null || rightEdgeProfile == null || leftEdgeProfile.Length == 0)
            return true;

        float tileLen = Length / leftEdgeProfile.Length;
        int tile = Mathf.Clamp(Mathf.FloorToInt(distance / tileLen), 0, leftEdgeProfile.Length - 1);
        return laneIndex >= leftEdgeProfile[tile] && laneIndex <= rightEdgeProfile[tile];
    }

    public void SetupForkEntry(RoadSegmentView[] children, int[] firstLanes, int[] laneCounts)
    {
        childViews = children;
        exitFirstLane = firstLanes;
        exitLaneCount = laneCounts;
        isForkEntry = true;

        foreach (var cv in children)
            if (cv != null) cv.previousRoad = this;
    }

    public void SetupLinear(RoadSegmentView prev, RoadSegmentView next, int[] firstLanes = null, int[] laneCounts = null)
    {
        previousRoad = prev;
        nextRoad = next;
        exitFirstLane = firstLanes ?? new[] { 0 };
        exitLaneCount = laneCounts ?? new[] { laneCount };
        isForkEntry = false;
        childViews = Array.Empty<RoadSegmentView>();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (spline == null || laneCount <= 0) return;

        float thick = 5f;
        float thin = 2.5f;

        using (new Handles.DrawingScope(isForkEntry ? new Color(0f, 0.8f, 1f, 0.8f) : new Color(0.3f, 1f, 0.3f, 0.8f)))
        {
            DrawSplineWireHandles(spline, 30, thick);
        }

        if (leftEdgeProfile != null && rightEdgeProfile != null && leftEdgeProfile.Length > 0)
        {
            using (new Handles.DrawingScope(new Color(1f, 0.5f, 0f, 0.9f)))
            {
                float tileLen = Length / leftEdgeProfile.Length;
                var leftPoints = new List<Vector3>();
                var rightPoints = new List<Vector3>();

                for (int i = 0; i <= leftEdgeProfile.Length; i++)
                {
                    float d = Mathf.Min(i * tileLen, Length);
                    var s = SampleAtDistance(d);
                    var (left, right) = GetRoadWidthAt(d);

                    leftPoints.Add(s.position - s.right * left);
                    rightPoints.Add(s.position + s.right * right);
                }

                Handles.DrawAAPolyLine(thick, leftPoints.ToArray());
                Handles.DrawAAPolyLine(thick, rightPoints.ToArray());
            }
        }
        else
        {
            using (new Handles.DrawingScope(new Color(1f, 0.5f, 0f, 0.7f)))
            {
                float halfWidth = laneWidth * (laneCount - 1) / 2f + laneWidth * 0.5f;
                DrawSplineOffsetHandles(spline, -halfWidth, 30, thick);
                DrawSplineOffsetHandles(spline, halfWidth, 30, thick);
            }
        }

        using (new Handles.DrawingScope(new Color(0f, 1f, 0f, 0.9f)))
        {
            float gizmoTileLen = (leftEdgeProfile != null && leftEdgeProfile.Length > 0)
                ? Length / leftEdgeProfile.Length
                : Length / 10f;

            for (int lane = 0; lane < laneCount; lane++)
            {
                var run = new List<Vector3>();
                for (float d = 0; d <= Length; d += gizmoTileLen)
                {
                    float dist = Mathf.Min(d, Length);
                    if (!IsLaneActiveAt(lane, dist))
                    {
                        if (run.Count > 1) Handles.DrawAAPolyLine(thin, run.ToArray());
                        run.Clear();
                        continue;
                    }
                    var s = SampleAtDistance(dist);
                    run.Add(s.position + s.right * GetLaneOffset(lane));
                }
                if (run.Count > 1) Handles.DrawAAPolyLine(thin, run.ToArray());
            }
        }

        if (isForkEntry && childViews != null)
        {
            using (new Handles.DrawingScope(Color.magenta))
            {
                Vector3 entryEnd = spline.Evaluate(1).position;
                foreach (var cv in childViews)
                    if (cv != null && cv.spline != null)
                        Handles.DrawAAPolyLine(thick, entryEnd, cv.spline.Evaluate(0).position);
            }
        }
        if (!isForkEntry && previousRoad != null && previousRoad.spline != null)
        {
            using (new Handles.DrawingScope(new Color(0f, 0.8f, 1f, 0.9f)))
            {
                Handles.DrawAAPolyLine(thick, previousRoad.spline.Evaluate(1).position, spline.Evaluate(0).position);
            }
        }
    }

    private void DrawSplineWireHandles(SplineComputer spline, int segments, float thickness)
    {
        var points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            double p = (double)i / segments;
            points[i] = spline.Evaluate(p).position;
        }
        Handles.DrawAAPolyLine(thickness, points);
    }

    private void DrawSplineOffsetHandles(SplineComputer spline, float offset, int segments, float thickness)
    {
        var points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            double p = (double)i / segments;
            var s = spline.Evaluate(p);
            points[i] = s.position + s.right * offset;
        }
        Handles.DrawAAPolyLine(thickness, points);
    }
#endif

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

    public void RebuildTopologyFromProfiles()
    {
        if (leftEdgeProfile == null || rightEdgeProfile == null || leftEdgeProfile.Length == 0)
        {
            Debug.LogWarning($"[RoadSegmentView] {name}: No edge profiles to rebuild topology from");
            return;
        }

        if (laneCount <= 0)
        {
            Debug.LogWarning($"[RoadSegmentView] {name}: laneCount is {laneCount}, cannot rebuild topology");
            return;
        }

        var map = new Dictionary<int, List<Vector2Int>>();
        int totalTiles = leftEdgeProfile.Length;

        for (int lane = 0; lane < laneCount; lane++)
        {
            var segments = new List<Vector2Int>();
            bool inSegment = false;
            int segmentStart = 0;

            for (int tile = 0; tile < totalTiles; tile++)
            {
                bool isActive = lane >= leftEdgeProfile[tile]
                    && lane <= rightEdgeProfile[tile];

                if (isActive && !inSegment)
                {
                    segmentStart = tile;
                    inSegment = true;
                }
                else if (!isActive && inSegment)
                {
                    segments.Add(new Vector2Int(segmentStart, tile));
                    inSegment = false;
                }
            }

            if (inSegment)
            {
                segments.Add(new Vector2Int(segmentStart, totalTiles));
            }

            if (segments.Count > 0)
            {
                map[lane] = segments;
            }
        }

        serializedTopology.Clear();
        foreach (var kvp in map)
        {
            serializedTopology.Add(new LaneTopology { laneIndex = kvp.Key, segments = new List<Vector2Int>(kvp.Value) });
        }

        Debug.Log($"[RoadSegmentView] {name}: Rebuilt topology for {laneCount} lanes, {map.Count} active lanes");
    }
}