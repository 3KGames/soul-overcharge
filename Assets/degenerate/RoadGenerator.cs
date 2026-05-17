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

    private SegmentType lastSpawnedType = SegmentType.Straight;
    private int sameTurnCount = 0;
    private int hillCount = 0;
    private bool lastHillWasUp = false;

    void Start()
    {
        lastExitPoint = this.transform;

        for (int i = 0; i < segmentsAhead; i++)
            SpawnSegment();
    }

    void Update()
    {
        if (activeSegments.Count == 0) return;

        RoadSegment lastSeg = activeSegments[activeSegments.Count - 1];
        float distToEnd = Vector3.Distance(player.position, lastSeg.exitPoint.position);

        if (distToEnd < spawnDistanceThreshold)
        {
            SpawnSegment();
            RemoveOldSegment();
        }
    }

    void SpawnSegment()
    {
        GameObject prefab = PickPrefab();
        if (prefab == null) return;

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
        lastExitPoint = seg.exitPoint;
        lastSpawnedType = seg.segmentType;
    }

    GameObject PickPrefab()
    {
        List<(SegmentType type, int weight)> options = new List<(SegmentType, int)>();

        options.Add((SegmentType.Straight, weightStraight));

        bool blockLeft  = lastSpawnedType == SegmentType.TurnLeft  && sameTurnCount >= maxSameTurnInRow;
        bool blockRight = lastSpawnedType == SegmentType.TurnRight && sameTurnCount >= maxSameTurnInRow;

        if (!blockLeft)  options.Add((SegmentType.TurnLeft,  weightTurnLeft));
        if (!blockRight) options.Add((SegmentType.TurnRight, weightTurnRight));

        bool blockHills = hillCount >= maxHillsInRow;
        bool blockHillUp   = blockHills || (lastSpawnedType == SegmentType.HillDown);
        bool blockHillDown = blockHills || (lastSpawnedType == SegmentType.HillUp);

        if (!blockHillUp)   options.Add((SegmentType.HillUp,   weightHillUp));
        if (!blockHillDown) options.Add((SegmentType.HillDown, weightHillDown));

        SegmentType chosenType = WeightedRandom(options);

        UpdateCounters(chosenType);

        return PickFromArray(chosenType);
    }

    SegmentType WeightedRandom(List<(SegmentType type, int weight)> options)
    {
        int total = 0;
        foreach (var o in options) total += o.weight;

        int roll = Random.Range(0, total);
        int cumulative = 0;

        foreach (var o in options)
        {
            cumulative += o.weight;
            if (roll < cumulative) return o.type;
        }

        return SegmentType.Straight;
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
            lastHillWasUp = (chosen == SegmentType.HillUp);
        }
        else
        {
            hillCount = 0;
        }
    }

    GameObject PickFromArray(SegmentType type)
    {
        GameObject[] arr = type switch
        {
            SegmentType.Straight   => straightPrefabs,
            SegmentType.TurnLeft   => turnLeftPrefabs,
            SegmentType.TurnRight  => turnRightPrefabs,
            SegmentType.HillUp     => hillUpPrefabs,
            SegmentType.HillDown   => hillDownPrefabs,
            _                      => straightPrefabs
        };

        if (arr == null || arr.Length == 0)
        {
            Debug.LogWarning($"Массив префабов для {type} пуст! Используем Straight.");
            arr = straightPrefabs;
        }

        return arr[Random.Range(0, arr.Length)];
    }

    void AlignSegment(RoadSegment seg, Transform targetExit)
    {
        Quaternion rotationDiff = targetExit.rotation * Quaternion.Inverse(seg.entryPoint.rotation);
        seg.transform.rotation = rotationDiff * seg.transform.rotation;

        Vector3 posDiff = targetExit.position - seg.entryPoint.position;
        seg.transform.position += posDiff;
    }

    void RemoveOldSegment()
    {
        while (activeSegments.Count > segmentsAhead + 3)
        {
            Destroy(activeSegments[0].gameObject);
            activeSegments.RemoveAt(0);
        }
    }

    public void ResetGenerator()
    {
        foreach (var seg in activeSegments)
            if (seg != null) Destroy(seg.gameObject);

        activeSegments.Clear();
        lastExitPoint = this.transform;
        lastSpawnedType = SegmentType.Straight;
        sameTurnCount = 0;
        hillCount = 0;

        for (int i = 0; i < segmentsAhead; i++)
            SpawnSegment();
    }
}