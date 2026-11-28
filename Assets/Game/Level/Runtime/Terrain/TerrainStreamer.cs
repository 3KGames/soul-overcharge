using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;
using VContainer.Unity;

namespace Game.Level.Runtime.Terrain
{
	/// <summary>
	/// Строит землю вокруг игрока чанками по клеткам дороги (1 чанк = 1 клетка RoadGenerator),
	/// подстраивает её под дорогу, ставит ямы, холмы и объекты по биомам. Старые чанки уходят в пул.
	/// </summary>
	[DefaultExecutionOrder(100)]
	[DisallowMultipleComponent]
	public class TerrainStreamer : MonoBehaviour
	{
		[Header("Связи")]
		public RoadGenerator roadGenerator;

		[Tooltip("Пусто — берётся player у RoadGenerator")]
		public Transform player;

		[Header("Биомы")]
		[Tooltip("Настройки земли для каждого биома дороги (связь по biomeTag)")]
		public List<BiomeTerrainSO> biomes = new List<BiomeTerrainSO>();

		[Tooltip("Для биома, которого нет в списке. Пусто — первый из списка")]
		public BiomeTerrainSO fallbackBiome;

		[Tooltip("Материал, если у биома он не задан")]
		public Material defaultMaterial;

		[Header("Сетка рельефа")]
		[Tooltip("Квадратов на сторону чанка. 50 при клетке 50 м — вершина каждый метр")]
		[Range(8, 128)] public int resolution = 50;

		[Tooltip("Влияет на всё случайное в рельефе. 0 — новый при каждом запуске")]
		public int seed = 0;

		[Header("Где строить подробную землю")]
		[Tooltip("Вкл — подробный рельеф с ямами и объектами только у дороги и под машиной. Выкл — квадратом loadRadius вокруг игрока")]
		public bool detailOnlyNearRoad = true;

		[Tooltip("Сколько клеток вокруг каждой клетки дороги получают подробный рельеф. 0 — только клетки самой дороги")]
		[Range(0, 3)] public int roadPadding = 1;

		[Tooltip("Сколько клеток вокруг машины всегда подробные и с коллайдером — чтобы съехав с дороги не провалиться. 1 → 3×3")]
		[Range(1, 3)] public int playerPadding = 1;

		[Tooltip("Ширина плавного перехода от подробного рельефа к ровной земле на краю, м")]
		[Min(0f)] public float edgeFadeWidth = 15f;

		[Header("Дальняя земля")]
		[Tooltip("Ровная дешёвая земля без объектов вокруг подробной, чтобы на горизонте не было пустоты")]
		public bool farGround = true;

		[Tooltip("Радиус дальней земли вокруг игрока в клетках. 4 → квадрат 9×9")]
		[Range(1, 8)] public int loadRadius = 4;

		[Tooltip("Квадратов на сторону у дальнего чанка. 5 — вершина каждые 10 м")]
		[Range(1, 16)] public int farResolution = 5;

		[Header("Коллайдеры")]
		[Tooltip("Сколько чанков вокруг игрока получают коллайдер. 1 → 3×3. Не больше Player Padding")]
		[Range(0, 3)] public int colliderRadius = 1;

		[Tooltip("Если машина оказалась под землёй — поднять её на поверхность и написать в Console, что случилось")]
		public bool preventFallThrough = true;

		[Tooltip("На сколько метров машина должна уйти под поверхность, чтобы сработала защита")]
		[Min(0.2f)] public float fallThroughDepth = 0.75f;

		[Header("Расчёт")]
		[Tooltip("Считать чанки в фоновых потоках (на WebGL всегда в главном)")]
		public bool useBackgroundThreads = true;

		[Tooltip("Сколько чанков считать одновременно в фоне")]
		[Range(1, 8)] public int maxConcurrentJobs = 3;

		[Tooltip("Сколько готовых чанков применять за кадр (меш, коллайдер, объекты)")]
		[Range(1, 8)] public int maxAppliesPerFrame = 2;

		[Tooltip("Без фоновых потоков: сколько чанков считать за кадр")]
		[Range(1, 4)] public int maxSyncBuildsPerFrame = 1;

		[Header("Форма дороги")]
		public RoadShapeSource roadShapeSource = RoadShapeSource.Auto;

		[Tooltip("Ширина полосы, м. В префабах проекта дорога 9 м на 3 полосы — 3 м")]
		[Min(0.5f)] public float laneWidth = 3f;

		[Tooltip("Обочина с каждой стороны, м — добавляется к ширине полос")]
		[Min(0f)] public float roadShoulder = 0.5f;

		[Tooltip("Если сплайн идёт не по центру дороги — сдвиг вправо (+) или влево (-), м")]
		public float splineLateralOffset = 0f;

		[Tooltip("Поправка высоты поверхности дороги относительно сплайна, м")]
		public float roadSurfaceOffset = 0f;

		[Tooltip("Точек на один сплайн")]
		[Range(8, 128)] public int splineSamples = 40;

		[Header("Переход от дороги к рельефу")]
		[Tooltip("Ровная полоса сразу за краем дороги, м")]
		[Min(0f)] public float roadClearance = 2f;

		[Tooltip("Ширина плавного перехода от ровной полосы к рельефу, м")]
		[Min(0.5f)] public float roadFalloff = 10f;

		[Tooltip("На сколько земля под дорогой ниже покрытия, м")]
		[Min(0f)] public float sinkUnderRoad = 0.3f;

		[Tooltip("На сколько обочина ниже покрытия, м")]
		[Min(0f)] public float shoulderDrop = 0.05f;

