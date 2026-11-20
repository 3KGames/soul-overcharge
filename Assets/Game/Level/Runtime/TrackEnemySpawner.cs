using System.Collections.Generic;
using Common.Runtime;
using Enemies;
using Game.Enemies;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// Добавлен неймспейс для PlayerTracker

namespace Game.Level.Runtime
{
	public class TrackEnemySpawner : MonoBehaviour
	{
		// Структура для хранения заспавненного объекта и его тега
		private struct SpawnedEnemyData
		{
			public GameObject Instance;
			public EnemyTag Tag;
		}

		public List<EnemySpawnEntry> enemyEntries = new();

		[Range(0f, 1f)]
		public float globalSpawnChance = 1f;

		[Header("Despawn Settings")]
		[Tooltip("Дистанция от игрока, при превышении которой Biker и SoulVOZ будут деспавниться")]
		public float despawnDistance = 150f;

		private EnemySpawnPoint[] _spawnPoints;
		private readonly List<SpawnedEnemyData> _spawnedEnemies = new();
		private bool _hasSpawned;
    
		private LifetimeScope _parentScope;
		private PlayerTracker _playerTracker;
		private RoadSegmentView _entryView;

		// Инжектим PlayerTracker
		[Inject]
		public void Construct(LifetimeScope parentScope, PlayerTracker playerTracker)
		{
			_parentScope = parentScope;
			_playerTracker = playerTracker;
		}

		private void Awake()
		{
			_spawnPoints = GetComponentsInChildren<EnemySpawnPoint>();
			_entryView = GetComponent<RoadSegmentView>();

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
						point.ArmForAmbush(entry.prefab, this);
					else
						PerformSpawn(point, entry.prefab);
				}
			}
		}

		public void SpawnFromTrigger(EnemySpawnPoint point, GameObject prefab)
		{
			PerformSpawn(point, prefab);
		}

		private void PerformSpawn(EnemySpawnPoint point, GameObject prefab)
		{
			if (_parentScope == null)
			{
				Debug.LogError("[TrackEnemySpawner] _parentScope is null — инжект не прошёл!");
				return;
			}

			GameObject enemy;
			var scopePrefab = prefab.GetComponent<LifetimeScope>();

			if (scopePrefab != null)
			{
				using (LifetimeScope.EnqueueParent(_parentScope))
				{
					enemy = Instantiate(prefab, point.transform.position, point.transform.rotation, null);
				}
			}
			else
			{
				enemy = _parentScope.Container.Instantiate(
					prefab, point.transform.position, point.transform.rotation, null);
			}

			// VARIANT B: Determine which RoadSegmentView this spawn point belongs to
			RoadSegmentView spawnView = point.GetComponentInParent<RoadSegmentView>();
			if (spawnView == null) spawnView = _entryView; // Fallback
			 
			if (enemy != null && spawnView != null)
			{
				if (enemy.TryGetComponent<EnemyController>(out var ctrl))
				{
					// Initialize with the specific view (Entry or Exit)
					ctrl.InitializeWithSegment(spawnView);
				}
				else
				{
					Debug.LogWarning($"[TrackEnemySpawner] Спавненный префаб {prefab.name} не имеет EnemyController!", this);
				}
			}

			// Находим тег по префабу и сохраняем его вместе с инстансом
			EnemyTag tag = GetTagForPrefab(prefab);
			_spawnedEnemies.Add(new SpawnedEnemyData { Instance = enemy, Tag = tag });
		}

		public void DespawnEnemies()
		{
			// Проходимся по списку с конца, так как будем удалять элементы
			for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
			{
				var data = _spawnedEnemies[i];
            
				if (data.Instance == null)
				{
					_spawnedEnemies.RemoveAt(i);
					continue;
				}

				// Delegate to EnemyController (polymorphic despawn)
				var ctrl = data.Instance.GetComponent<EnemyController>();
				if (ctrl != null)
				{
					ctrl.Despawn();
				}
				else
				{
					Destroy(data.Instance);
				}
            
				// В любом случае убираем из списка. 
				// Если враг выжил (уехал на другой сегмент), этот спавнер больше за него не отвечает.
				_spawnedEnemies.RemoveAt(i);
			}

			foreach (var point in _spawnPoints)
				point.Disarm();

			_hasSpawned = false;
		}

		private void DestroyEnemy(GameObject e)
		{
			var enemy = e.GetComponent<IEnemy>();
			if (enemy != null)
				enemy.ForceDestroy();
			else
				Destroy(e);
		}

		private EnemyTag GetTagForPrefab(GameObject prefab)
		{
			foreach (var entry in enemyEntries)
			{
				if (entry.prefab == prefab) return entry.enemyTag;
			}
			return EnemyTag.None;
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
}