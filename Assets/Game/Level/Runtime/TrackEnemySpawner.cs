using System.Collections.Generic;
using Common.Runtime;
using Enemies;
using Game.Common.Runtime;
using Game.Enemies;
using Game.Enemies.Vehicle;
using Game.Enemies.Behavior;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Level.Runtime
{
	public class TrackEnemySpawner : MonoBehaviour
	{
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

		[Inject]
		public void Construct(LifetimeScope parentScope, PlayerTracker playerTracker)
		{
			_parentScope = parentScope;
			_playerTracker = playerTracker;
		}

		private void Awake()
		{
			_spawnPoints = GetComponentsInChildren<EnemySpawnPoint>();
			_roadSegment = GetComponent<RoadSegment>();
			if (_roadSegment != null)
				_entryView = _roadSegment.roadView;

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
			if (segment != _roadSegment) return;

			Debug.Log($"[TrackEnemySpawner] OnSegmentDestroying: segment={segment.name}, resident enemies={_roadSegment.ResidentEnemies.Count}");

			var enemiesToDespawn = new List<EnemyVehicleController>(_roadSegment.ResidentEnemies);
			foreach (var vehicle in enemiesToDespawn)
			{
				vehicle.OnSegmentChanged -= HandleEnemySegmentChanged;
				vehicle.Despawn();
				Debug.Log($"[TrackEnemySpawner] Despawned enemy {vehicle.name} from destroyed segment");
			}
			_roadSegment.ResidentEnemies.Clear();
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

		public void InitializeEnemy(EnemyVehicleController vehicle, IRoadPath roadPath, int lane, float startDistance, EnemyBehaviorProfileSO profile = null)
		{
			if (vehicle == null || roadPath == null) return;

			vehicle.InitializeOnRoad(roadPath, lane, startDistance, profile);

			var segment = (roadPath as RoadSegmentView)?.Segment;
			if (segment == null)
			{
				Debug.LogWarning($"[TrackEnemySpawner] InitializeEnemy: roadPath is not a RoadSegmentView or has no Segment! roadPath={roadPath?.GetType().Name}");
			}
			else
			{
				segment.RegisterEnemy(vehicle);
				Debug.Log($"[TrackEnemySpawner] InitializeEnemy: {vehicle.name} registered to segment {segment.name}");
			}

			vehicle.OnSegmentChanged += HandleEnemySegmentChanged;
		}

		private void HandleEnemySegmentChanged(EnemyVehicleController vehicle, IRoadPath oldPath, IRoadPath newPath)
		{
			var oldSeg = (oldPath as RoadSegmentView)?.Segment;
			var newSeg = (newPath as RoadSegmentView)?.Segment;

			if (oldSeg != null && oldSeg != newSeg)
			{
				oldSeg.UnregisterEnemy(vehicle);
				Debug.Log($"[TrackEnemySpawner] Enemy {vehicle.name} unregistered from segment {oldSeg.name}");
			}

			if (newSeg != null)
			{
				newSeg.RegisterEnemy(vehicle);
				Debug.Log($"[TrackEnemySpawner] Enemy {vehicle.name} registered to segment {newSeg.name}");
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

			RoadSegmentView spawnView = _entryView;
			 
			Debug.Log($"[TrackEnemySpawner] Spawn: point={point.name} spawnView={spawnView != null} enemy={enemy != null}");
			 
			if (enemy != null && spawnView != null)
			{
				if (enemy.TryGetComponent<EnemyVehicleController>(out var vehicleCtrl))
				{
					EnemyBehaviorProfileSO profile = null;
					foreach (var entry in enemyEntries)
					{
						if (entry.prefab == prefab && entry.behaviorProfile != null)
						{
							profile = entry.behaviorProfile;
							break;
						}
					}

					int startLane = 0;
					InitializeEnemy(vehicleCtrl, spawnView, startLane, 0f, profile);
				}
				else if (enemy.TryGetComponent<EnemyController>(out var legacyCtrl))
				{
					legacyCtrl.InitializeWithSegment(spawnView);
				}
				else
				{
					Debug.LogWarning($"[TrackEnemySpawner] Спавненный префаб {prefab.name} не имеет ни EnemyVehicleController, ни EnemyController!", this);
				}
			}
			else
			{
				Debug.LogError($"[TrackEnemySpawner] SKIP init: enemy={enemy != null} spawnView={spawnView != null}");
			}

			EnemyTag tag = GetTagForPrefab(prefab);
			_spawnedEnemies.Add(new SpawnedEnemyData { Instance = enemy, Tag = tag });
		}

		public void DespawnEnemies()
		{
			for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
			{
				var data = _spawnedEnemies[i];
			
				if (data.Instance == null)
				{
					_spawnedEnemies.RemoveAt(i);
					continue;
				}

				var ctrl = data.Instance.GetComponent<EnemyController>();
				var vehicleCtrl = data.Instance.GetComponent<EnemyVehicleController>();
				if (ctrl != null) ctrl.Despawn();
				else if (vehicleCtrl != null) vehicleCtrl.Despawn();
				else data.Instance.SetActive(false);
				
				_spawnedEnemies.RemoveAt(i);
			}

			foreach (var point in _spawnPoints)
				point.Disarm();

			_hasSpawned = false;
		}

		private void DespawnAllTracked()
		{
			for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
			{
				var data = _spawnedEnemies[i];
				if (data.Instance != null)
				{
					var ctrl = data.Instance.GetComponent<EnemyController>();
					var vehicleCtrl = data.Instance.GetComponent<EnemyVehicleController>();
					if (ctrl != null) ctrl.Despawn();
					else if (vehicleCtrl != null) vehicleCtrl.Despawn();
					else data.Instance.SetActive(false);
				}
				_spawnedEnemies.RemoveAt(i);
			}
		}

		public void RegisterEnemy(EnemyController enemy)
		{
			if (enemy == null) return;
			_spawnedEnemies.Add(new SpawnedEnemyData { Instance = enemy.gameObject, Tag = GetTagForPrefab(enemy.gameObject) });
		}

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