		[Header("Ямы и холмы")]
		[Tooltip("Шаг сетки, в ячейках которой появляются ямы и холмы, м")]
		[Min(4f)] public float stampCellSize = 20f;

		[Tooltip("0 — яма или холм целиком вне зоны дороги, 1 — центр может стоять у самого края дороги")]
		[Range(0f, 1f)] public float stampRoadOverlap = 0.5f;

		[Header("Объекты")]
		public bool spawnProps = true;

		[Tooltip("Шаг сетки объектов, м. В каждой ячейке не больше одного объекта")]
		[Min(1f)] public float propCellSize = 7f;

		[Tooltip("Общий отступ объектов от края дороги, м")]
		[Min(0f)] public float propRoadClearance = 1.5f;

		[Header("Отображение")]
		[Tooltip("Слой чанков. В проекте это 16 Offroad — он уже стоит в offroadMask у LevelLifetimeScope")]
		public int terrainLayer = 16;

		[Tooltip("Размер текстуры на земле, м")]
		[Min(0.1f)] public float uvScale = 10f;

		public bool castShadows = true;

		[Header("Отладка")]
		public bool drawGizmos = true;
		public bool logStats;

		// -------------------------------------------------------------------------------------------

		sealed class SpawnedProp
		{
			public GameObject instance;
			public GameObject prefab;
		}

		sealed class Chunk
		{
			public Vector2Int coord;
			public GameObject go;
			public MeshFilter filter;
			public MeshRenderer renderer;
			public MeshCollider collider;
			public Mesh mesh;
			public bool loaded;
			public bool built;
			public bool dirty;
			public bool hasCollider;
			public bool detailed;
			public ChunkJob running;
			public Task task;
			public ChunkJob data;
			public readonly List<SpawnedProp> props = new List<SpawnedProp>();
		}

		struct PendingSegment
		{
			public RoadSegmentInfo info;
			public int frame;
		}

		const int InferRadius = 6;

		readonly Dictionary<Vector2Int, Chunk> _chunks = new Dictionary<Vector2Int, Chunk>();
		readonly Stack<Chunk> _chunkPool = new Stack<Chunk>();
		readonly List<Chunk> _work = new List<Chunk>();
		readonly List<RoadShape> _shapes = new List<RoadShape>();
		readonly Dictionary<RoadSegment, RoadShape> _shapeBySegment = new Dictionary<RoadSegment, RoadShape>();
		readonly List<PendingSegment> _pending = new List<PendingSegment>();
		readonly List<RoadSegmentInfo> _existing = new List<RoadSegmentInfo>();
		readonly List<RoadLine> _lineBuffer = new List<RoadLine>();
		readonly TerrainCellMap _cellMap = new TerrainCellMap();
		readonly Dictionary<GameObject, Stack<GameObject>> _propPool = new Dictionary<GameObject, Stack<GameObject>>();
		readonly Dictionary<GameObject, Vector3> _prefabScale = new Dictionary<GameObject, Vector3>();
		readonly Dictionary<int, int[]> _triangles = new Dictionary<int, int[]>();
		HashSet<Vector2Int> _detailed = new HashSet<Vector2Int>();
		readonly Dictionary<Vector2Int, bool> _desired = new Dictionary<Vector2Int, bool>();
		bool _layoutDirty;

		BiomeTerrainData[] _biomeByTag;
		BiomeTerrainData _fallback;
		BiomeTerrainSO _defaultBiome;
		TerrainBuildSettings _settings;
		Transform _chunkRoot, _propRoot;
		Vector2Int _center;
		bool _ready;
		bool _settingsDirty;
		bool _threads;
		int _runningCount;
		BiomeTag _lastBiome = BiomeTag.None;
		float _lastRoadHeight;
		bool _hasRoadHeight;
		float _nextStatsTime;
		bool _warnedNoMaterial;
		IObjectResolver _resolver;

		public int LoadedChunks => _chunks.Count;
		public int RoadShapes => _shapes.Count;
		public bool IsReady => _ready;

		[Inject]
		public void Construct(IObjectResolver resolver)
		{
			_resolver = resolver;
		}

		// ------------------------------------------------------------------------------- жизненный цикл

		void Awake()
		{
			_chunkRoot = new GameObject("Terrain Chunks").transform;
			_chunkRoot.SetParent(transform, false);
			_propRoot = new GameObject("Terrain Props").transform;
			_propRoot.SetParent(transform, false);

#if UNITY_WEBGL && !UNITY_EDITOR
			_threads = false;
#else
			_threads = useBackgroundThreads;
#endif
			if (seed == 0) seed = Random.Range(1, int.MaxValue);
			BakeSettings();
		}

		void OnEnable()
		{
			if (roadGenerator == null) return;
			roadGenerator.SegmentPlaced += OnSegmentPlaced;
			roadGenerator.SegmentRemoved += OnSegmentRemoved;
			roadGenerator.GeneratorReset += OnGeneratorReset;
#if UNITY_EDITOR
			TerrainAssetEvents.Changed += MarkSettingsDirty;
#endif
		}

		void OnDisable()
		{
			if (roadGenerator != null)
			{
				roadGenerator.SegmentPlaced -= OnSegmentPlaced;
				roadGenerator.SegmentRemoved -= OnSegmentRemoved;
				roadGenerator.GeneratorReset -= OnGeneratorReset;
			}
#if UNITY_EDITOR
			TerrainAssetEvents.Changed -= MarkSettingsDirty;
#endif
		}

