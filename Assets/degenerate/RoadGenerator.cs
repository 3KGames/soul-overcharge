using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using static RoadSegment;

public class RoadGenerator : MonoBehaviour
{
    [Header("Биомы")]
    public List<RoadBiomeDefinition> biomes = new List<RoadBiomeDefinition>();

    public Transform player;
    public int segmentsAhead = 20;
    public float spawnDistanceThreshold = 60f;

    public int maxSameTurnInRow = 1;
    public int maxHillsInRow = 2;

    private List<RoadSegment> activeSegments = new List<RoadSegment>();
    private Transform lastExitPoint;
    private int lastExitLanes = -1;
    private SegmentType lastSpawnedType = SegmentType.Straight;
    private int sameTurnCount = 0;
    private int hillCount = 0;
    private bool lastHillWasUp = false;

    private RoadBiomeDefinition currentBiome;

    public BiomeTag CurrentBiomeTag => currentBiome != null ? currentBiome.biomeTag : BiomeTag.None;

    private IObjectResolver _resolver;

    [Inject]
    public void Construct(IObjectResolver resolver)
    {
        _resolver = resolver;
    }

    void Start()
    {
        lastExitPoint = this.transform;
        lastExitLanes = -1;
        currentBiome = WeightedRandomBiome(biomes);
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
        UpdateBiomeState();

        GameObject prefab = PickPrefab();
        if (prefab == null)
        {
            Debug.LogError($"Не найден подходящий префаб с {lastExitLanes} линиями на входе ни в одном биоме!");
            return;
        }

        GameObject go = _resolver.Instantiate(prefab);
        RoadSegment seg = go.GetComponent<RoadSegment>();

        if (seg == null)
        {
            Debug.LogError($"Префаб {prefab.name} не имеет компонента RoadSegment!");
            Destroy(go);
            return;
        }

        if (activeSegments.Count > 0)
        {
            seg.roadView.previousRoad = activeSegments[^1].roadView;
            activeSegments[^1].roadView.nextRoad = seg.roadView;
        }

        AlignSegment(seg, lastExitPoint);
        activeSegments.Add(seg);
        lastExitPoint = seg.exitPoint.transform;
        lastExitLanes = seg.exitPoint.NumLanes;
        lastSpawnedType = seg.segmentType;
        seg.GetComponent<TrackEnemySpawner>()?.TrySpawnEnemies();
    }

    void UpdateBiomeState()
    {
        if (biomes == null || biomes.Count == 0)
        {
            Debug.LogError("[RoadGenerator] Список biomes пуст — добавьте хотя бы один биом!");
            currentBiome = null;
            return;
        }

        if (currentBiome == null)
        {
            currentBiome = WeightedRandomBiome(biomes);
            return;
        }

        if (Random.value < currentBiome.exitChance)
        {
            var previousBiome = currentBiome;
            var next = PickNextBiome(new HashSet<RoadBiomeDefinition> { currentBiome });
            if (next != null)
            {
                currentBiome = next;
                Debug.Log($"[RoadGenerator] Вышел из биома {previousBiome.biomeTag} \u2192 новый биом: {currentBiome.biomeTag}");
            }
        }
    }

    RoadBiomeDefinition PickNextBiome(ICollection<RoadBiomeDefinition> exclude)
    {
        var candidates = new List<RoadBiomeDefinition>(biomes.Count);
        foreach (var b in biomes)
            if (!exclude.Contains(b)) candidates.Add(b);

        if (candidates.Count == 0) return null;
        return WeightedRandomBiome(candidates);
    }

    RoadBiomeDefinition WeightedRandomBiome(List<RoadBiomeDefinition> pool)
    {
        if (pool == null || pool.Count == 0) return null;

        float total = 0f;
        foreach (var b in pool) total += Mathf.Max(0f, b.weight);

        if (total <= 0f) return pool[Random.Range(0, pool.Count)];

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        foreach (var b in pool)
        {
            cumulative += Mathf.Max(0f, b.weight);
            if (roll < cumulative) return b;
        }
        return pool[pool.Count - 1];
    }

