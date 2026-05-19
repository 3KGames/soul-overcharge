using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class TrackEnemySpawner : MonoBehaviour
{
    [Header("Типы врагов на этом сегменте")]
    public List<EnemySpawnEntry> enemyEntries = new();

    [Header("Общий шанс появления врагов на сегменте (0–1)")]
    [Range(0f, 1f)]
    public float globalSpawnChance = 1f;

    private EnemySpawnPoint[] _spawnPoints;
    private readonly List<GameObject> _spawnedEnemies = new();
    private bool _hasSpawned;

    #region VContainer
    
    private LifetimeScope _parentScope;
    
    [Inject]
    public void Construct(LifetimeScope parentScope)
    {
       _parentScope = parentScope;
    }
    
    #endregion
    
    void Awake()
    {
        _spawnPoints = GetComponentsInChildren<EnemySpawnPoint>();

        if (_spawnPoints.Length == 0)
            Debug.LogWarning($"[TrackEnemySpawner] На {gameObject.name} нет EnemySpawnPoint!", this);
    }

    public void TrySpawnEnemies()
    {
        if (_hasSpawned) return;
        _hasSpawned = true;

        if (_spawnPoints.Length == 0 || enemyEntries.Count == 0) return;
        if (Random.value > globalSpawnChance) return;

        foreach (var entry in enemyEntries)
        {
            if (entry.prefab == null) continue;
            if (Random.value > entry.spawnChance) continue;

            List<EnemySpawnPoint> validPoints = GetValidPoints(entry.enemyTag);
            if (validPoints.Count == 0) continue;

            List<EnemySpawnPoint> picked = PickRandom(validPoints, entry.spawnCount);

            foreach (var point in picked)
            {
                if (point.spawnOnTrigger)
                {
                    point.ArmForAmbush(entry.prefab, this);
                }
                else
                {
                    PerformSpawn(point, entry.prefab);
                }
            }
        }
    }

    public void SpawnFromTrigger(EnemySpawnPoint point, GameObject prefab)
    {
        PerformSpawn(point, prefab);
    }

    private void PerformSpawn(EnemySpawnPoint point, GameObject prefab)
    {
        GameObject enemy;
        var scopePrefab = prefab.GetComponent<LifetimeScope>();

        if (scopePrefab != null)
        {
            using (LifetimeScope.EnqueueParent(_parentScope))
            {
                enemy = Instantiate(
                    prefab, 
                    point.transform.position, 
                    point.transform.rotation, 
                    transform
                );
            }
        }
        else
        {
            enemy = _parentScope.Container.Instantiate(
                prefab, 
                point.transform.position, 
                point.transform.rotation, 
                transform
            );
        }
                
        _spawnedEnemies.Add(enemy);
    }

    public void DespawnEnemies()
    {
        foreach (var e in _spawnedEnemies)
        {
            if (e != null) Destroy(e);
        }
        _spawnedEnemies.Clear();

        foreach (var point in _spawnPoints)
        {
            point.Disarm();
        }

        _hasSpawned = false;
    }

    private List<EnemySpawnPoint> GetValidPoints(EnemyTag enemyTag)
    {
        var result = new List<EnemySpawnPoint>();
        foreach (var p in _spawnPoints)
            if (p.AllowsEnemy(enemyTag)) result.Add(p);
        return result;
    }

    private static List<T> PickRandom<T>(List<T> source, int k)
    {
        var pool = new List<T>(source);
        k = Mathf.Min(k, pool.Count);

        for (int i = 0; i < k; i++)
        {
            int j = Random.Range(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.GetRange(0, k);
    }
}