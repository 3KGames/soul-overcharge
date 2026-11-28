using System;
using System.Collections.Generic;
using System.Linq;
using Game.Level.Runtime.Config;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using static Game.Level.Runtime.RoadSegment;
using URandom = UnityEngine.Random;

namespace Game.Level.Runtime
{
	public class RoadGenerator : MonoBehaviour
	{
		[Header("Биомы")]
		public List<RoadBiomeDefinition> biomes = new List<RoadBiomeDefinition>();

		[Tooltip("Трансформ машины игрока")]
		public Transform player;

		[Header("Генерация")]
		[Tooltip("Сколько сегментов держать впереди игрока на каждой ветке")]
		[Min(1)] public int segmentsAhead = 3;
		[Tooltip("Сколько сегментов оставлять позади игрока")]
		[Min(0)] public int keepSegmentsBehind = 2;
		[Tooltip("Максимум новых сегментов за кадр")]
		[Min(1)] public int maxSpawnsPerFrame = 2;

		[Header("Сетка")] 
		public RoadGridSettings gridSettings;

		[Header("Развороты")]
		[Tooltip("Разрешить дороге поворачивать назад (змейка, петли). " +
		         "Без тупиков только при segmentsAhead + keepSegmentsBehind ≤ 5")]
		public bool allowBackward = true;

		[Header("Чередование")]
		[Tooltip("Максимум одинаковых поворотов подряд")]
		[Min(1)] public int maxSameTurnInRow = 1;
		[Tooltip("Максимум холмов подряд")]
		[Min(1)] public int maxHillsInRow = 2;

		[Header("Развилки")]
		[Tooltip("Минимум сегментов между развилками")]
		[Min(0)] public int minSegmentsBetweenForks = 8;
		[Tooltip("Через сколько сегментов после выбора ветки удалять остальные. Не больше keepSegmentsBehind")]
		[Min(1)] public int pruneAfterSegments = 2;

		private const int DirForward = 0, DirRight = 1, DirBack = 2, DirLeft = 3;
		private const int MaxWindowForBackward = 5;

		private struct GenState
		{
			public RoadBiomeDefinition biome;
			public SegmentType lastType;
			public int sameTurnCount;
			public int hillCount;
			public int sinceFork;
		}

		private class RoadNode
		{
			public RoadSegment segment;
			public RoadNode parent;
			public RoadNode[] children;
			public readonly List<Vector2Int> cells = new List<Vector2Int>();
			public int depth;
			public BiomeTag biome;
			public object customData;
		}

		private class ExitViewData
		{
			public RoadSegmentView view;
			public int exitIndex;
		}

		private class BranchCursor
		{
			public RoadNode owner;
			public int exitIndex;
			public Transform exitTransform;
			public int lanes = -1;
			public Vector2Int cell;
			public int heading;
			public int corridorHeading = -1;
			public int latMin = int.MinValue;
			public int latMax = int.MaxValue;
			public int depth;
			public GenState state;
			public bool stuck;
		}

		private readonly Dictionary<Vector2Int, RoadNode> _occupied = new Dictionary<Vector2Int, RoadNode>();
		private readonly List<BranchCursor> _open = new List<BranchCursor>();
		private readonly HashSet<GameObject> _warnedPrefabs = new HashSet<GameObject>();

		private RoadNode _root;
		private RoadNode _playerNode;
		private RoadNode _pendingFork;
		private RoadNode _chosenBranch;
		private int _nodeCount;
		private bool _warnedNoResolver;

		private Vector3 _gridPos;
		private Quaternion _gridRot;
		private bool _gridReady;

		/// <summary>Сегмент поставлен и выровнен. Клетки — в мировой сетке генератора.</summary>
		public event System.Action<RoadSegmentInfo> SegmentPlaced;

		/// <summary>Сегмент сейчас будет уничтожен (позади игрока или отрезанная ветка развилки).</summary>
		public event System.Action<RoadSegment> SegmentRemoved;

		/// <summary>Вызван ResetGenerator: старая дорога удалена, сейчас начнётся новая.</summary>
		public event System.Action GeneratorReset;

		public float CellSize => gridSettings != null ? gridSettings.cellSize : 0f;
		public bool IsGridReady => _gridReady;
		public Vector3 GridOrigin => _gridPos;
		public Quaternion GridRotation => _gridRot;

		/// <summary>Мировая точка → координаты в сетке (метры, без поворота генератора).</summary>
		public Vector3 WorldToGrid(Vector3 world) => Quaternion.Inverse(_gridRot) * (world - _gridPos);

		/// <summary>Координаты в сетке (метры) → мировая точка.</summary>
		public Vector3 GridToWorld(Vector3 grid) => _gridPos + _gridRot * grid;