		IEnumerator Start()
		{
			if (roadGenerator == null)
			{
				Debug.LogError("[TerrainStreamer] Не назначен roadGenerator — рельеф не строится.", this);
				enabled = false;
				yield break;
			}

			while (!roadGenerator.IsGridReady) yield return null;

			// Сплайны Dreamteck пересчитываются после перемещения сегмента в следующем кадре
			yield return null;

			roadGenerator.GetPlacedSegments(_existing);
			foreach (var info in _existing)
				if (!_shapeBySegment.ContainsKey(info.segment) && !IsPending(info.segment))
					_pending.Add(new PendingSegment { info = info, frame = -1 });
			ProcessPending();

			var target = PlayerTransform();
			_center = target != null ? roadGenerator.WorldToCell(target.position) : Vector2Int.zero;
			RefreshLoadedSet();

			// Чанки под машиной — сразу и синхронно, чтобы не было пустого кадра
			foreach (var c in _chunks.Values)
				if (Chebyshev(c.coord, _center) <= Mathf.Max(1, colliderRadius))
					BuildNow(c);

			// OnValidate при входе в Play Mode мог выставить флаг — всё и так только что построено
			_settingsDirty = false;
			_ready = true;
		}

		void Update()
		{
			if (!_ready) return;

			if (_settingsDirty)
			{
				_settingsDirty = false;
				BakeSettings();
				MarkAllDirty();
			}

			ProcessPending();

			var target = PlayerTransform();
			if (target != null)
			{
				var cell = roadGenerator.WorldToCell(target.position);
				if (cell != _center)
				{
					_center = cell;
					_layoutDirty = true;
				}
			}

			if (_layoutDirty)
			{
				_layoutDirty = false;
				RefreshLoadedSet();
				RefreshColliders();
			}

			ApplyFinishedJobs();
			EnsureGroundUnderPlayer();
			ScheduleJobs();

			if (Time.frameCount % 60 == 0) PruneMemory();

			if (logStats && Time.unscaledTime >= _nextStatsTime)
			{
				_nextStatsTime = Time.unscaledTime + 5f;
				Debug.Log($"[TerrainStreamer] чанков {_chunks.Count}, в работе {_runningCount}, форм дороги {_shapes.Count}, клеток {_cellMap.Count}", this);
			}
		}

		/// <summary>Куски с коллайдером вокруг машины должны быть построены всегда — если нет, строим сразу, не дожидаясь фона.</summary>
		void EnsureGroundUnderPlayer()
		{
			int r = Mathf.Min(colliderRadius, playerPadding);
			for (int dz = -r; dz <= r; dz++)
			for (int dx = -r; dx <= r; dx++)
			{
				if (!_chunks.TryGetValue(new Vector2Int(_center.x + dx, _center.y + dz), out var c)) continue;
				if (!c.built)
				{
					if (c.running == null) BuildNow(c);
					continue;
				}

				// Коллайдер под машиной должен быть на месте и с актуальным мешем. Если Unity его потеряла — вернуть
				if (!c.hasCollider || !c.collider.enabled || c.collider.sharedMesh != c.mesh)
				{
					if (_healedColliders < 5)
						Debug.LogWarning($"[TerrainStreamer] Коллайдер куска {c.coord} был потерян (есть={c.hasCollider}, включён={c.collider.enabled}, " +
						                 $"меш={(c.collider.sharedMesh == null ? "нет" : c.collider.sharedMesh == c.mesh ? "свой" : "чужой")}). Восстановлен.", this);
					_healedColliders++;
					c.hasCollider = false;
					UpdateCollider(c, true);
				}
			}
		}

		int _healedColliders;

		Rigidbody _playerBody;
		Collider[] _playerColliders;
		Transform _playerBodyOwner;

		void FixedUpdate()
		{
			if (!_ready || !preventFallThrough) return;
			var target = PlayerTransform();
			if (target == null) return;

			if (_playerBodyOwner != target)
			{
				_playerBodyOwner = target;
				_playerBody = target.GetComponentInParent<Rigidbody>();
				if (_playerBody == null) _playerBody = target.GetComponentInChildren<Rigidbody>();
				_playerColliders = _playerBody != null ? _playerBody.GetComponentsInChildren<Collider>() : null;
			}
			if (_playerBody == null || _playerColliders == null || _playerColliders.Length == 0) return;

			// Нижняя точка машины по её коллайдерам
			float bottom = float.MaxValue;
			Vector3 center = _playerBody.worldCenterOfMass;
			foreach (var col in _playerColliders)
				if (col != null && col.enabled && !col.isTrigger)
					bottom = Mathf.Min(bottom, col.bounds.min.y);
			if (bottom == float.MaxValue) return;

			if (!TryGetHeight(center, out float ground)) return;

			// Под землёй должна оказаться вся машина, а не только колесо в яме
			float top = bottom;
			foreach (var col in _playerColliders)
				if (col != null && col.enabled && !col.isTrigger)
					top = Mathf.Max(top, col.bounds.max.y);
			if (top > ground - fallThroughDepth) return;

			// Не трогать машину, если под ней дорога: дорога выше земли и держит сама
			var cell = roadGenerator.WorldToCell(center);
			_chunks.TryGetValue(cell, out var chunk);
			string why = chunk == null ? "куска земли нет"
				: !chunk.built ? "кусок ещё не построен"
				: !chunk.hasCollider || !chunk.collider.enabled ? "у куска нет коллайдера"
				: chunk.collider.sharedMesh == null ? "у коллайдера нет меша"
				: "коллайдер есть — машину протолкнуло сквозь него";
			Debug.LogWarning($"[TerrainStreamer] Машина под землёй в клетке {cell} (низ {bottom:0.00}, земля {ground:0.00}, скорость {_playerBody.linearVelocity.magnitude:0.0} м/с, " +
			                 $"подробный={chunk?.detailed}, грязный={chunk?.dirty}). Причина: {why}. Подняли на поверхность.", this);

			float lift = ground + 0.3f - bottom;
			_playerBody.position += Vector3.up * lift;
			var v = _playerBody.linearVelocity;
			if (v.y < 0f) v.y = 0f;
			_playerBody.linearVelocity = v;
		}

