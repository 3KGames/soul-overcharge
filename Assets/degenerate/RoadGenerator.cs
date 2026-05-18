using System.Collections.Generic;
using UnityEngine;
using static RoadSegment;

public class RoadGenerator : MonoBehaviour
{
    [Header("Префабы сегментов")]
    public GameObject[] straightPrefabs;
    public GameObject[] turnLeftPrefabs;
    public GameObject[] turnRightPrefabs;
    public GameObject[] hillUpPrefabs;
    public GameObject[] hillDownPrefabs;

    [Header("Настройки генерации")]
    public Transform player;
    public int segmentsAhead = 10;
    public float spawnDistanceThreshold = 60f;

    [Header("Ограничения чередования")]
    public int maxSameTurnInRow = 1;
    public int maxHillsInRow = 2;

    [Header("Веса появления (0 = не появляется)")]
    [Range(0, 10)] public int weightStraight = 5;
    [Range(0, 10)] public int weightTurnLeft = 2;
    [Range(0, 10)] public int weightTurnRight = 2;
    [Range(0, 10)] public int weightHillUp = 1;
    [Range(0, 10)] public int weightHillDown = 1;

    private List<RoadSegment> activeSegments = new List<RoadSegment>();
    private Transform lastExitPoint;
    private int lastExitLanes = -1;

    private SegmentType lastSpawnedType = SegmentType.Straight;
    private int sameTurnCount = 0;
    private int hillCount = 0;
    private bool lastHillWasUp = false;

    void Start()
    {
        lastExitPoint = this.transform;
        lastExitLanes = -1;

        for (int i = 0; i < segmentsAhead; i++)
            SpawnSegment();
    }

    void Update()
    {
        if (activeSegments.Count == 0) return;

        RoadSegment lastSeg = activeSegments[activeSegments.Count - 1];
        float distToEnd = Vector3.Distance(player.position, lastSeg.exitPoint.transform.position);

        if (distToEnd < spawnDistanceThreshold)
        {
            SpawnSegment();
            RemoveOldSegment();
        }
    }

    void SpawnSegment()
    {
        GameObject prefab = PickPrefab();
        if (prefab == null)
        {
            Debug.LogError($"Не найден подходящий префаб с {lastExitLanes} линиями на входе!");
            return;
        }

        GameObject go = Instantiate(prefab);
        RoadSegment seg = go.GetComponent<RoadSegment>();

        if (seg == null)
        {
            Debug.LogError($"Префаб {prefab.name} не имеет компонента RoadSegment!");
            Destroy(go);
            return;
        }

        AlignSegment(seg, lastExitPoint);

        activeSegments.Add(seg);
        lastExitPoint = seg.exitPoint.transform;
        lastExitLanes = seg.exitPoint.NumLanes;
        lastSpawnedType = seg.segmentType;

        seg.GetComponent<TrackEnemySpawner>()?.TrySpawnEnemies();
    }

