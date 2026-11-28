using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;

namespace Game.Level.Runtime.Terrain
{
	public enum RoadShapeSource
	{
		[Tooltip("Сплайны сегмента, а выходы, до которых сплайн не доходит, — плавной кривой от входа")]
		Auto,
		[Tooltip("Только сплайны Dreamteck")]
		SplinesOnly,
		[Tooltip("Только плавные кривые от входа к каждому выходу (если сплайнов нет или они не по центру дороги)")]
		EntryToExits,
	}

	/// <summary>
	/// Форма одного сегмента дороги для рельефа: ломаные осевой линии в координатах сетки, ширина, биом.
	/// Создаётся в главном потоке, дальше не меняется — фоновые потоки читают её спокойно.
	/// </summary>
	public sealed class RoadShape
	{
		public RoadSegment segment; // null, когда сегмент уже удалён (остался «след»)
		public readonly BiomeTag biome;
		public readonly Vector2Int[] cells;
		public readonly RoadLine[] lines;
		public readonly float minX, maxX, minZ, maxZ;
		public readonly float maxHalfWidth;
		public readonly bool usedFallback;

		public bool IsGhost => segment == null;

		RoadShape(RoadSegment segment, BiomeTag biome, Vector2Int[] cells, RoadLine[] lines, bool usedFallback)
		{
			this.segment = segment;
			this.biome = biome;
			this.cells = cells;
			this.lines = lines;
			this.usedFallback = usedFallback;

			minX = minZ = float.MaxValue;
			maxX = maxZ = float.MinValue;
			foreach (var l in lines)
			{
				minX = Mathf.Min(minX, Mathf.Min(l.a.x, l.b.x));
				maxX = Mathf.Max(maxX, Mathf.Max(l.a.x, l.b.x));
				minZ = Mathf.Min(minZ, Mathf.Min(l.a.z, l.b.z));
				maxZ = Mathf.Max(maxZ, Mathf.Max(l.a.z, l.b.z));
				maxHalfWidth = Mathf.Max(maxHalfWidth, l.halfWidth);
			}
		}

		/// <summary>Средняя высота дороги внутри клетки (по точкам осевой). false — дорога через клетку не проходит.</summary>
		public bool TryGetHeightInCell(Vector2Int cell, float cellSize, out float height)
		{
			float sum = 0f;
			int count = 0;
			float x0 = (cell.x - 0.5f) * cellSize, x1 = (cell.x + 0.5f) * cellSize;
			float z0 = cell.y * cellSize, z1 = (cell.y + 1) * cellSize;
			foreach (var l in lines)
			{
				AddIfInside(l.a, x0, x1, z0, z1, ref sum, ref count);
				Vector3 mid = (l.a + l.b) * 0.5f;
				AddIfInside(mid, x0, x1, z0, z1, ref sum, ref count);
			}
			height = count > 0 ? sum / count : 0f;
			return count > 0;
		}

		public float AverageHeight()
		{
			if (lines.Length == 0) return 0f;
			float sum = 0f;
			foreach (var l in lines) sum += (l.a.y + l.b.y) * 0.5f;
			return sum / lines.Length;
		}

		static void AddIfInside(Vector3 p, float x0, float x1, float z0, float z1, ref float sum, ref int count)
		{
			if (p.x < x0 || p.x > x1 || p.z < z0 || p.z > z1) return;
			sum += p.y;
			count++;
		}

		/// <summary>Пересекается ли с прямоугольником в координатах сетки, расширенным на margin.</summary>
		public bool Overlaps(float x0, float x1, float z0, float z1, float margin)
		{
			return maxX + maxHalfWidth + margin >= x0 && minX - maxHalfWidth - margin <= x1
			       && maxZ + maxHalfWidth + margin >= z0 && minZ - maxHalfWidth - margin <= z1;
		}

		// ---------------------------------------------------------------- построение