    List<(SegmentType type, int weight, List<GameObject> prefabs)> ForceBiomeSwitchAndRetry()
    {
        var tried = new HashSet<RoadBiomeDefinition> { currentBiome };

        for (int i = 0; i < biomes.Count; i++)
        {
            var next = PickNextBiome(tried);
            if (next == null) break;

            tried.Add(next);
            currentBiome = next;

            var candidates = BuildTypeCandidates(true);
            if (candidates.Count == 0)
                candidates = BuildTypeCandidates(false);

            if (candidates.Count > 0) return candidates;
        }

        return new List<(SegmentType, int, List<GameObject>)>();
    }

    GameObject PickPrefab()
    {
        if (currentBiome == null) return null;

        var candidates = BuildTypeCandidates(true);
        if (candidates.Count == 0)
            candidates = BuildTypeCandidates(false);
        if (candidates.Count == 0)
            candidates = ForceBiomeSwitchAndRetry();

        if (candidates.Count == 0) return null;
        return PickFromTypeCandidates(candidates);
    }

    List<(SegmentType type, int weight, List<GameObject> prefabs)> BuildTypeCandidates(bool respectBlocks)
    {
        var list = new List<(SegmentType, int, List<GameObject>)>();

        bool blockLeft = false, blockRight = false, blockHillUp = false, blockHillDown = false;
        if (respectBlocks)
        {
            blockLeft     = lastSpawnedType == SegmentType.TurnLeft  && sameTurnCount >= maxSameTurnInRow;
            blockRight    = lastSpawnedType == SegmentType.TurnRight && sameTurnCount >= maxSameTurnInRow;
            bool blockHills = hillCount >= maxHillsInRow;
            blockHillUp   = blockHills || lastSpawnedType == SegmentType.HillDown;
            blockHillDown = blockHills || lastSpawnedType == SegmentType.HillUp;
        }

        AddTypeCandidate(list, currentBiome.straightPrefabs,  SegmentType.Straight,  currentBiome.weightStraight);
        if (!blockLeft)     AddTypeCandidate(list, currentBiome.turnLeftPrefabs,  SegmentType.TurnLeft,  currentBiome.weightTurnLeft);
        if (!blockRight)    AddTypeCandidate(list, currentBiome.turnRightPrefabs, SegmentType.TurnRight, currentBiome.weightTurnRight);
        if (!blockHillUp)   AddTypeCandidate(list, currentBiome.hillUpPrefabs,    SegmentType.HillUp,    currentBiome.weightHillUp);
        if (!blockHillDown) AddTypeCandidate(list, currentBiome.hillDownPrefabs,  SegmentType.HillDown,  currentBiome.weightHillDown);

        return list;
    }

    void AddTypeCandidate(List<(SegmentType, int, List<GameObject>)> list, GameObject[] prefabs, SegmentType type, int weight)
    {
        if (prefabs == null || prefabs.Length == 0 || weight == 0) return;

        var valid = new List<GameObject>();
        foreach (var p in prefabs)
        {
            if (p == null) continue;
            if (lastExitLanes != -1)
            {
                RoadSegment seg = p.GetComponent<RoadSegment>();
                if (seg == null || seg.entryPoint.NumLanes != lastExitLanes) continue;
            }
            valid.Add(p);
        }

        if (valid.Count == 0) return;
        list.Add((type, weight, valid));
    }

    GameObject PickFromTypeCandidates(List<(SegmentType type, int weight, List<GameObject> prefabs)> candidates)
    {
        int total = 0;
        foreach (var c in candidates) total += c.weight;

        int roll = Random.Range(0, total);
        int cumulative = 0;
        foreach (var c in candidates)
        {
            cumulative += c.weight;
            if (roll < cumulative)
            {
                UpdateCounters(c.type);
                return c.prefabs[Random.Range(0, c.prefabs.Count)];
            }
        }

        var last = candidates[candidates.Count - 1];
        UpdateCounters(last.type);
        return last.prefabs[Random.Range(0, last.prefabs.Count)];
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
        while (activeSegments.Count > segmentsAhead + 1)
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
        currentBiome = WeightedRandomBiome(biomes);
        for (int i = 0; i < segmentsAhead; i++)
            SpawnSegment();
    }

    public RoadSegmentView GetFirstSegmentView()
    {
        if (activeSegments.Count == 0) return null;
        return activeSegments[0].roadView;
    }

    public RoadSegmentView GetSegmentBehindPlayer(int segmentsBehind)
    {
        if (activeSegments.Count < 2) return null;
        return activeSegments[0].roadView;
    }
}