		void OnDestroy()
		{
			foreach (var c in _chunks.Values)
				if (c.mesh != null) Destroy(c.mesh);
			foreach (var c in _chunkPool)
				if (c.mesh != null) Destroy(c.mesh);
		}

		void OnValidate()
		{
			terrainLayer = Mathf.Clamp(terrainLayer, 0, 31);
			if (detailOnlyNearRoad && colliderRadius > playerPadding) colliderRadius = playerPadding;
			if (Application.isPlaying) _settingsDirty = true;
		}

		void MarkSettingsDirty() => _settingsDirty = true;

		[ContextMenu("Перестроить весь рельеф")]
		public void RebuildAll()
		{
			BakeSettings();
			MarkAllDirty();
		}

		Transform PlayerTransform() => player != null ? player : roadGenerator != null ? roadGenerator.player : null;

		// ------------------------------------------------------------------------------- настройки

		void BakeSettings()
		{
			float cs = roadGenerator != null && roadGenerator.CellSize > 0f ? roadGenerator.CellSize : 50f;
			float maxStampRadius = cs * 0.45f;

			int tagCount = 0;
			foreach (BiomeTag t in System.Enum.GetValues(typeof(BiomeTag)))
				tagCount = Mathf.Max(tagCount, (int)t + 1);
			_biomeByTag = new BiomeTerrainData[tagCount];

			float realMaxStamp = 0f;
			float maxPropReach = 0f;
			foreach (var so in biomes)
			{
				if (so == null) continue;
				int i = (int)so.biomeTag;
				if (i < 0 || i >= tagCount) continue;
				if (_biomeByTag[i] != null)
					Debug.LogWarning($"[TerrainStreamer] Два BiomeTerrain с тегом {so.biomeTag} — используется первый ({_biomeByTag[i].name})", this);
				else
					_biomeByTag[i] = so.Bake(maxStampRadius);

				var data = _biomeByTag[i];
				foreach (var st in data.stamps) realMaxStamp = Mathf.Max(realMaxStamp, st.radiusMax);
				foreach (var p in data.props)
					maxPropReach = Mathf.Max(maxPropReach,
						Mathf.Max(propRoadClearance + p.minRoadDistance + p.radius * p.scaleMax, p.maxRoadDistance) + 1f);
			}

			var fb = fallbackBiome != null ? fallbackBiome : biomes.Find(b => b != null);
			if (fb == null)
			{
				if (_defaultBiome == null)
				{
					_defaultBiome = ScriptableObject.CreateInstance<BiomeTerrainSO>();
					_defaultBiome.name = "Default (нет BiomeTerrain)";
					Debug.LogWarning("[TerrainStreamer] Список biomes пуст — земля будет по настройкам по умолчанию.", this);
				}
				fb = _defaultBiome;
			}
			int fi = (int)fb.biomeTag;
			_fallback = fi >= 0 && fi < tagCount && _biomeByTag[fi] != null && fallbackBiome == null ? _biomeByTag[fi] : fb.Bake(maxStampRadius);
			foreach (var st in _fallback.stamps) realMaxStamp = Mathf.Max(realMaxStamp, st.radiusMax);

			float step = cs / Mathf.Max(8, resolution);
			float roadReach = roadClearance + roadFalloff;
			if (roadReach > cs * 0.8f)
				Debug.LogWarning($"[TerrainStreamer] roadClearance + roadFalloff = {roadReach} м — больше 80% клетки ({cs} м). " +
				                 "Между чанками могут появиться ступеньки.", this);

			_settings = new TerrainBuildSettings
			{
				seed = seed,
				cellSize = cs,
				resolution = Mathf.Max(8, resolution),
				roadClearance = roadClearance,
				roadFalloff = roadFalloff,
				sinkUnderRoad = sinkUnderRoad,
				shoulderDrop = shoulderDrop,
				stampCellSize = stampCellSize,
				stampRoadOverlap = stampRoadOverlap,
				maxStampRadius = Mathf.Max(realMaxStamp, 0.5f),
				props = spawnProps,
				propCellSize = propCellSize,
				propRoadClearance = propRoadClearance,
				uvScale = uvScale,
				edgeFade = edgeFadeWidth,
				skirtDepth = 2f,
				influence = Mathf.Max(roadReach + step, 2f * realMaxStamp + step, maxPropReach),
			};

			if (_settings.influence > cs)
				Debug.LogWarning($"[TerrainStreamer] Зона влияния дороги {_settings.influence:0} м больше клетки {cs} м " +
				                 "(большие maxRoadDistance у объектов?). Возможны ступеньки между чанками.", this);
		}

		// ------------------------------------------------------------------------------- дорога

		void OnSegmentPlaced(RoadSegmentInfo info)
		{
			if (info.segment == null) return;
			_pending.Add(new PendingSegment { info = info, frame = Time.frameCount });
		}