		public struct BuildSettings
		{
			public RoadShapeSource source;
			public float laneWidth;
			public float shoulder;
			public float lateralOffset;
			public float surfaceOffset;
			public int splineSamples;
		}

		static readonly HashSet<string> WarnedPrefabs = new HashSet<string>();
		static readonly List<Vector3> Points = new List<Vector3>();

		public static RoadShape Build(in RoadSegmentInfo info, RoadGenerator grid, in BuildSettings s)
		{
			var seg = info.segment;
			var lines = new List<RoadLine>();
			bool fallback = false;

			int lanes = seg.entryPoint != null ? seg.entryPoint.NumLanes : 1;
			if (seg.exitPoints != null)
				foreach (var p in seg.exitPoints)
					if (p != null) lanes = Mathf.Max(lanes, p.NumLanes);
			float halfWidth = Mathf.Max(1, lanes) * s.laneWidth * 0.5f + s.shoulder;

			var exits = CollectExitPoints(seg);
			var covered = new bool[exits.Count];
			float tolerance = halfWidth + 3f;

			if (s.source != RoadShapeSource.EntryToExits)
			{
				var splines = seg.GetComponentsInChildren<SplineComputer>(true);
				foreach (var spline in splines)
				{
					if (!SampleSpline(spline, grid, s, Points)) continue;

					if (!LooksAttached(Points, seg, exits, grid, tolerance))
					{
						WarnOnce(seg, $"сплайн {spline.name} не проходит рядом с точками входа/выхода — он пропущен. " +
						              "Проверьте, что сплайн идёт по центру дороги, или поставьте Road Shape Source = EntryToExits");
						continue;
					}

					AddPolyline(lines, Points, halfWidth);
					for (int i = 0; i < exits.Count; i++)
						if (!covered[i] && MinDistance(Points, grid.WorldToGrid(exits[i].transform.position)) <= tolerance)
							covered[i] = true;
				}
			}

			if (s.source != RoadShapeSource.SplinesOnly && seg.entryPoint != null)
			{
				for (int i = 0; i < exits.Count; i++)
				{
					if (covered[i]) continue;
					if (s.source == RoadShapeSource.Auto) fallback = true;
					HermitePoints(seg.entryPoint.transform, exits[i].transform, grid, s, Points);
					AddPolyline(lines, Points, halfWidth);
				}
			}

			if (lines.Count == 0)
				WarnOnce(seg, "не удалось получить форму дороги — рельеф не будет под неё подстраиваться. Проверьте entryPoint/exitPoints и сплайны");

			return new RoadShape(seg, info.biome, info.cells, lines.ToArray(), fallback);
		}

		static List<ConnectionPoint> CollectExitPoints(RoadSegment seg)
		{
			var list = new List<ConnectionPoint>();
			if (seg.exitPoints != null)
				foreach (var p in seg.exitPoints)
					if (p != null) list.Add(p);
			if (list.Count == 0 && seg.exitPoint != null)
				list.Add(seg.exitPoint);
			return list;
		}

		static bool SampleSpline(SplineComputer spline, RoadGenerator grid, in BuildSettings s, List<Vector3> result)
		{
			result.Clear();
			if (spline == null) return false;

			// Сегмент только что передвинули: пересчитать точки сплайна сейчас, а не в его Update
			spline.RebuildImmediate();

			int n = Mathf.Clamp(s.splineSamples, 4, 256);
			for (int i = 0; i < n; i++)
			{
				double t = i / (double)(n - 1);
				result.Add(grid.WorldToGrid(spline.EvaluatePosition(t)));
			}

			// Все точки в одном месте — сплайн пустой
			if ((result[0] - result[n - 1]).sqrMagnitude < 0.01f && (result[0] - result[n / 2]).sqrMagnitude < 0.01f)
				return false;

			ApplyOffsets(result, s);
			return true;
		}

