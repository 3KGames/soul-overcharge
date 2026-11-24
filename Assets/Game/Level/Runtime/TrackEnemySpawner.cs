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
		private RoadSegment _roadSegment;

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
			// RoadSegment.roadView уже ссылается на правильный входной view (ForkEntry или линейный)
			_roadSegment = GetComponent<RoadSegment>();
			if (_roadSegment != null)
				_entryView = _roadSegment.roadView;

			// Subscribe to segment destruction event
			var generator = FindObjectOfType<RoadGenerator>();
			if (generator != null)
				generator.OnSegmentDestroying += OnSegmentDestroying;

			if (_spawnPoints.Length == 0)
				Debug.LogWarning($"[TrackEnemySpawner] На {gameObject.name} нет EnemySpawnPoint!", this);
			if (_entryView == null)
				Debug.LogWarning($"[TrackEnemySpawner] На {gameObject.name} нет roadView в RoadSegment!", this);
		}

		private void OnDestroy()
		{
			var generator = FindObjectOfType<RoadGenerator>();
			if (generator != null)
				generator.OnSegmentDestroying -= OnSegmentDestroying;
		}

		private void OnSegmentDestroying(RoadSegment segment)
		{
			if (segment == _roadSegment)
				DespawnEnemies();
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

			// roadView берётся из RoadSegment.roadView (уже правильный ForkEntry или линейный)
			RoadSegmentView spawnView = _entryView;
			 
			Debug.Log($"[TrackEnemySpawner] Spawn: point={point.name} spawnView={spawnView != null} enemy={enemy != null}");
			 
			if (enemy != null && spawnView != null)
			{
				if (enemy.TryGetComponent<EnemyController>(out var ctrl))
				{
					ctrl.InitializeWithSegment(spawnView);
				}
				else
				{
					Debug.LogWarning($"[TrackEnemySpawner] Спавненный префаб {prefab.name} не имеет EnemyController!", this);
				}
			}
			else
			{
				Debug.LogError($"[TrackEnemySpawner] SKIP init: enemy={enemy != null} spawnView={spawnView != null}");
			}

			// Находим тег по префабу и сохраняем его вместе с инстансом
			EnemyTag tag = GetTagForPrefab(prefab);
			_spawnedEnemies.Add(new SpawnedEnemyData { Instance = enemy, Tag = tag });
		}

		public void DespawnEnemies()
		{
			// Immediate despawn for all tracked enemies on this segment
			for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
			{
				var data = _spawnedEnemies[i];
			
				if (data.Instance == null)
				{
					_spawnedEnemies.RemoveAt(i);
					continue;
				}

				// Instant deactivation stops FixedUpdate immediately
				var ctrl = data.Instance.GetComponent<EnemyController>();
				if (ctrl != null) ctrl.Despawn(); // Despawn does SetActive(false) + Destroy
				else data.Instance.SetActive(false);
				
				_spawnedEnemies.RemoveAt(i);
			}

			foreach (var point in _spawnPoints)
				point.Disarm();

			_hasSpawned = false;
		}

		// Деспавн всех отслеживаемых (fallback)
		private void DespawnAllTracked()
		{
			for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
			{
				var data = _spawnedEnemies[i];
				if (data.Instance != null)
				{
					var ctrl = data.Instance.GetComponent<EnemyController>();
					if (ctrl != null) ctrl.Despawn();
					else data.Instance.SetActive(false);
				}
				_spawnedEnemies.RemoveAt(i);
			}
		}

		// Регистрация врага, переехавшего с другого сегмента
		public void RegisterEnemy(EnemyController enemy)
		{
			if (enemy == null) return;
			_spawnedEnemies.Add(new SpawnedEnemyData { Instance = enemy.gameObject, Tag = GetTagForPrefab(enemy.gameObject) });
		}

		// Удаление врага, уехавшего на другой сегмент
		public void UnregisterEnemy(EnemyController enemy)
		{
			if (enemy == null) return;
			for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
			{
				if (_spawnedEnemies[i].Instance == enemy.gameObject)
				{
					_spawnedEnemies.RemoveAt(i);
					break;
				}
			}
		}

		// Grid frame из RoadSegment
		private void GetGridFrame(out Vector3 origin, out Vector3 fwd, out Vector3 right)
		{
			fwd = Flat(_roadSegment.entryPoint.transform.forward);
			right = Vector3.Cross(Vector3.up, fwd);
			origin = _roadSegment.entryPoint.transform.position + fwd * (_roadSegment.cellSize * 0.5f);
		}

		private Vector2Int WorldToGridCell(Vector3 worldPos, Vector3 origin, Vector3 fwd, Vector3 right)
		{
			Vector3 d = worldPos - origin;
			return new Vector2Int(
				Mathf.RoundToInt(Vector3.Dot(d, right) / _roadSegment.cellSize),
				Mathf.RoundToInt(Vector3.Dot(d, fwd) / _roadSegment.cellSize));
		}

		private static Vector3 Flat(Vector3 v)
		{
			v.y = 0f;
			return v.normalized;
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