		void OnSegmentRemoved(RoadSegment segment)
		{
			for (int i = _pending.Count - 1; i >= 0; i--)
				if (_pending[i].info.segment == segment) _pending.RemoveAt(i);

			// Дорогу убрали — убираем и её форму: на месте отрезанной ветки развилки снова будет песок,
			// а не ровная полоса без асфальта
			if (segment != null && _shapeBySegment.TryGetValue(segment, out var shape))
			{
				_shapeBySegment.Remove(segment);
				_shapes.Remove(shape);
				MarkOverlapping(shape);
				_layoutDirty = true;
			}
		}

		void OnGeneratorReset()
		{
			_pending.Clear();
			_shapes.Clear();
			_shapeBySegment.Clear();
			_cellMap.Clear();
			_hasRoadHeight = false;
			MarkAllDirty();
		}

		bool IsPending(RoadSegment seg)
		{
			foreach (var p in _pending)
				if (p.info.segment == seg) return true;
			return false;
		}

		void ProcessPending()
		{
			int frame = Time.frameCount;
			for (int i = 0; i < _pending.Count; i++)
			{
				var p = _pending[i];
				if (p.frame >= frame) continue; // сплайн ещё не обновился после перемещения

				_pending.RemoveAt(i--);
				if (p.info.segment == null || _shapeBySegment.ContainsKey(p.info.segment)) continue;
				AddShape(p.info);
				_layoutDirty = true;
			}
		}

		void AddShape(in RoadSegmentInfo info)
		{
			var shape = RoadShape.Build(info, roadGenerator, new RoadShape.BuildSettings
			{
				source = roadShapeSource,
				laneWidth = laneWidth,
				shoulder = roadShoulder,
				lateralOffset = splineLateralOffset,
				surfaceOffset = roadSurfaceOffset,
				splineSamples = splineSamples,
			});

			_shapes.Add(shape);
			_shapeBySegment[info.segment] = shape;
			_lastBiome = info.biome;

			float avg = shape.AverageHeight();
			if (shape.lines.Length > 0)
			{
				_lastRoadHeight = avg;
				_hasRoadHeight = true;
			}

			if (info.cells != null)
				foreach (var cell in info.cells)
				{
					float h = shape.TryGetHeightInCell(cell, _settings.cellSize, out float inCell) ? inCell : avg;
					if (_cellMap.SetRoadCell(cell, info.biome, h))
						MarkAround(cell, 1);
				}

			MarkOverlapping(shape);
		}

		// ------------------------------------------------------------------------------- чанки

		/// <summary>Клетки с подробной землёй: клетки дороги с запасом roadPadding и клетки вокруг машины.</summary>
		HashSet<Vector2Int> ComputeDetailedSet()
		{
			var set = new HashSet<Vector2Int>();
			if (!detailOnlyNearRoad)
			{
				AddSquare(set, _center, loadRadius);
				return set;
			}

			int limit = loadRadius + 2;
			foreach (var shape in _shapes)
			{
				if (shape.cells == null) continue;
				foreach (var cell in shape.cells)
				{
					// след удалённого сегмента держим, только пока он рядом — дальше он не нужен
					if (shape.IsGhost && Chebyshev(cell, _center) > loadRadius) continue;
					if (Chebyshev(cell, _center) > limit) continue;
					AddSquare(set, cell, roadPadding);
				}
			}
			AddSquare(set, _center, playerPadding);
			return set;
		}

		static void AddSquare(HashSet<Vector2Int> set, Vector2Int c, int r)
		{
			for (int dz = -r; dz <= r; dz++)
			for (int dx = -r; dx <= r; dx++)
				set.Add(new Vector2Int(c.x + dx, c.y + dz));
		}

		void RefreshLoadedSet()
		{
			var detailed = ComputeDetailedSet();

			_desired.Clear();
			foreach (var cell in detailed) _desired[cell] = true;
			if (farGround)
				for (int dz = -loadRadius; dz <= loadRadius; dz++)
				for (int dx = -loadRadius; dx <= loadRadius; dx++)
				{
					var cell = new Vector2Int(_center.x + dx, _center.y + dz);
					if (!_desired.ContainsKey(cell)) _desired[cell] = false;
				}

			// Сменился статус клетки — пересчитать её подробных соседей: у них меняется затухание к краю
			foreach (var cell in detailed)
				if (!_detailed.Contains(cell)) MarkAround(cell, 1);
			foreach (var cell in _detailed)
				if (!detailed.Contains(cell)) MarkAround(cell, 1);
			_detailed = detailed;

			_work.Clear();
			foreach (var c in _chunks.Values)
				if (!_desired.ContainsKey(c.coord)) _work.Add(c);
			foreach (var c in _work) Unload(c);

			foreach (var kv in _desired)
			{
				if (_chunks.TryGetValue(kv.Key, out var c))
				{
					if (c.detailed != kv.Value)
					{
						c.detailed = kv.Value;
						c.dirty = true;
					}
				}
				else Load(kv.Key, kv.Value);
			}
		}

		void Load(Vector2Int coord, bool detailed)
		{
			var c = _chunkPool.Count > 0 ? _chunkPool.Pop() : CreateChunk();
			c.coord = coord;
			c.loaded = true;
			c.built = false;
			c.dirty = true;
			c.hasCollider = false;
			c.detailed = detailed;
			c.data = null;
			c.go.name = $"Chunk {coord.x},{coord.y}";
			c.go.layer = terrainLayer;
			Vector3 corner = new Vector3((coord.x - 0.5f) * _settings.cellSize, 0f, coord.y * _settings.cellSize);
			c.go.transform.SetPositionAndRotation(roadGenerator.GridToWorld(corner), roadGenerator.GridRotation);
			_chunks.Add(coord, c);
		}