		/// <summary>Клетка сетки, в которой лежит мировая точка.</summary>
		public Vector2Int WorldToCell(Vector3 world)
		{
			if (gridSettings == null) return Vector2Int.zero;
			Vector3 local = WorldToGrid(world);
			return new Vector2Int(Mathf.RoundToInt(local.x / gridSettings.cellSize), Mathf.FloorToInt(local.z / gridSettings.cellSize));
		}

		/// <summary>Все живые сегменты (для тех, кто подписался позже первой генерации).</summary>
		public void GetPlacedSegments(List<RoadSegmentInfo> result)
		{
			result.Clear();
			if (_root != null) CollectSegments(_root, result);
		}

		static void CollectSegments(RoadNode node, List<RoadSegmentInfo> result)
		{
			if (node.segment != null)
				result.Add(new RoadSegmentInfo(node.segment, node.biome, node.cells.ToArray(), node.depth));
			foreach (var ch in node.children)
				if (ch != null && ch != node) CollectSegments(ch, result); // Skip self-reference (linear segments)
		}

		public BiomeTag CurrentBiomeTag =>
			_playerNode != null ? _playerNode.biome : _root != null ? _root.biome : BiomeTag.None;

		public RoadSegmentView PlayerSegmentView =>
			_playerNode != null && _playerNode.segment != null ? _playerNode.segment.roadView : null;

		private IObjectResolver _resolver;

		[Inject]
		public void Construct(IObjectResolver resolver)
		{
			_resolver = resolver;
		}

		void Start()
		{
			ValidateSetup();
			StartGeneration();
		}

		void Update()
		{
			if (_root == null && _open.Count == 0) return;
			UpdatePlayerNode();
			GenerateAhead(maxSpawnsPerFrame);
		}

		void OnValidate()
		{
			if (allowBackward && segmentsAhead + keepSegmentsBehind > MaxWindowForBackward)
				Debug.LogWarning($"[RoadGenerator] Развороты включены, но segmentsAhead + keepSegmentsBehind = " +
				                 $"{segmentsAhead + keepSegmentsBehind} > {MaxWindowForBackward}. Дорога может запереть сама себя. " +
				                 "Уменьшите окно или выключите allowBackward.", this);

			if (allowBackward && pruneAfterSegments > keepSegmentsBehind)
				Debug.LogWarning("[RoadGenerator] pruneAfterSegments больше keepSegmentsBehind: развилка будет держать лишние клетки, " +
				                 "и гарантия разворотов ослабнет.", this);
		}

		void StartGeneration()
		{
			_gridPos = transform.position;
			_gridRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
			_gridReady = true;

			_open.Add(new BranchCursor
			{
				exitTransform = transform,
				heading = DirForward,
				state = new GenState { biome = WeightedRandomBiome(biomes), lastType = SegmentType.Straight },
			});

			GenerateAhead(10000);
		}

		void GenerateAhead(int budget)
		{
			for (int i = 0; i < budget; i++)
			{
				var cursor = NextCursorToExtend();
				if (cursor == null) return;
				Extend(cursor);
			}
		}

		BranchCursor NextCursorToExtend()
		{
			int playerDepth = _playerNode != null ? _playerNode.depth : _root != null ? _root.depth : 0;
			BranchCursor best = null;
			foreach (var c in _open)
			{
				if (c.stuck || c.depth - playerDepth >= segmentsAhead) continue;
				if (best == null || c.depth < best.depth) best = c;
			}
			return best;
		}

