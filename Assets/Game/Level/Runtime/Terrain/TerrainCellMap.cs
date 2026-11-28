using System.Collections.Generic;
using UnityEngine;

namespace Game.Level.Runtime.Terrain
{
	/// <summary>
	/// Биом и средняя высота для каждой клетки сетки (клетка = чанк рельефа).
	/// Клетки с дорогой берут значения с дороги. Остальные один раз «угадываются» по ближайшим известным
	/// и дальше не меняются — поэтому соседние чанки, построенные в разное время, сходятся без ступенек.
	/// Только главный поток.
	/// </summary>
	public sealed class TerrainCellMap
	{
		readonly Dictionary<Vector2Int, CellInfo> _cells = new Dictionary<Vector2Int, CellInfo>();
		readonly Dictionary<BiomeTag, int> _votes = new Dictionary<BiomeTag, int>();

		public int Count => _cells.Count;

		public void Clear() => _cells.Clear();

		public bool TryGet(Vector2Int cell, out CellInfo info) => _cells.TryGetValue(cell, out info);

		/// <summary>Записать клетку с дорогой. Возвращает true, если значение поменялось.</summary>
		public bool SetRoadCell(Vector2Int cell, BiomeTag biome, float height)
		{
			if (_cells.TryGetValue(cell, out var old) && old.fromRoad && old.biome == biome && Mathf.Abs(old.height - height) < 0.01f)
				return false;

			_cells[cell] = new CellInfo { biome = biome, height = height, fromRoad = true };
			return true;
		}

		/// <summary>Значение клетки; если его нет — угадать по ближайшим известным и запомнить.</summary>
		public CellInfo GetOrInfer(Vector2Int cell, BiomeTag fallbackBiome, float fallbackHeight, int searchRadius)
		{
			if (_cells.TryGetValue(cell, out var info)) return info;

			info = Infer(cell, fallbackBiome, fallbackHeight, searchRadius);
			_cells[cell] = info;
			return info;
		}

		CellInfo Infer(Vector2Int cell, BiomeTag fallbackBiome, float fallbackHeight, int searchRadius)
		{
			for (int r = 1; r <= searchRadius; r++)
			{
				_votes.Clear();
				float heightSum = 0f;
				int heightCount = 0;

				for (int dy = -r; dy <= r; dy++)
				for (int dx = -r; dx <= r; dx++)
				{
					if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
					if (!_cells.TryGetValue(new Vector2Int(cell.x + dx, cell.y + dy), out var n)) continue;

					// голос дороги весит больше, чем угаданной клетки
					int w = n.fromRoad ? 3 : 1;
					_votes.TryGetValue(n.biome, out int v);
					_votes[n.biome] = v + w;
					heightSum += n.height * w;
					heightCount += w;
				}

				if (heightCount == 0) continue;

				BiomeTag best = fallbackBiome;
				int bestVotes = -1;
				foreach (var kv in _votes)
					if (kv.Value > bestVotes || (kv.Value == bestVotes && kv.Key < best))
					{
						best = kv.Key;
						bestVotes = kv.Value;
					}

				return new CellInfo { biome = best, height = heightSum / heightCount, fromRoad = false };
			}

			return new CellInfo { biome = fallbackBiome, height = fallbackHeight, fromRoad = false };
		}

		readonly List<Vector2Int> _toRemove = new List<Vector2Int>();

		/// <summary>Забыть клетки дальше radius от центра (по Чебышёву).</summary>
		public void Prune(Vector2Int center, int radius)
		{
			_toRemove.Clear();
			foreach (var kv in _cells)
				if (Mathf.Max(Mathf.Abs(kv.Key.x - center.x), Mathf.Abs(kv.Key.y - center.y)) > radius)
					_toRemove.Add(kv.Key);
			foreach (var c in _toRemove)
				_cells.Remove(c);
		}
	}
}