		Chunk CreateChunk()
		{
			var go = new GameObject("Chunk");
			go.transform.SetParent(_chunkRoot, false);
			go.SetActive(false);
			var c = new Chunk
			{
				go = go,
				filter = go.AddComponent<MeshFilter>(),
				renderer = go.AddComponent<MeshRenderer>(),
				collider = go.AddComponent<MeshCollider>(),
				mesh = new Mesh { name = "TerrainChunk" },
			};
			c.mesh.MarkDynamic();
			c.filter.sharedMesh = c.mesh;
			c.collider.enabled = false;
			return c;
		}

		void Unload(Chunk c)
		{
			_chunks.Remove(c.coord);
			ReleaseProps(c);
			c.loaded = false;
			c.built = false;
			c.dirty = false;
			c.data = null;
			c.collider.sharedMesh = null;
			c.collider.enabled = false;
			c.hasCollider = false;
			c.go.SetActive(false);
			if (c.running == null) _chunkPool.Push(c);
			else _orphans.Add(c);
		}

		void MarkAround(Vector2Int cell, int radius)
		{
			for (int dz = -radius; dz <= radius; dz++)
			for (int dx = -radius; dx <= radius; dx++)
				if (_chunks.TryGetValue(new Vector2Int(cell.x + dx, cell.y + dz), out var c))
					c.dirty = true;
		}

		void MarkOverlapping(RoadShape shape)
		{
			float cs = _settings.cellSize;
			foreach (var c in _chunks.Values)
			{
				float x0 = (c.coord.x - 0.5f) * cs, z0 = c.coord.y * cs;
				if (shape.Overlaps(x0, x0 + cs, z0, z0 + cs, _settings.influence))
					c.dirty = true;
			}
		}

		void MarkAllDirty()
		{
			foreach (var c in _chunks.Values) c.dirty = true;
		}

		void RefreshColliders()
		{
			foreach (var c in _chunks.Values)
				if (c.built) UpdateCollider(c, false);
		}

		void UpdateCollider(Chunk c, bool meshChanged)
		{
			bool want = Chebyshev(c.coord, _center) <= colliderRadius;
			if (want && (!c.hasCollider || meshChanged))
			{
				c.collider.sharedMesh = null;
				c.collider.sharedMesh = c.mesh;
				c.collider.enabled = true;
				c.hasCollider = true;
			}
			else if (!want && c.hasCollider)
			{
				c.collider.enabled = false;
				c.collider.sharedMesh = null;
				c.hasCollider = false;
			}
		}

		// ------------------------------------------------------------------------------- расчёт

		void ScheduleJobs()
		{
			int slots = _threads ? maxConcurrentJobs - _runningCount : maxSyncBuildsPerFrame;
			if (slots <= 0) return;

			_work.Clear();
			foreach (var c in _chunks.Values)
				if (c.dirty && c.running == null) _work.Add(c);
			if (_work.Count == 0) return;

			_work.Sort((a, b) => Priority(a).CompareTo(Priority(b)));

			for (int i = 0; i < _work.Count && slots > 0; i++, slots--)
			{
				var c = _work[i];
				if (_threads) StartJob(c);
				else BuildNow(c);
			}
		}

		int Priority(Chunk c)
		{
			// Сначала пустые места, потом обновления; ближние раньше дальних
			int d = Chebyshev(c.coord, _center);
			return (c.built ? 100 : 0) + (c.detailed ? 0 : 20) + d;
		}

		void StartJob(Chunk c)
		{
			var job = CreateJob(c);
			c.dirty = false;
			c.running = job;
			c.task = Task.Run(() => TerrainChunkCompute.Run(job));
			_runningCount++;
		}

		void BuildNow(Chunk c)
		{
			var job = CreateJob(c);
			c.dirty = false;
			TerrainChunkCompute.Run(job);
			Apply(c, job);
		}

		void ApplyFinishedJobs()
		{
			if (_runningCount == 0) return;

			// Чанки, выгруженные во время расчёта: результат выбрасываем, чанк — в пул
			for (int i = _orphans.Count - 1; i >= 0; i--)
			{
				var c = _orphans[i];
				if (!c.task.IsCompleted) continue;
				FinishTask(c);
				_orphans.RemoveAt(i);
				_chunkPool.Push(c);
			}

			_work.Clear();
			foreach (var c in _chunks.Values)
				if (c.running != null && c.task.IsCompleted) _work.Add(c);
			if (_work.Count == 0) return;

			_work.Sort((a, b) => Priority(a).CompareTo(Priority(b)));

			// Остальные готовые подождут следующего кадра — так кадр не проседает
			for (int i = 0; i < _work.Count && i < maxAppliesPerFrame; i++)
			{
				var c = _work[i];
				var job = c.running;
				FinishTask(c);
				Apply(c, job);
			}
		}

		void FinishTask(Chunk c)
		{
			c.running = null;
			c.task = null;
			_runningCount--;
		}

		readonly List<Chunk> _orphans = new List<Chunk>();