		void Extend(BranchCursor c)
		{
			UpdateBiomeState(ref c.state);

			GameObject prefab = PickPrefab(c);
			if (prefab == null)
			{
				Debug.LogError($"[RoadGenerator] Ветка остановлена: ни один префаб не помещается " +
				               $"(полос на входе: {c.lanes}, клетка: {c.cell}, направление: {c.heading}). " +
				               "Проверьте, что в биомах есть однаклеточная прямая с таким числом полос.", this);
				c.stuck = true;
				return;
			}

			GameObject go = InstantiateSegment(prefab);
			if (!go.TryGetComponent(out RoadSegment seg))
			{
				Debug.LogError($"[RoadGenerator] Префаб {prefab.name} не имеет компонента RoadSegment!", this);
				Destroy(go);
				c.stuck = true;
				return;
			}

			// Обновляем счётчик ДО копирования state в дочерние курсоры:
			// иначе они навсегда получают sinceFork = 0 и вилки блокируются в CanPlace.
			bool isFork = seg.exits != null && seg.exits.Length > 1;
			c.state.sinceFork = isFork ? 0 : c.state.sinceFork + 1;

			AlignSegment(seg, c.exitTransform);

			var node = new RoadNode
			{
				segment = seg,
				parent = c.owner,
				depth = c.depth,
				children = new RoadNode[seg.exits.Length],
				biome = c.state.biome != null ? c.state.biome.biomeTag : BiomeTag.None,
			};

			foreach (var local in seg.cells)
			{
				var cell = c.cell + Rot(local, c.heading);
				node.cells.Add(cell);
				_occupied[cell] = node;
			}

			// === VARIANT B: Handle Fork Prefabs ===
			// roadView — сериализованная ссылка на входной view (у вилки это ForkEntry).
			// Если она не проставлена, fallback по isForkEntry/spline, как в RoadSegment.SetLaneCountOnViews.
			var allViews = go.GetComponentsInChildren<RoadSegmentView>();
			var entryView = ResolveEntryView(seg, allViews);
			var childViews = entryView != null && entryView.isForkEntry && entryView.childViews != null &&
			                 entryView.childViews.Length > 0
				? entryView.childViews
				: allViews.Where(v => v != entryView).ToArray();

			// Ensure laneCount is set on all views (critical for gizmos, spline follower, etc.)
			int entryLanes = isFork ? seg.exits.Sum(e => e.point.NumLanes) : seg.exits[0].point.NumLanes;
			if (entryView.laneCount == 0) entryView.laneCount = entryLanes;
			foreach (var cv in childViews)
				if (cv.laneCount == 0) cv.laneCount = cv.GetComponent<RoadSegmentView>().laneCount; // will be set per-exit below

			// Sync laneWidth from RoadSegment to all views
			float laneWidth = seg.laneWidth;
			entryView.laneWidth = laneWidth;
			foreach (var cv in childViews) cv.laneWidth = laneWidth;

			if (isFork && childViews.Length == seg.exits.Length)
			{
				// Setup Entry View with Exit Views mapping
				int[] firstLanes = new int[seg.exits.Length];
				int[] laneCounts = new int[seg.exits.Length];
				for (int i = 0; i < seg.exits.Length; i++)
				{
					firstLanes[i] = seg.exits[i].point.FirstEntryLane;
					laneCounts[i] = seg.exits[i].point.NumLanes;
				}
				entryView.SetupForkEntry(childViews, firstLanes, laneCounts);

				// Set laneCount on each child view
				for (int i = 0; i < childViews.Length; i++)
					childViews[i].laneCount = laneCounts[i];

				// Generate constant edge profiles for fork views
				GenerateConstantEdgeProfiles(entryView, entryLanes, laneWidth);
				for (int i = 0; i < childViews.Length; i++)
					GenerateConstantEdgeProfiles(childViews[i], laneCounts[i], laneWidth);

				// Each Exit View gets its own node in the graph
				for (int i = 0; i < childViews.Length; i++)
				{
					var cv = childViews[i];
					// Link linear: entryView -> childView -> next segment
					cv.SetupLinear(entryView, null, new[] { firstLanes[i] }, new[] { laneCounts[i] });

					// Register childView as separate node for graph traversal
					// Each fork exit leads to ONE next segment, so children array size = 1
					var exitNode = new RoadNode
					{
						segment = seg, // Same RoadSegment data
						parent = node,
						depth = c.depth + 1,
						children = new RoadNode[1], // Single next segment after this fork exit
						biome = c.state.biome?.biomeTag ?? BiomeTag.None,
						// Custom: store which childView this node represents
						customData = new ExitViewData { view = cv, exitIndex = i }
					};

					// Add to graph
					node.children[i] = exitNode;
					var forkBranchCell = c.cell + Rot(seg.exits[i].nextCell, c.heading);
					var forkBranchHeading = (c.heading + seg.exits[i].turn) & 3;
					Debug.Log($"[RoadGenerator] Fork branch {i}: parentCell={c.cell} parentHeading={c.heading} exitNextCell={seg.exits[i].nextCell} exitTurn={seg.exits[i].turn} => branchCell={forkBranchCell} branchHeading={forkBranchHeading} lanes={seg.exits[i].point.NumLanes}");
					_open.Add(new BranchCursor
					{
						owner = exitNode,
						exitIndex = 0, // Fork exit node has only 1 child slot (index 0)
						// Дочерний spline view стоит в (0,0,0) с identity-поворотом —
						// выравнивание по нему ставит следующий сегмент прямо в центр вилки.
						// Используем ту же ConnectionPoint, от которой взяты cell и heading.
						exitTransform = seg.exits[i].point.transform,
						lanes = seg.exits[i].point.NumLanes,
						cell = forkBranchCell,
						heading = forkBranchHeading,
						depth = c.depth + 1,
						state = c.state,
					});
				}
			}
			else
			{
				// Normal linear segment
				if (entryView.laneCount == 0) entryView.laneCount = seg.exits[0].point.NumLanes;
				entryView.laneWidth = laneWidth;
				entryView.SetupLinear(
					c.owner?.segment?.roadView, 
					null, // nextRoad set later when next segment spawns
					new[] { c.lanes == -1 ? 0 : c.lanes }, // Simplified
					new[] { c.lanes == -1 ? entryView.laneCount : c.lanes }
				);

				// Generate constant edge profiles for linear view
				GenerateConstantEdgeProfiles(entryView, entryView.laneCount, laneWidth);

				// Single branch for normal segment
				var cursor = new BranchCursor
				{
					owner = node,
					exitIndex = 0,
					exitTransform = seg.exits[0].point.transform,
					lanes = seg.exits[0].point.NumLanes,
					cell = c.cell + Rot(seg.exits[0].nextCell, c.heading),
					heading = (c.heading + seg.exits[0].turn) & 3,
					corridorHeading = c.corridorHeading,
					latMin = c.latMin,
					latMax = c.latMax,
					depth = c.depth + 1,
					state = c.state,
				};
				node.children[0] = node; // Self-reference for linear segment
				_open.Add(cursor);
			}

			LinkViews(c, node, seg);

			if (c.owner == null)
				_root = node;

			_open.Remove(c);
			_nodeCount++;

			if (isFork)
				SplitCorridors(_open.Where(x => x.owner != null && x.owner.parent == node).ToList(), node, c.heading);

			if (seg.TryGetComponent(out TrackEnemySpawner spawner))
				spawner.TrySpawnEnemies();

			NotifyPlaced(node);
		}