		static void HermitePoints(Transform entry, Transform exit, RoadGenerator grid, in BuildSettings s, List<Vector3> result)
		{
			result.Clear();
			Vector3 p0 = grid.WorldToGrid(entry.position);
			Vector3 p1 = grid.WorldToGrid(exit.position);
			Vector3 d0 = Quaternion.Inverse(grid.GridRotation) * entry.forward;
			Vector3 d1 = Quaternion.Inverse(grid.GridRotation) * exit.forward;
			d0.y = 0f;
			d1.y = 0f;
			d0.Normalize();
			d1.Normalize();

			// 1.17 × хорда — так четверть окружности получается почти ровной дугой
			float chord = Vector3.Distance(new Vector3(p0.x, 0f, p0.z), new Vector3(p1.x, 0f, p1.z));
			Vector3 m0 = d0 * (chord * 1.17f);
			Vector3 m1 = d1 * (chord * 1.17f);

			int n = Mathf.Clamp(s.splineSamples, 4, 256);
			for (int i = 0; i < n; i++)
			{
				float t = i / (float)(n - 1);
				float t2 = t * t, t3 = t2 * t;
				float h00 = 2f * t3 - 3f * t2 + 1f;
				float h10 = t3 - 2f * t2 + t;
				float h01 = -2f * t3 + 3f * t2;
				float h11 = t3 - t2;
				Vector3 p = h00 * p0 + h10 * m0 + h01 * p1 + h11 * m1;
				// высоту ведём плавно от входа к выходу, без выбросов от касательных
				p.y = Mathf.Lerp(p0.y, p1.y, t2 * (3f - 2f * t));
				result.Add(p);
			}

			ApplyOffsets(result, s);
		}

		static void ApplyOffsets(List<Vector3> pts, in BuildSettings s)
		{
			if (Mathf.Abs(s.lateralOffset) < 0.0001f && Mathf.Abs(s.surfaceOffset) < 0.0001f) return;

			var copy = pts.ToArray();
			for (int i = 0; i < copy.Length; i++)
			{
				Vector3 prev = copy[Mathf.Max(0, i - 1)];
				Vector3 next = copy[Mathf.Min(copy.Length - 1, i + 1)];
				Vector3 tangent = next - prev;
				tangent.y = 0f;
				Vector3 right = tangent.sqrMagnitude > 1e-6f ? new Vector3(tangent.z, 0f, -tangent.x).normalized : Vector3.zero;
				pts[i] = copy[i] + right * s.lateralOffset + Vector3.up * s.surfaceOffset;
			}
		}

		static bool LooksAttached(List<Vector3> pts, RoadSegment seg, List<ConnectionPoint> exits, RoadGenerator grid, float tolerance)
		{
			if (seg.entryPoint != null && MinDistance(pts, grid.WorldToGrid(seg.entryPoint.transform.position)) <= tolerance)
				return true;
			foreach (var e in exits)
				if (MinDistance(pts, grid.WorldToGrid(e.transform.position)) <= tolerance)
					return true;
			return false;
		}

		static float MinDistance(List<Vector3> pts, Vector3 p)
		{
			float best = float.MaxValue;
			foreach (var q in pts)
			{
				float dx = q.x - p.x, dz = q.z - p.z;
				best = Mathf.Min(best, dx * dx + dz * dz);
			}
			return Mathf.Sqrt(best);
		}

		static void AddPolyline(List<RoadLine> lines, List<Vector3> pts, float halfWidth)
		{
			for (int i = 0; i + 1 < pts.Count; i++)
				lines.Add(new RoadLine { a = pts[i], b = pts[i + 1], halfWidth = halfWidth });
		}

		static void WarnOnce(RoadSegment seg, string message)
		{
			string key = seg.name.Replace("(Clone)", "").Trim() + "|" + message;
			if (WarnedPrefabs.Add(key))
				Debug.LogWarning($"[TerrainStreamer] {seg.name}: {message}", seg);
		}
	}
}