		ChunkJob CreateJob(Chunk chunk)
		{
			var coord = chunk.coord;
			var job = new ChunkJob
			{
				coord = coord,
				s = _settings,
				biomeByTag = _biomeByTag,
				fallback = _fallback,
				flat = !chunk.detailed,
			};
			if (job.flat) job.s.resolution = Mathf.Max(1, farResolution);
			for (int dz = -1; dz <= 1; dz++)
			for (int dx = -1; dx <= 1; dx++)
				job.detailed[(dx + 1) + (dz + 1) * 3] = _detailed.Contains(new Vector2Int(coord.x + dx, coord.y + dz));

			BiomeTag fbTag = _lastBiome != BiomeTag.None ? _lastBiome : roadGenerator.CurrentBiomeTag;
			float fbHeight = _hasRoadHeight ? _lastRoadHeight : 0f;
			for (int dz = -1; dz <= 1; dz++)
			for (int dx = -1; dx <= 1; dx++)
				job.cells[(dx + 1) + (dz + 1) * 3] =
					_cellMap.GetOrInfer(new Vector2Int(coord.x + dx, coord.y + dz), fbTag, fbHeight, InferRadius);

			float cs = _settings.cellSize;
			float x0 = (coord.x - 0.5f) * cs, x1 = x0 + cs;
			float z0 = coord.y * cs, z1 = z0 + cs;
			float m = _settings.influence;

			_lineBuffer.Clear();
			foreach (var shape in _shapes)
			{
				if (!shape.Overlaps(x0, x1, z0, z1, m)) continue;
				foreach (var l in shape.lines)
				{
					float r = m + l.halfWidth;
					if (Mathf.Max(l.a.x, l.b.x) + r < x0 || Mathf.Min(l.a.x, l.b.x) - r > x1) continue;
					if (Mathf.Max(l.a.z, l.b.z) + r < z0 || Mathf.Min(l.a.z, l.b.z) - r > z1) continue;
					_lineBuffer.Add(l);
				}
			}
			job.lines = _lineBuffer.ToArray();
			job.lineCount = job.lines.Length;
			return job;
		}

		void Apply(Chunk c, ChunkJob job)
		{
			if (job.error != null)
			{
				Debug.LogException(job.error, this);
				return;
			}

			var mesh = c.mesh;
			int vCount = job.vertices.Length;
			mesh.Clear();
			mesh.indexFormat = vCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
			mesh.SetVertices(job.vertices);
			mesh.SetNormals(job.normals);
			mesh.SetColors(job.colors);
			mesh.SetUVs(0, job.uvs);
			mesh.SetTriangles(Triangles(job.Resolution), 0, false);
			float cs = job.s.cellSize;
			float minY = job.minY, maxY = Mathf.Max(job.maxY, job.minY + 0.01f);
			mesh.bounds = new Bounds(new Vector3(cs * 0.5f, (minY + maxY) * 0.5f, cs * 0.5f), new Vector3(cs, maxY - minY, cs));

			var mat = job.dominant != null && job.dominant.material != null ? job.dominant.material : defaultMaterial;
			if (mat == null && !_warnedNoMaterial)
			{
				_warnedNoMaterial = true;
				Debug.LogWarning("[TerrainStreamer] Нет материала ни у биома, ни в defaultMaterial — земля будет розовой.", this);
			}
			c.renderer.sharedMaterial = mat;
			c.renderer.shadowCastingMode = castShadows && !job.flat ? ShadowCastingMode.On : ShadowCastingMode.Off;
			c.go.layer = terrainLayer;

			c.data = job;
			c.built = true;
			c.go.SetActive(true);
			UpdateCollider(c, true);

			ReleaseProps(c);
			if (spawnProps) SpawnProps(c, job);
		}

		int[] Triangles(int res)
		{
			if (_triangles.TryGetValue(res, out var tris)) return tris;

			int row = res + 1;
			int gridCount = row * row;
			// сетка + юбка: по 4 краям res отрезков, каждый — 2 треугольника с обеих сторон
			tris = new int[res * res * 6 + 4 * res * 12];
			int t = 0;
			for (int z = 0; z < res; z++)
			for (int x = 0; x < res; x++)
			{
				int i = x + z * row;
				// диагональ чередуется, чтобы склоны не выглядели полосатыми
				if (((x + z) & 1) == 0)
				{
					tris[t++] = i; tris[t++] = i + row; tris[t++] = i + row + 1;
					tris[t++] = i; tris[t++] = i + row + 1; tris[t++] = i + 1;
				}
				else
				{
					tris[t++] = i; tris[t++] = i + row; tris[t++] = i + 1;
					tris[t++] = i + 1; tris[t++] = i + row; tris[t++] = i + row + 1;
				}
			}
			for (int side = 0; side < 4; side++)
			for (int i = 0; i < res; i++)
			{
				int a = side == 0 ? i : side == 1 ? i + res * row : side == 2 ? i * row : res + i * row;
				int b = side == 0 ? i + 1 : side == 1 ? i + 1 + res * row : side == 2 ? (i + 1) * row : res + (i + 1) * row;
				int sa = gridCount + side * row + i;
				int sb = sa + 1;
				// двусторонняя полоска, чтобы не думать о направлении обхода
				tris[t++] = a; tris[t++] = sa; tris[t++] = b;
				tris[t++] = b; tris[t++] = sa; tris[t++] = sb;
				tris[t++] = a; tris[t++] = b; tris[t++] = sa;
				tris[t++] = b; tris[t++] = sb; tris[t++] = sa;
			}
			_triangles[res] = tris;
			return tris;
		}

		// ------------------------------------------------------------------------------- объекты