		void NotifyPlaced(RoadNode node)
		{
			if (SegmentPlaced == null) return;
			try
			{
				SegmentPlaced(new RoadSegmentInfo(node.segment, node.biome, node.cells.ToArray(), node.depth));
			}
			catch (System.Exception e)
			{
				Debug.LogException(e, this);
			}
		}

		void NotifyRemoved(RoadSegment segment)
		{
			if (SegmentRemoved == null || segment == null) return;
			try
			{
				SegmentRemoved(segment);
			}
			catch (System.Exception e)
			{
				Debug.LogException(e, this);
			}
		}

		static RoadSegmentView ResolveEntryView(RoadSegment seg, RoadSegmentView[] allViews)
		{
			if (seg.roadView != null && allViews.Contains(seg.roadView)) return seg.roadView;
			var forkEntry = allViews.FirstOrDefault(v => v.isForkEntry);
			if (forkEntry != null) return forkEntry;
			return allViews.FirstOrDefault(v => v.spline != null) ?? allViews.FirstOrDefault();
		}

		GameObject InstantiateSegment(GameObject prefab)
		{
			if (_resolver != null)
				return _resolver.Instantiate(prefab);

			if (!_warnedNoResolver)
			{
				_warnedNoResolver = true;
				Debug.LogWarning("[RoadGenerator] IObjectResolver не заинжекчен (VContainer). Сегменты создаются без инъекции, " +
				                 "враги на них не заспавнятся. Добавьте генератор в Auto Inject Game Objects у LifetimeScope.", this);
			}
			return Instantiate(prefab);
		}

		void LinkViews(BranchCursor c, RoadNode node, RoadSegment seg)
		{
			var view = seg.roadView; // ForkEntry or Normal View
			if (view == null) return;
			
			// Set exit lane mapping for ForkEntry
			if (seg.exits.Length > 1)
			{
				var first = new int[seg.exits.Length];
				var count = new int[seg.exits.Length];
				for (int i = 0; i < seg.exits.Length; i++)
				{
					first[i] = seg.exits[i].point.FirstEntryLane;
					count[i] = seg.exits[i].point.NumLanes;
				}
				view.SetExitLanes(first, count);
			}

			// Link to previous segment's exit view (or entry view of previous fork)
			if (c.owner != null)
			{
				// Find the correct previous view for this branch
				RoadSegmentView prevView = null;
				if (c.owner.customData is ExitViewData evd)
				{
					prevView = evd.view; // We came from an exit view of a fork
				}
				else if (c.owner.segment != null)
				{
					prevView = c.owner.segment.roadView; // Normal segment
				}
				
				if (prevView != null && view != null)
				{
					// Connect previous view's nextRoad to this view
					prevView.nextRoad = view;
					view.previousRoad = prevView;
				}
				
				c.owner.children[c.exitIndex] = node;
			}
		}

		void SplitCorridors(List<BranchCursor> branches, RoadNode fork, int forkHeading)
		{
			branches.Sort((a, b) => Lateral(a.cell, forkHeading).CompareTo(Lateral(b.cell, forkHeading)));
			for (int i = 0; i < branches.Count; i++)
			{
				int lat = Lateral(branches[i].cell, forkHeading);
				branches[i].corridorHeading = forkHeading;
				if (i > 0) branches[i].latMin = lat;
				if (i < branches.Count - 1) branches[i].latMax = lat;
			}
			_pendingFork = fork;
		}

		static int Lateral(Vector2Int cell, int heading)
		{
			var right = Rot(new Vector2Int(1, 0), heading);
			return cell.x * right.x + cell.y * right.y;
		}

