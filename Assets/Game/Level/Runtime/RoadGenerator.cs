using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using static Game.Level.Runtime.RoadSegment;

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
		[Tooltip("Размер клетки в метрах. Должен совпадать с cellSize у всех префабов")]
		[Min(0.01f)] public float cellSize = 40f;

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

			LinkViews(c, node, seg);

			if (c.owner == null)
				_root = node;

			_open.Remove(c);
			_nodeCount++;

			bool isFork = seg.exits.Length > 1;
			c.state.sinceFork = isFork ? 0 : c.state.sinceFork + 1;

			var created = new List<BranchCursor>(seg.exits.Length);
			for (int i = 0; i < seg.exits.Length; i++)
			{
				var exit = seg.exits[i];
				created.Add(new BranchCursor
				{
					owner = node,
					exitIndex = i,
					exitTransform = exit.point.transform,
					lanes = exit.point.NumLanes,
					cell = c.cell + Rot(exit.nextCell, c.heading),
					heading = (c.heading + exit.turn) & 3,
					corridorHeading = c.corridorHeading,
					latMin = c.latMin,
					latMax = c.latMax,
					depth = c.depth + 1,
					state = c.state,
				});
			}

			if (isFork)
				SplitCorridors(created, node, c.heading);

			_open.AddRange(created);

			if (seg.TryGetComponent(out TrackEnemySpawner spawner))
				spawner.TrySpawnEnemies();
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
			var view = seg.roadView;
			if (view != null)
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

			if (c.owner == null) return;

			c.owner.children[c.exitIndex] = node;
			var prevView = c.owner.segment != null ? c.owner.segment.roadView : null;
			if (prevView != null && view != null)
			{
				prevView.SetNextRoad(c.exitIndex, c.owner.children.Length, view);
				view.previousRoad = prevView;
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
				if (!InCorridor(cell, c) || IsBlocked(cell, c)) return false;
			}

			foreach (var exit in seg.exits)
			{
				int h = (c.heading + exit.turn) & 3;
				if (h == DirBack && !allowBackward) return false;

				if (c.corridorHeading >= 0)
				{
					if (h == ((c.corridorHeading + 1) & 3) && c.latMax != int.MaxValue) return false;
					if (h == ((c.corridorHeading + 3) & 3) && c.latMin != int.MinValue) return false;
				}

				var next = c.cell + Rot(exit.nextCell, c.heading);
				if (!InCorridor(next, c) || IsBlocked(next, c)) return false;
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
			else if (!Mathf.Approximately(seg.cellSize, cellSize))
				problem = $"cellSize {seg.cellSize} не совпадает с генератором ({cellSize})";
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
			var cell = new Vector2Int(Mathf.RoundToInt(local.x / cellSize), Mathf.FloorToInt(local.z / cellSize));

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
				if (fork.segment != null && fork.segment.roadView != null)
					fork.segment.roadView.ClearNextRoad(i);
			}

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
				if (ch != null) DestroySubtree(ch);
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
				if (node.segment.TryGetComponent(out TrackEnemySpawner spawner))
					spawner.DespawnEnemies();
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

			if (Random.value < s.biome.exitChance)
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

		GameObject PickPrefab(BranchCursor c)
		{
			if (c.state.biome == null) return null;

			var candidates = BuildTypeCandidates(c, true);
			if (candidates.Count == 0)
				candidates = BuildTypeCandidates(c, false);
			if (candidates.Count == 0)
				candidates = ForceBiomeSwitchAndRetry(c);

			if (candidates.Count == 0) return null;
			return PickFromTypeCandidates(c, candidates);
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

		GameObject PickFromTypeCandidates(BranchCursor c, List<(SegmentType type, int weight, List<GameObject> prefabs)> candidates)
		{
			int total = 0;
			foreach (var cand in candidates) total += cand.weight;

			int roll = Random.Range(0, total);
			int cumulative = 0;
			foreach (var cand in candidates)
			{
				cumulative += cand.weight;
				if (roll < cumulative)
				{
					UpdateCounters(ref c.state, cand.type);
					return cand.prefabs[Random.Range(0, cand.prefabs.Count)];
				}
			}

			var last = candidates[candidates.Count - 1];
			UpdateCounters(ref c.state, last.type);
			return last.prefabs[Random.Range(0, last.prefabs.Count)];
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
			var size = new Vector3(cellSize * 0.9f, 0.1f, cellSize * 0.9f);

			Gizmos.color = new Color(0f, 0.7f, 1f, 0.6f);
			foreach (var cell in _occupied.Keys)
				Gizmos.DrawWireCube(CellCenterLocal(cell), size);

			Gizmos.color = Color.yellow;
			foreach (var c in _open)
				Gizmos.DrawWireCube(CellCenterLocal(c.cell), size);

			Gizmos.matrix = oldMatrix;
		}

		Vector3 CellCenterLocal(Vector2Int c) => new Vector3(c.x * cellSize, 0f, (c.y + 0.5f) * cellSize);
#endif
	}
}