		void SpawnProps(Chunk c, ChunkJob job)
		{
			Quaternion gridRot = roadGenerator.GridRotation;
			foreach (var p in job.props)
			{
				if (p.rejected) continue;
				var data = TerrainChunkCompute.PropDataFor(job, p);
				if (data == null || data.prefab == null) continue;

				var go = GetProp(data.prefab);
				Quaternion yaw = Quaternion.Euler(0f, p.yaw, 0f);
				Quaternion tilt = data.alignToNormal ? Quaternion.FromToRotation(Vector3.up, p.normal) : Quaternion.identity;
				go.transform.SetPositionAndRotation(roadGenerator.GridToWorld(new Vector3(p.x, p.y, p.z)), gridRot * tilt * yaw);
				go.transform.localScale = _prefabScale[data.prefab] * p.scale;
				var billboard = go.GetComponent<TerrainBillboard>();
				if (data.faceCamera && billboard == null) billboard = go.AddComponent<TerrainBillboard>();
				if (billboard != null) billboard.enabled = data.faceCamera;
				go.SetActive(true);
				c.props.Add(new SpawnedProp { instance = go, prefab = data.prefab });
			}
		}

		GameObject GetProp(GameObject prefab)
		{
			if (!_prefabScale.ContainsKey(prefab)) _prefabScale[prefab] = prefab.transform.localScale;

			if (_propPool.TryGetValue(prefab, out var stack))
				while (stack.Count > 0)
				{
					var pooled = stack.Pop();
					if (pooled != null) return pooled;
				}

			var go = _resolver != null ? _resolver.Instantiate(prefab) : Instantiate(prefab);
			go.transform.SetParent(_propRoot, true);
			return go;
		}

		void ReleaseProps(Chunk c)
		{
			foreach (var p in c.props)
			{
				if (p.instance == null) continue;
				p.instance.SetActive(false);
				if (!_propPool.TryGetValue(p.prefab, out var stack))
					_propPool[p.prefab] = stack = new Stack<GameObject>();
				stack.Push(p.instance);
			}
			c.props.Clear();
		}

		// ------------------------------------------------------------------------------- память

		void PruneMemory()
		{
			int far = loadRadius + 3;
			for (int i = _shapes.Count - 1; i >= 0; i--)
			{
				var s = _shapes[i];
				if (!s.IsGhost) continue;
				bool near = false;
				if (s.cells != null)
					foreach (var cell in s.cells)
						if (Chebyshev(cell, _center) <= far) { near = true; break; }
				if (!near) _shapes.RemoveAt(i);
			}

			_cellMap.Prune(_center, loadRadius + 4);
		}

		static int Chebyshev(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

		// ------------------------------------------------------------------------------- запросы

		/// <summary>Высота земли под мировой точкой (если чанк уже построен).</summary>
		public bool TryGetHeight(Vector3 worldPosition, out float height)
		{
			height = 0f;
			if (!_ready || roadGenerator == null) return false;
			var cell = roadGenerator.WorldToCell(worldPosition);
			if (!_chunks.TryGetValue(cell, out var c) || c.data == null) return false;
			Vector3 local = roadGenerator.WorldToGrid(worldPosition);
			height = c.data.SampleHeight(local.x, local.z) + roadGenerator.GridOrigin.y;
			return true;
		}

		// ------------------------------------------------------------------------------- отладка

#if UNITY_EDITOR
		void OnDrawGizmosSelected()
		{
			if (!drawGizmos || !Application.isPlaying || roadGenerator == null) return;

			var old = Gizmos.matrix;
			Gizmos.matrix = Matrix4x4.TRS(roadGenerator.GridOrigin, roadGenerator.GridRotation, Vector3.one);
			float cs = _settings.cellSize;

			foreach (var c in _chunks.Values)
			{
				Gizmos.color = c.running != null ? new Color(1f, 0.5f, 0f, 0.8f)
					: c.dirty ? new Color(1f, 1f, 0f, 0.6f)
					: c.built ? (c.detailed ? new Color(0.2f, 0.9f, 0.3f, 0.35f) : new Color(0.6f, 0.6f, 0.6f, 0.2f))
					: new Color(1f, 0f, 0f, 0.6f);
				float y = c.data != null ? (c.data.minY + c.data.maxY) * 0.5f : 0f;
				Gizmos.DrawWireCube(new Vector3(c.coord.x * cs, y, (c.coord.y + 0.5f) * cs), new Vector3(cs * 0.98f, 0.1f, cs * 0.98f));
			}

			foreach (var s in _shapes)
			{
				Gizmos.color = s.IsGhost ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : s.usedFallback ? Color.magenta : Color.cyan;
				foreach (var l in s.lines)
				{
					Gizmos.DrawLine(l.a, l.b);
					Vector3 dir = l.b - l.a;
					dir.y = 0f;
					if (dir.sqrMagnitude < 1e-6f) continue;
					Vector3 right = new Vector3(dir.z, 0f, -dir.x).normalized * l.halfWidth;
					Gizmos.DrawLine(l.a + right, l.b + right);
					Gizmos.DrawLine(l.a - right, l.b - right);
				}
			}

			Gizmos.color = new Color(1f, 0.4f, 1f, 0.7f);
			foreach (var c in _chunks.Values)
			{
				if (c.data == null) continue;
				foreach (var p in c.data.props)
					if (p.flatten && !p.rejected)
						Gizmos.DrawWireSphere(new Vector3(p.x, p.y, p.z), p.radius + p.flattenFalloff);
			}

			Gizmos.matrix = old;
		}
#endif
	}

#if UNITY_EDITOR
	/// <summary>Сообщает стримеру, что ассеты рельефа поменяли в инспекторе — можно перестроить на лету.</summary>
	public static class TerrainAssetEvents
	{
		public static event System.Action Changed;
		public static void Raise()
		{
			if (Application.isPlaying) Changed?.Invoke();
		}
	}
#endif
}