		bool CanPlace(RoadSegment seg, BranchCursor c)
		{
			if (!IsBaked(seg)) return false;

			if (seg.exits.Length > 1 && (_pendingFork != null || c.state.sinceFork < minSegmentsBetweenForks))
				return false;

			foreach (var local in seg.cells)
			{
				var cell = c.cell + Rot(local, c.heading);
				if (!InCorridor(cell, c))
				{
					Debug.Log($"[RoadGenerator] CanPlace FAIL InCorridor: seg={seg.name} c.cell={c.cell} c.heading={c.heading} local={local} => cell={cell} corridorHeading={c.corridorHeading} latMin={c.latMin} latMax={c.latMax}");
					return false;
				}
				if (IsBlocked(cell, c))
				{
					Debug.Log($"[RoadGenerator] CanPlace FAIL IsBlocked: cell={cell}");
					return false;
				}
			}

			foreach (var exit in seg.exits)
			{
				int h = (c.heading + exit.turn) & 3;
				if (h == DirBack && !allowBackward)
				{
					Debug.Log($"[RoadGenerator] CanPlace FAIL DirBack: h={h} allowBackward={allowBackward}");
					return false;
				}

				if (c.corridorHeading >= 0)
				{
					if (h == ((c.corridorHeading + 1) & 3) && c.latMax != int.MaxValue)
					{
						Debug.Log($"[RoadGenerator] CanPlace FAIL CorridorRight: h={h} corridorHeading={c.corridorHeading} latMax={c.latMax}");
						return false;
					}
					if (h == ((c.corridorHeading + 3) & 3) && c.latMin != int.MinValue)
					{
						Debug.Log($"[RoadGenerator] CanPlace FAIL CorridorLeft: h={h} corridorHeading={c.corridorHeading} latMin={c.latMin}");
						return false;
					}
				}

				var next = c.cell + Rot(exit.nextCell, c.heading);
				if (!InCorridor(next, c))
				{
					Debug.Log($"[RoadGenerator] CanPlace FAIL Exit InCorridor: next={next}");
					return false;
				}
				if (IsBlocked(next, c))
				{
					Debug.Log($"[RoadGenerator] CanPlace FAIL Exit IsBlocked: next={next}");
					return false;
				}
			}

			return true;
		}

		static bool InCorridor(Vector2Int cell, BranchCursor c)
		{
			if (c.corridorHeading < 0) return true;
			int lat = Lateral(cell, c.corridorHeading);
			return lat >= c.latMin && lat <= c.latMax;
		}

		bool IsBlocked(Vector2Int cell, BranchCursor self)
		{
			if (_occupied.ContainsKey(cell)) return true;
			foreach (var other in _open)
				if (other != self && other.cell == cell) return true;
			return false;
		}

		bool IsBaked(RoadSegment seg)
		{
			string problem = null;
			if (seg.cells == null || seg.cells.Length == 0 || seg.exits == null || seg.exits.Length == 0)
				problem = "не запечён (Bake grid)";
			else if (!Mathf.Approximately(seg.cellSize, gridSettings.cellSize))
				problem = $"cellSize {seg.cellSize} не совпадает с генератором ({gridSettings.cellSize})";
			else if (seg.entryPoint == null)
				problem = "не назначен entryPoint";
			else
				foreach (var e in seg.exits)
					if (e.point == null) { problem = "у выхода не назначен ConnectionPoint"; break; }

			if (problem == null) return true;
			if (_warnedPrefabs.Add(seg.gameObject))
				Debug.LogWarning($"[RoadGenerator] Префаб {seg.name} пропущен: {problem}", seg);
			return false;
		}

		static Vector2Int Rot(Vector2Int v, int heading)
		{
			switch (heading & 3)
			{
				case DirRight: return new Vector2Int(v.y, -v.x);
				case DirBack:  return new Vector2Int(-v.x, -v.y);
				case DirLeft:  return new Vector2Int(-v.y, v.x);
				default:       return v;
			}
		}

		void UpdatePlayerNode()
		{
			if (player == null) return;

			Vector3 local = Quaternion.Inverse(_gridRot) * (player.position - _gridPos);
			var cell = new Vector2Int(Mathf.RoundToInt(local.x / gridSettings.cellSize), Mathf.FloorToInt(local.z / gridSettings.cellSize));

			if (!_occupied.TryGetValue(cell, out var node) || node == _playerNode) return;

			_playerNode = node;
			OnPlayerEnteredNode(node);
		}

		void OnPlayerEnteredNode(RoadNode node)
		{
			if (_pendingFork != null && node.depth > _pendingFork.depth)
			{
				RoadNode branch = node;
				while (branch != null && branch.parent != _pendingFork)
					branch = branch.parent;

				if (branch != null)
				{
					if (_chosenBranch == null)
						CommitBranch(branch);

					if (branch == _chosenBranch && node.depth - _pendingFork.depth >= pruneAfterSegments)
						ResolveFork();
				}
			}

			RemoveNodesBehind();
		}

		void CommitBranch(RoadNode branch)
		{
			_chosenBranch = branch;
			_open.RemoveAll(c => !IsInSubtree(c.owner, branch));
		}