    GameObject PickPrefab()
    {
        var candidates = FilterByLanes(BuildCandidateList());

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"Нет кандидатов с {lastExitLanes} линиями при текущих ограничениях. Снимаем ограничения.");
            candidates = FilterByLanes(BuildCandidateListNoRestrictions());
        }

        if (candidates.Count == 0) return null;

        return WeightedRandomPrefab(candidates);
    }

    List<(GameObject prefab, SegmentType type, int weight)> BuildCandidateList()
    {
        var list = new List<(GameObject, SegmentType, int)>();

        bool blockLeft     = lastSpawnedType == SegmentType.TurnLeft  && sameTurnCount >= maxSameTurnInRow;
        bool blockRight    = lastSpawnedType == SegmentType.TurnRight && sameTurnCount >= maxSameTurnInRow;
        bool blockHills    = hillCount >= maxHillsInRow;
        bool blockHillUp   = blockHills || lastSpawnedType == SegmentType.HillDown;
        bool blockHillDown = blockHills || lastSpawnedType == SegmentType.HillUp;

        AddToList(list, straightPrefabs,  SegmentType.Straight,  weightStraight);
        if (!blockLeft)     AddToList(list, turnLeftPrefabs,  SegmentType.TurnLeft,  weightTurnLeft);
        if (!blockRight)    AddToList(list, turnRightPrefabs, SegmentType.TurnRight, weightTurnRight);
        if (!blockHillUp)   AddToList(list, hillUpPrefabs,    SegmentType.HillUp,    weightHillUp);
        if (!blockHillDown) AddToList(list, hillDownPrefabs,  SegmentType.HillDown,  weightHillDown);

        return list;
    }

    List<(GameObject prefab, SegmentType type, int weight)> BuildCandidateListNoRestrictions()
    {
        var list = new List<(GameObject, SegmentType, int)>();
        AddToList(list, straightPrefabs,  SegmentType.Straight,  weightStraight);
        AddToList(list, turnLeftPrefabs,  SegmentType.TurnLeft,  weightTurnLeft);
        AddToList(list, turnRightPrefabs, SegmentType.TurnRight, weightTurnRight);
        AddToList(list, hillUpPrefabs,    SegmentType.HillUp,    weightHillUp);
        AddToList(list, hillDownPrefabs,  SegmentType.HillDown,  weightHillDown);
        return list;
    }

    void AddToList(List<(GameObject, SegmentType, int)> list, GameObject[] prefabs, SegmentType type, int weight)
    {
        if (prefabs == null || prefabs.Length == 0 || weight == 0) return;
        foreach (var p in prefabs)
            if (p != null) list.Add((p, type, weight));
    }

    List<(GameObject prefab, SegmentType type, int weight)> FilterByLanes(
        List<(GameObject prefab, SegmentType type, int weight)> candidates)
    {
        if (lastExitLanes == -1) return candidates;

        var filtered = new List<(GameObject, SegmentType, int)>();

        foreach (var (prefab, type, weight) in candidates)
        {
            RoadSegment seg = prefab.GetComponent<RoadSegment>();
            if (seg == null) continue;

            if (seg.entryPoint.NumLanes == lastExitLanes)
                filtered.Add((prefab, type, weight));
        }

        return filtered;
    }

    GameObject WeightedRandomPrefab(List<(GameObject prefab, SegmentType type, int weight)> candidates)
    {
        int total = 0;
        foreach (var c in candidates) total += c.weight;

        int roll = Random.Range(0, total);
        int cumulative = 0;

        foreach (var (prefab, type, weight) in candidates)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                UpdateCounters(type);
                return prefab;
            }
        }

        UpdateCounters(candidates[0].type);
        return candidates[0].prefab;
    }

    void UpdateCounters(SegmentType chosen)
    {
        if (chosen == lastSpawnedType &&
           (chosen == SegmentType.TurnLeft || chosen == SegmentType.TurnRight))
            sameTurnCount++;
        else
            sameTurnCount = 0;

        if (chosen == SegmentType.HillUp || chosen == SegmentType.HillDown)
        {
            hillCount++;
            lastHillWasUp = chosen == SegmentType.HillUp;
        }
        else
        {
            hillCount = 0;
        }
    }

    void AlignSegment(RoadSegment seg, Transform targetExit)
    {
        Quaternion rotationDiff = targetExit.rotation * Quaternion.Inverse(seg.entryPoint.transform.rotation);
        seg.transform.rotation = rotationDiff * seg.transform.rotation;

        Vector3 posDiff = targetExit.position - seg.entryPoint.transform.position;
        seg.transform.position += posDiff;
    }

    void RemoveOldSegment()
    {
        while (activeSegments.Count > segmentsAhead + 3)
        {
            activeSegments[0].GetComponent<TrackEnemySpawner>()?.DespawnEnemies();
            Destroy(activeSegments[0].gameObject);
            activeSegments.RemoveAt(0);
        }
    }

    public void ResetGenerator()
    {
        foreach (var seg in activeSegments)
        {
            if (seg == null) continue;
            seg.GetComponent<TrackEnemySpawner>()?.DespawnEnemies();
            Destroy(seg.gameObject);
        }

        activeSegments.Clear();
        lastExitPoint = this.transform;
        lastExitLanes = -1;
        lastSpawnedType = SegmentType.Straight;
        sameTurnCount = 0;
        hillCount = 0;

        for (int i = 0; i < segmentsAhead; i++)
            SpawnSegment();
    }
}