		void ResolveFork()
		{
			var fork = _pendingFork;
			for (int i = 0; i < fork.children.Length; i++)
			{
				var child = fork.children[i];
				if (child == null || child == _chosenBranch) continue;
				
				DestroySubtree(child);
				fork.children[i] = null;
				
				// Clear graph connections on ForkEntry
				if (fork.segment?.roadView != null)
				{
					var fv = fork.segment.roadView;
					if (fv.childViews != null && i < fv.childViews.Length && fv.childViews[i] != null)
					{
						fv.childViews[i].previousRoad = null;
					}
				}
			}
				
			// Reset corridor constraints on ALL open cursors
			foreach (var c in _open)
			{
				c.corridorHeading = -1;
				c.latMin = int.MinValue;
				c.latMax = int.MaxValue;
			}
				
			_pendingFork = null;
			_chosenBranch = null;
		}

		void RemoveNodesBehind()
		{
			while (_root != null && _playerNode != null && _root != _pendingFork
			       && _playerNode.depth - _root.depth > keepSegmentsBehind)
			{
				RoadNode next = null;
				foreach (var ch in _root.children)
					if (ch != null) { next = ch; break; }
				if (next == null) break;

				DestroyNode(_root);
				next.parent = null;
				_root = next;
			}
		}

		static bool IsInSubtree(RoadNode node, RoadNode root)
		{
			for (var n = node; n != null; n = n.parent)
				if (n == root) return true;
			return false;
		}

		void DestroySubtree(RoadNode node)
		{
			foreach (var ch in node.children)
				if (ch != null && ch != node) DestroySubtree(ch); // Skip self-reference (linear segments)
			DestroyNode(node);
		}

		void DestroyNode(RoadNode node)
		{
			foreach (var cell in node.cells)
				if (_occupied.TryGetValue(cell, out var n) && n == node)
					_occupied.Remove(cell);

			_open.RemoveAll(c => c.owner == node);
			if (_playerNode == node) _playerNode = null;

			if (node.segment != null)
			{
				// 1. Notify subscribers BEFORE deactivating segment (allows immediate despawn)
				NotifyRemoved(node.segment);

				// 2. Despawn enemies on the segment that is being removed
				if (node.segment.TryGetComponent(out TrackEnemySpawner spawner))
					spawner.DespawnEnemies();

				// 3. Deactivate segment immediately (stops FixedUpdate on RoadSegmentView, removes from physics)
				node.segment.gameObject.SetActive(false);

				// 4. Destroy at end of frame for GC cleanup
				Destroy(node.segment.gameObject);
			}

			_nodeCount--;
		}

		void UpdateBiomeState(ref GenState s)
		{
			if (biomes == null || biomes.Count == 0)
			{
				Debug.LogError("[RoadGenerator] Список biomes пуст — добавьте хотя бы один биом!", this);
				s.biome = null;
				return;
			}

			if (s.biome == null)
			{
				s.biome = WeightedRandomBiome(biomes);
				return;
			}

			if (URandom.value < s.biome.exitChance)
			{
				var previous = s.biome;
				var next = PickNextBiome(new HashSet<RoadBiomeDefinition> { previous });
				if (next != null)
				{
					s.biome = next;
					Debug.Log($"[RoadGenerator] Вышел из биома {previous.biomeTag} → новый биом: {next.biomeTag}");
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

			if (total <= 0f) return pool[URandom.Range(0, pool.Count)];

			float roll = URandom.Range(0f, total);
			float cumulative = 0f;
			foreach (var b in pool)
			{
				cumulative += Mathf.Max(0f, b.weight);
				if (roll < cumulative) return b;
			}
			return pool[pool.Count - 1];
		}

		GameObject PickPrefab(BranchCursor c)
		{
			if (c.state.biome == null) return null;

			var candidates = BuildTypeCandidates(c, true);
			if (candidates.Count == 0)
				candidates = BuildTypeCandidates(c, false);
			if (candidates.Count == 0)
				candidates = ForceBiomeSwitchAndRetry(c);

			if (candidates.Count == 0) return null;
			return PickFromTypeCandidates(ref c.state, candidates);
		}

		List<(SegmentType type, int weight, List<GameObject> prefabs)> ForceBiomeSwitchAndRetry(BranchCursor c)
		{
			var original = c.state.biome;
			var tried = new HashSet<RoadBiomeDefinition> { original };

			for (int i = 0; i < biomes.Count; i++)
			{
				var next = PickNextBiome(tried);
				if (next == null) break;

				tried.Add(next);
				c.state.biome = next;

				var candidates = BuildTypeCandidates(c, true);
				if (candidates.Count == 0)
					candidates = BuildTypeCandidates(c, false);

				if (candidates.Count > 0) return candidates;
			}

			c.state.biome = original;
			return new List<(SegmentType, int, List<GameObject>)>();
		}

		List<(SegmentType type, int weight, List<GameObject> prefabs)> BuildTypeCandidates(BranchCursor c, bool respectBlocks)
		{
			var list = new List<(SegmentType, int, List<GameObject>)>();
			var b = c.state.biome;
			if (b == null) return list;

			bool blockLeft = false, blockRight = false, blockHillUp = false, blockHillDown = false;
			if (respectBlocks)
			{
				var s = c.state;
				blockLeft     = s.lastType == SegmentType.TurnLeft  && s.sameTurnCount >= maxSameTurnInRow;
				blockRight    = s.lastType == SegmentType.TurnRight && s.sameTurnCount >= maxSameTurnInRow;
				bool blockHills = s.hillCount >= maxHillsInRow;
				blockHillUp   = blockHills || s.lastType == SegmentType.HillDown;
				blockHillDown = blockHills || s.lastType == SegmentType.HillUp;
			}

			AddTypeCandidate(list, c, b.straightPrefabs, SegmentType.Straight, b.weightStraight);
			if (!blockLeft)     AddTypeCandidate(list, c, b.turnLeftPrefabs,  SegmentType.TurnLeft,  b.weightTurnLeft);
			if (!blockRight)    AddTypeCandidate(list, c, b.turnRightPrefabs, SegmentType.TurnRight, b.weightTurnRight);
			if (!blockHillUp)   AddTypeCandidate(list, c, b.hillUpPrefabs,    SegmentType.HillUp,    b.weightHillUp);
			if (!blockHillDown) AddTypeCandidate(list, c, b.hillDownPrefabs,  SegmentType.HillDown,  b.weightHillDown);
			AddTypeCandidate(list, c, b.forkPrefabs, SegmentType.Fork, b.weightFork);

			return list;
		}

		void AddTypeCandidate(List<(SegmentType, int, List<GameObject>)> list, BranchCursor c,
			GameObject[] prefabs, SegmentType type, int weight)
		{
			if (prefabs == null || prefabs.Length == 0 || weight <= 0) return;

			var valid = new List<GameObject>();
			foreach (var p in prefabs)
			{
				if (p == null || !p.TryGetComponent(out RoadSegment seg)) continue;
				if (c.lanes != -1 && (seg.entryPoint == null || seg.entryPoint.NumLanes != c.lanes)) continue;
				if (!CanPlace(seg, c)) continue;
				valid.Add(p);
			}

			if (valid.Count > 0)
				list.Add((type, weight, valid));
		}

		GameObject PickFromTypeCandidates(ref GenState state, List<(SegmentType type, int weight, List<GameObject> prefabs)> candidates)
		{
			int total = 0;
			foreach (var cand in candidates) total += cand.weight;

			int roll = URandom.Range(0, total);
			int cumulative = 0;
			foreach (var cand in candidates)
			{
				cumulative += cand.weight;
				if (roll < cumulative)
				{
					UpdateCounters(ref state, cand.type);
					return cand.prefabs[URandom.Range(0, cand.prefabs.Count)];
				}
			}

			var last = candidates[candidates.Count - 1];
			UpdateCounters(ref state, last.type);
			return last.prefabs[URandom.Range(0, last.prefabs.Count)];
		}

		void UpdateCounters(ref GenState s, SegmentType chosen)
		{
			bool isTurn = chosen == SegmentType.TurnLeft || chosen == SegmentType.TurnRight;
			if (isTurn)
				s.sameTurnCount = chosen == s.lastType ? s.sameTurnCount + 1 : 1;
			else
				s.sameTurnCount = 0;

			bool isHill = chosen == SegmentType.HillUp || chosen == SegmentType.HillDown;
			s.hillCount = isHill ? s.hillCount + 1 : 0;

			s.lastType = chosen;
		}

		void AlignSegment(RoadSegment seg, Transform targetExit)
		{
			Quaternion rotationDiff = targetExit.rotation * Quaternion.Inverse(seg.entryPoint.transform.rotation);
			seg.transform.rotation = rotationDiff * seg.transform.rotation;
			Vector3 posDiff = targetExit.position - seg.entryPoint.transform.position;
			seg.transform.position += posDiff;
		}

		void ValidateSetup()
		{
			if (player == null)
				Debug.LogWarning("[RoadGenerator] Не назначен player — генератор не узнает, где машина, и дорога не будет расти.", this);

			if (Vector3.Angle(transform.up, Vector3.up) > 0.1f)
				Debug.LogWarning("[RoadGenerator] Трансформ генератора повёрнут не только по Y. Сетка плоская: оставьте X и Z поворота нулевыми.", this);

			if (biomes == null || biomes.Count == 0)
			{
				Debug.LogError("[RoadGenerator] Список biomes пуст — добавьте хотя бы один биом!", this);
				return;
			}

			if (gridSettings == null)
			{
				Debug.LogError("[RoadGenerator] RoadGridSettings не назначен!", this);
				return;
			}

			var laneCounts = new HashSet<int>();
			foreach (var b in biomes)
				foreach (var p in AllPrefabs(b))
					if (p != null && p.TryGetComponent(out RoadSegment s) && s.exits != null)
						foreach (var e in s.exits)
							if (e.point != null) laneCounts.Add(e.point.NumLanes);

			foreach (int lanes in laneCounts)
			{
				var missing = new List<BiomeTag>();
				foreach (var b in biomes)
					if (!HasOneCellStraight(b, lanes)) missing.Add(b.biomeTag);

				if (missing.Count == biomes.Count)
					Debug.LogError($"[RoadGenerator] Ни в одном биоме нет однаклеточной прямой на {lanes} полос. " +
					               "Дорога может остановиться.", this);
				else if (missing.Count > 0)
					Debug.LogWarning($"[RoadGenerator] Нет однаклеточной прямой на {lanes} полос в биомах: {string.Join(", ", missing)}. " +
					                 "В тесных местах генератор будет переключать биом.", this);
			}
		}

		static bool HasOneCellStraight(RoadBiomeDefinition b, int lanes)
		{
			if (b.straightPrefabs == null || b.weightStraight <= 0) return false;
			foreach (var p in b.straightPrefabs)
			{
				if (p == null || !p.TryGetComponent(out RoadSegment s)) continue;
				if (s.entryPoint != null && s.entryPoint.NumLanes == lanes
				    && s.cells != null && s.cells.Length == 1
				    && s.exits != null && s.exits.Length == 1 && s.exits[0].turn == 0)
					return true;
			}
			return false;
		}

		static IEnumerable<GameObject> AllPrefabs(RoadBiomeDefinition b)
		{
			var arrays = new[] { b.straightPrefabs, b.turnLeftPrefabs, b.turnRightPrefabs, b.hillUpPrefabs, b.hillDownPrefabs, b.forkPrefabs };
			foreach (var arr in arrays)
			{
				if (arr == null) continue;
				foreach (var p in arr) yield return p;
			}
		}

		public void ResetGenerator()
		{
			if (_root != null) DestroySubtree(_root);

			_occupied.Clear();
			_open.Clear();
			_root = _playerNode = _pendingFork = _chosenBranch = null;
			_nodeCount = 0;

			try { GeneratorReset?.Invoke(); }
			catch (System.Exception e) { Debug.LogException(e, this); }

			StartGeneration();
		}

		public RoadSegmentView GetFirstSegmentView()
		{
			return _root != null && _root.segment != null ? _root.segment.roadView : null;
		}

		public RoadSegmentView GetSegmentBehindPlayer(int segmentsBehind)
		{
			if (_nodeCount < 2) return null;
			return GetFirstSegmentView();
		}

#if UNITY_EDITOR
		void OnDrawGizmosSelected()
		{
			if (!Application.isPlaying) return;

			Matrix4x4 oldMatrix = Gizmos.matrix;
			Gizmos.matrix = Matrix4x4.TRS(_gridPos, _gridRot, Vector3.one);
			var size = new Vector3(gridSettings.cellSize * 0.9f, 0.1f, gridSettings.cellSize * 0.9f);

			Gizmos.color = new Color(0f, 0.7f, 1f, 0.6f);
			foreach (var cell in _occupied.Keys)
				Gizmos.DrawWireCube(CellCenterLocal(cell), size);

			Gizmos.color = Color.yellow;
			foreach (var c in _open)
				Gizmos.DrawWireCube(CellCenterLocal(c.cell), size);

			Gizmos.matrix = oldMatrix;
		}

		Vector3 CellCenterLocal(Vector2Int c) => new Vector3(c.x * gridSettings.cellSize, 0f, (c.y + 0.5f) * gridSettings.cellSize);

		private static void GenerateConstantEdgeProfiles(RoadSegmentView view, int laneCount, float laneWidth)
		{
			if (view == null || view.spline == null || laneCount <= 0) return;
			
			float length = view.spline.CalculateLength();
			int tiles = Mathf.Max(1, Mathf.FloorToInt(length / (laneWidth * 2f)));
			
			view.leftEdgeProfile = new int[tiles];
			view.rightEdgeProfile = new int[tiles];
			for (int i = 0; i < tiles; i++)
			{
				view.leftEdgeProfile[i] = 0;
				view.rightEdgeProfile[i] = laneCount - 1;
			}
		}
#endif
	}

	/// <summary>Что сообщает генератор о поставленном сегменте.</summary>
	public readonly struct RoadSegmentInfo
	{
		public readonly RoadSegment segment;
		public readonly BiomeTag biome;
		/// <summary>Клетки сегмента в мировой сетке генератора.</summary>
		public readonly Vector2Int[] cells;
		public readonly int depth;

		public RoadSegmentInfo(RoadSegment segment, BiomeTag biome, Vector2Int[] cells, int depth)
		{
			this.segment = segment;
			this.biome = biome;
			this.cells = cells;
			this.depth = depth;
		}
	}
}
