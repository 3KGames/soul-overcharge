using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Level.Runtime.Terrain
{
	/// <summary>
	/// Всё, что нужно для расчёта одного чанка, и результат. Вход собирает главный поток,
	/// расчёт может идти в фоне, результат применяет главный поток.
	/// </summary>
	public sealed class ChunkJob
	{
		// ---- вход
		public Vector2Int coord;
		public TerrainBuildSettings s;
		/// <summary>Клетки 3×3 вокруг чанка, индекс (dx + 1) + (dz + 1) * 3.</summary>
		public readonly CellInfo[] cells = new CellInfo[9];
		public RoadLine[] lines = Array.Empty<RoadLine>();
		public int lineCount;
		/// <summary>Настройки биома по (int)BiomeTag. null — берётся fallback.</summary>
		public BiomeTerrainData[] biomeByTag;
		public BiomeTerrainData fallback;
		/// <summary>Дальний плоский чанк: без мелкого рельефа, ям, холмов и объектов.</summary>
		public bool flat;
		/// <summary>Клетки 3×3 вокруг: true — там подробная земля. По краю к остальным рельеф плавно гаснет.</summary>
		public readonly bool[] detailed = new bool[9];

		// ---- выход
		public float[] heights;   // (res + 3)², с рамкой в одну вершину для нормалей
		public Vector3[] vertices;
		public Vector3[] normals;
		public Color[] colors;
		public Vector2[] uvs;
		public readonly List<PropPlacement> props = new List<PropPlacement>();
		public BiomeTerrainData dominant;
		public float minY, maxY;
		public Exception error;

		public int Resolution => s.resolution;
		public float Step => s.cellSize / s.resolution;
		public float OriginX => (coord.x - 0.5f) * s.cellSize;
		public float OriginZ => coord.y * s.cellSize;

		/// <summary>Высота по координатам сетки внутри чанка (билинейно). Для главного потока после расчёта.</summary>
		public float SampleHeight(float x, float z)
		{
			int res = s.resolution;
			int stride = res + 3;
			float step = Step;
			float fx = Mathf.Clamp((x - OriginX) / step, 0f, res);
			float fz = Mathf.Clamp((z - OriginZ) / step, 0f, res);
			int ix = Mathf.Min((int)fx, res - 1), iz = Mathf.Min((int)fz, res - 1);
			float tx = fx - ix, tz = fz - iz;
			int i00 = (ix + 1) + (iz + 1) * stride;
			float h00 = heights[i00], h10 = heights[i00 + 1];
			float h01 = heights[i00 + stride], h11 = heights[i00 + stride + 1];
			return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
		}
	}

	/// <summary>
	/// Расчёт рельефа чанка. Чистая функция от ChunkJob — без Unity API, можно звать из фонового потока.
	///
	/// Высота точки:
	///   база     = высоты клеток (билинейно) + сдвиг биома
	///   природа  = база + (мелкий шум + ямы/холмы) × маска объектов
	///   у дороги = плавно от «чуть ниже дороги» к природе: 0 на дороге и в отступе, 1 после перехода
	/// </summary>
	public static class TerrainChunkCompute
	{
		const int SaltDetail = 1, SaltStamp = 2, SaltProp = 3;

		struct StampInstance
		{
			public float cx, cz;
			public float cos, sin;
			public float invA, invB;
			public float amplitude;
			public float reach;
			public StampData data;
		}

		/// <summary>Всё временное для одного расчёта. Отдельный объект на поток — без общих буферов.</summary>
		sealed class Context
		{
			public ChunkJob job;
			public BiomeTerrainData[] slots = new BiomeTerrainData[9];
			public int[] slotOfCell = new int[9];
			public int slotCount;
			public float[] weights = new float[9];
			public readonly List<StampInstance> stamps = new List<StampInstance>();
			public readonly List<PropPlacement> pads = new List<PropPlacement>();
		}

		public static void Run(ChunkJob job)
		{
			try
			{
				var ctx = new Context { job = job };
				PrepareSlots(ctx);
				if (job.flat)
				{
					job.props.Clear();
				}
				else
				{
					CollectStamps(ctx);
					PlanProps(ctx);
				}
				ComputeHeights(ctx);
				FinishProps(ctx);
			}
			catch (Exception e)
			{
				job.error = e;
			}
		}

		// ------------------------------------------------------------------ биомы

		static void PrepareSlots(Context ctx)
		{
			var job = ctx.job;
			ctx.slotCount = 0;
			for (int i = 0; i < 9; i++)
			{
				var b = BiomeFor(job, job.cells[i].biome);
				int slot = -1;
				for (int k = 0; k < ctx.slotCount; k++)
					if (ctx.slots[k] == b) { slot = k; break; }
				if (slot < 0)
				{
					slot = ctx.slotCount++;
					ctx.slots[slot] = b;
				}
				ctx.slotOfCell[i] = slot;
			}
		}

		static BiomeTerrainData BiomeFor(ChunkJob job, BiomeTag tag)
		{
			int i = (int)tag;
			if (job.biomeByTag != null && i >= 0 && i < job.biomeByTag.Length && job.biomeByTag[i] != null)
				return job.biomeByTag[i];
			return job.fallback;
		}

		/// <summary>Билинейно по центрам клеток: базовая высота и доли биомов в ctx.weights.</summary>
		static float SampleCells(Context ctx, float x, float z)
		{
			var job = ctx.job;
			float cs = job.s.cellSize;
			float u = x / cs;
			float v = z / cs - 0.5f;
			int i0 = TerrainNoise.FastFloor(u);
			int j0 = TerrainNoise.FastFloor(v);
			float fu = TerrainNoise.SmoothStep01(u - i0);
			float fv = TerrainNoise.SmoothStep01(v - j0);

			for (int k = 0; k < ctx.slotCount; k++) ctx.weights[k] = 0f;

			float h = 0f;
			h += Corner(ctx, i0, j0, (1f - fu) * (1f - fv));
			h += Corner(ctx, i0 + 1, j0, fu * (1f - fv));
			h += Corner(ctx, i0, j0 + 1, (1f - fu) * fv);
			h += Corner(ctx, i0 + 1, j0 + 1, fu * fv);
			return h;
		}

		static float Corner(Context ctx, int cx, int cz, float w)
		{
			var job = ctx.job;
			int dx = Mathf.Clamp(cx - job.coord.x, -1, 1);
			int dz = Mathf.Clamp(cz - job.coord.y, -1, 1);
			int idx = (dx + 1) + (dz + 1) * 3;
			int slot = ctx.slotOfCell[idx];
			ctx.weights[slot] += w;
			return w * (job.cells[idx].height + ctx.slots[slot].baseOffset);
		}

		static BiomeTerrainData PickSlot(Context ctx, float r01)
		{
			float acc = 0f;
			for (int k = 0; k < ctx.slotCount; k++)
			{
				acc += ctx.weights[k];
				if (r01 < acc) return ctx.slots[k];
			}
			for (int k = ctx.slotCount - 1; k >= 0; k--)
				if (ctx.weights[k] > 0f) return ctx.slots[k];
			return ctx.slots[0];
		}

		// ------------------------------------------------------------------ ямы и холмы

		static void CollectStamps(Context ctx)
		{
			var job = ctx.job;
			var s = job.s;
			if (s.stampCellSize <= 0f) return;

			float step = job.Step;
			float x0 = job.OriginX - step, x1 = job.OriginX + s.cellSize + step;
			float z0 = job.OriginZ - step, z1 = job.OriginZ + s.cellSize + step;
			float sc = s.stampCellSize;
			float maxR = s.maxStampRadius;
			int seed = TerrainNoise.Salt(s.seed, SaltStamp);

			int sx0 = TerrainNoise.FastFloor((x0 - maxR) / sc), sx1 = TerrainNoise.FastFloor((x1 + maxR) / sc);
			int sz0 = TerrainNoise.FastFloor((z0 - maxR) / sc), sz1 = TerrainNoise.FastFloor((z1 + maxR) / sc);

			for (int sz = sz0; sz <= sz1; sz++)
			for (int sx = sx0; sx <= sx1; sx++)
			{
				var rng = new HashRandom(TerrainNoise.Hash(sx, sz, seed));
				float cx = (sx + rng.Next01()) * sc;
				float cz = (sz + rng.Next01()) * sc;
				float rBiome = rng.Next01();
				float rDensity = rng.Next01();
				float rStamp = rng.Next01();
				float rRadius = rng.Next01();
				float rAmp = rng.Next01();
				float rElong = rng.Next01();
				float rAngle = rng.Next01();

				SampleCells(ctx, cx, cz);
				var biome = PickSlot(ctx, rBiome);
				if (biome.stamps == null || biome.stamps.Length == 0 || rDensity >= biome.stampDensity) continue;

				int si = biome.PickStamp(rStamp);
				if (si < 0) continue;
				var data = biome.stamps[si];

				float radius = Mathf.Lerp(data.radiusMin, data.radiusMax, rRadius);
				if (cx + radius < x0 || cx - radius > x1 || cz + radius < z0 || cz - radius > z1) continue;

				// Не ставить центр слишком близко к дороге: остаток всё равно сгладится маской
				float keep = radius * (1f - s.stampRoadOverlap);
				if (keep > 0f && job.lineCount > 0)
				{
					float d = RoadDistance.EdgeDistance(job.lines, job.lineCount, cx, cz, keep, out _);
					if (d < keep) continue;
				}

				float elong = Mathf.Lerp(data.elongationMin, data.elongationMax, rElong);
				float angle = rAngle * Mathf.PI;
				ctx.stamps.Add(new StampInstance
				{
					cx = cx,
					cz = cz,
					cos = Mathf.Cos(angle),
					sin = Mathf.Sin(angle),
					invA = 1f / radius,
					invB = elong / radius,
					amplitude = Mathf.Lerp(data.amplitudeMin, data.amplitudeMax, rAmp),
					reach = radius,
					data = data,
				});
			}
		}

		static float StampsAt(Context ctx, float x, float z)
		{
			float sum = 0f;
			var list = ctx.stamps;
			for (int i = 0; i < list.Count; i++)
			{
				var st = list[i];
				float dx = x - st.cx, dz = z - st.cz;
				if (dx > st.reach || dx < -st.reach || dz > st.reach || dz < -st.reach) continue;
				float a = (dx * st.cos + dz * st.sin) * st.invA;
				float b = (-dx * st.sin + dz * st.cos) * st.invB;
				float q = a * a + b * b;
				if (q >= 1f) continue;
				sum += st.amplitude * st.data.Evaluate((float)Math.Sqrt(q));
			}
			return sum;
		}

		// ------------------------------------------------------------------ объекты (план)

		static void PlanProps(Context ctx)
		{
			var job = ctx.job;
			var s = job.s;
			job.props.Clear();
			ctx.pads.Clear();
			if (!s.props || s.propCellSize <= 0f) return;

			int n = Mathf.Max(1, Mathf.FloorToInt(s.cellSize / s.propCellSize));
			float sub = s.cellSize / n;
			float x0 = job.OriginX, z0 = job.OriginZ;
			float x1 = x0 + s.cellSize, z1 = z0 + s.cellSize;
			int seed = TerrainNoise.Salt(s.seed, SaltProp);

			for (int pj = 0; pj < n; pj++)
			for (int pi = 0; pi < n; pi++)
			{
				var rng = new HashRandom(TerrainNoise.Hash(job.coord.x * n + pi, job.coord.y * n + pj, seed));
				float x = x0 + (pi + rng.Next01()) * sub;
				float z = z0 + (pj + rng.Next01()) * sub;
				float rBiome = rng.Next01();
				float rDensity = rng.Next01();
				float rProp = rng.Next01();
				float rScale = rng.Next01();
				float rYaw = rng.Next01();

				SampleCells(ctx, x, z);
				var biome = PickSlot(ctx, rBiome);
				if (biome.props == null || biome.props.Length == 0 || rDensity >= biome.propDensity) continue;

				int pIndex = biome.PickProp(rProp);
				if (pIndex < 0) continue;
				var p = biome.props[pIndex];

				float scale = Mathf.Lerp(p.scaleMin, p.scaleMax, rScale);
				float r = p.radius * scale;

				float minEdge = s.propRoadClearance + p.minRoadDistance + r;
				float cap = Mathf.Max(minEdge, p.maxRoadDistance) + 1f;
				float d = job.lineCount > 0
					? RoadDistance.EdgeDistance(job.lines, job.lineCount, x, z, cap, out _)
					: float.MaxValue;
				if (d < minEdge) continue;
				if (p.maxRoadDistance > 0f && d > p.maxRoadDistance) continue;

				if (p.flatten)
				{
					// Площадка целиком внутри своего чанка (с запасом в шаг сетки для нормалей на краю) —
					// тогда соседям не нужно о ней знать
					float pad = r + p.flattenFalloff + job.Step;
					if (x - pad < x0 || x + pad > x1 || z - pad < z0 || z + pad > z1) continue;
				}

				bool overlaps = false;
				foreach (var other in job.props)
				{
					float dx = other.x - x, dz = other.z - z;
					float min = other.radius + r;
					if (dx * dx + dz * dz < min * min) { overlaps = true; break; }
				}
				if (overlaps) continue;

				var placement = new PropPlacement
				{
					biomeIndex = IsRegistered(job, biome) ? (int)biome.tag : -1,
					propIndex = pIndex,
					x = x,
					z = z,
					yaw = p.randomYaw ? rYaw * 360f : 0f,
					scale = scale,
					radius = r,
					flatten = p.flatten,
					flattenFalloff = p.flattenFalloff,
				};
				job.props.Add(placement);
				if (p.flatten) ctx.pads.Add(placement);
			}
		}

		static bool IsRegistered(ChunkJob job, BiomeTerrainData b)
		{
			int i = (int)b.tag;
			return job.biomeByTag != null && i >= 0 && i < job.biomeByTag.Length && job.biomeByTag[i] == b;
		}

		static float PropMask(Context ctx, float x, float z)
		{
			float m = 1f;
			var pads = ctx.pads;
			for (int i = 0; i < pads.Count; i++)
			{
				var p = pads[i];
				float dx = x - p.x, dz = z - p.z;
				float outer = p.radius + p.flattenFalloff;
				float d2 = dx * dx + dz * dz;
				if (d2 >= outer * outer) continue;
				m *= TerrainNoise.SmoothStep(p.radius, outer, (float)Math.Sqrt(d2));
			}
			return m;
		}

		/// <summary>
		/// 1 — полный рельеф, 0 — только базовая высота. Падает к клеткам, где подробной земли нет,
		/// чтобы край совпадал с дальними плоскими чанками. Зависит только от статуса соседних клеток,
		/// поэтому на общем крае двух подробных чанков значение одинаковое.
		/// </summary>
		static float EdgeFade(Context ctx, float x, float z)
		{
			var job = ctx.job;
			if (job.flat) return 0f;
			float w = job.s.edgeFade;
			if (w <= 0f) return 1f;

			float cs = job.s.cellSize;
			float best = float.MaxValue;
			for (int dz = -1; dz <= 1; dz++)
			for (int dx = -1; dx <= 1; dx++)
			{
				if (job.detailed[(dx + 1) + (dz + 1) * 3]) continue;
				float rx0 = (job.coord.x + dx - 0.5f) * cs, rx1 = rx0 + cs;
				float rz0 = (job.coord.y + dz) * cs, rz1 = rz0 + cs;
				float ddx = x < rx0 ? rx0 - x : x > rx1 ? x - rx1 : 0f;
				float ddz = z < rz0 ? rz0 - z : z > rz1 ? z - rz1 : 0f;
				float d2 = ddx * ddx + ddz * ddz;
				if (d2 < best) best = d2;
			}
			if (best == float.MaxValue) return 1f;
			return TerrainNoise.SmoothStep(0f, w, (float)Math.Sqrt(best));
		}

		// ------------------------------------------------------------------ высоты и меш

		static void ComputeHeights(Context ctx)
		{
			var job = ctx.job;
			var s = job.s;
			int res = s.resolution;
			int stride = res + 3;
			float step = job.Step;
			float ox = job.OriginX, oz = job.OriginZ;
			int detailSeed = TerrainNoise.Salt(s.seed, SaltDetail);
			float roadReach = s.roadClearance + s.roadFalloff;
			float invUv = s.uvScale > 0f ? 1f / s.uvScale : 0.1f;

			int gridCount = (res + 1) * (res + 1);
			int vCount = gridCount + 4 * (res + 1); // + юбка по 4 краям
			job.heights = new float[stride * stride];
			job.vertices = new Vector3[vCount];
			job.normals = new Vector3[vCount];
			job.colors = new Color[vCount];
			job.uvs = new Vector2[vCount];

			var slotTotals = new float[9];
			float minY = float.MaxValue, maxY = float.MinValue;

			for (int vz = -1; vz <= res + 1; vz++)
			for (int vx = -1; vx <= res + 1; vx++)
			{
				float x = ox + vx * step;
				float z = oz + vz * step;

				float baseH = SampleCells(ctx, x, z);

				float natural = baseH;
				float fade = EdgeFade(ctx, x, z);
				if (fade > 0f)
				{
					float detail = 0f;
					for (int k = 0; k < ctx.slotCount; k++)
					{
						float w = ctx.weights[k];
						if (w > 0.001f) detail += w * ctx.slots[k].Detail(x, z, detailSeed);
					}
					natural += (detail + StampsAt(ctx, x, z)) * PropMask(ctx, x, z) * fade;
				}
				float h = natural;

				if (job.lineCount > 0)
				{
					float d = RoadDistance.EdgeDistance(job.lines, job.lineCount, x, z, roadReach, out float roadY);
					if (d < roadReach)
					{
						float k = s.roadClearance > 0f ? Mathf.Clamp01(d / s.roadClearance) : 1f;
						float target = roadY - Mathf.Lerp(s.sinkUnderRoad, s.shoulderDrop, k);
						float m = TerrainNoise.SmoothStep(s.roadClearance, roadReach, d);
						h = target + (natural - target) * m;
					}
				}

				job.heights[(vx + 1) + (vz + 1) * stride] = h;

				bool inside = vx >= 0 && vz >= 0 && vx <= res && vz <= res;
				if (!inside) continue;

				int vi = vx + vz * (res + 1);
				job.vertices[vi] = new Vector3(vx * step, h, vz * step);
				job.uvs[vi] = new Vector2(x * invUv, z * invUv);

				float r = 0f, g = 0f, b = 0f, a = 0f;
				for (int k = 0; k < ctx.slotCount; k++)
				{
					float w = ctx.weights[k];
					slotTotals[k] += w;
					switch (ctx.slots[k].splatChannel)
					{
						case 0: r += w; break;
						case 1: g += w; break;
						case 2: b += w; break;
						default: a += w; break;
					}
				}
				job.colors[vi] = new Color(r, g, b, a);

				if (h < minY) minY = h;
				if (h > maxY) maxY = h;
			}

			// Нормали по соседям из рамки — одинаковые на краях соседних чанков
			for (int vz = 0; vz <= res; vz++)
			for (int vx = 0; vx <= res; vx++)
			{
				int c = (vx + 1) + (vz + 1) * stride;
				float hl = job.heights[c - 1], hr = job.heights[c + 1];
				float hd = job.heights[c - stride], hu = job.heights[c + stride];
				var n = new Vector3(hl - hr, 2f * step, hd - hu);
				job.normals[vx + vz * (res + 1)] = n.normalized;
			}

			// Юбка: копия краёв, опущенная вниз. Закрывает щели между чанками разной детализации
			int row = res + 1;
			float skirt = job.s.skirtDepth;
			for (int side = 0; side < 4; side++)
			for (int i = 0; i <= res; i++)
			{
				int src = side == 0 ? i                 // низ (z = 0)
					: side == 1 ? i + res * row          // верх (z = res)
					: side == 2 ? i * row                // лево (x = 0)
					: res + i * row;                     // право (x = res)
				int dst = gridCount + side * row + i;
				var v = job.vertices[src];
				job.vertices[dst] = new Vector3(v.x, v.y - skirt, v.z);
				job.normals[dst] = job.normals[src];
				job.colors[dst] = job.colors[src];
				job.uvs[dst] = job.uvs[src];
			}
			minY -= skirt;

			int best = 0;
			for (int k = 1; k < ctx.slotCount; k++)
				if (slotTotals[k] > slotTotals[best]) best = k;
			job.dominant = ctx.slots[best];
			job.minY = minY;
			job.maxY = maxY;
		}

		// ------------------------------------------------------------------ объекты (высота и уклон)

		static void FinishProps(Context ctx)
		{
			var job = ctx.job;
			int res = job.s.resolution;
			int stride = res + 3;
			float step = job.Step;

			for (int i = 0; i < job.props.Count; i++)
			{
				var p = job.props[i];
				var data = PropDataFor(job, p);
				if (data == null) { p.rejected = true; job.props[i] = p; continue; }

				p.y = job.SampleHeight(p.x, p.z);

				int ix = Mathf.Clamp(Mathf.RoundToInt((p.x - job.OriginX) / step), 0, res);
				int iz = Mathf.Clamp(Mathf.RoundToInt((p.z - job.OriginZ) / step), 0, res);
				// уклон по пятну размером с объект, а не по одной вершине
				int span = Mathf.Clamp(Mathf.CeilToInt(p.radius / step), 1, 4);
				int xl = Mathf.Max(ix - span, -1), xr = Mathf.Min(ix + span, res + 1);
				int zd = Mathf.Max(iz - span, -1), zu = Mathf.Min(iz + span, res + 1);
				float hl = job.heights[(xl + 1) + (iz + 1) * stride], hr = job.heights[(xr + 1) + (iz + 1) * stride];
				float hd = job.heights[(ix + 1) + (zd + 1) * stride], hu = job.heights[(ix + 1) + (zu + 1) * stride];
				var n = new Vector3((hl - hr) / ((xr - xl) * step), 1f, (hd - hu) / ((zu - zd) * step)).normalized;
				p.normal = n;

				if (n.y < data.maxSlopeCos) p.rejected = true;
				p.y -= data.sink;
				job.props[i] = p;
			}
		}

		public static PropData PropDataFor(ChunkJob job, in PropPlacement p)
		{
			var biome = p.biomeIndex >= 0 && job.biomeByTag != null && p.biomeIndex < job.biomeByTag.Length && job.biomeByTag[p.biomeIndex] != null
				? job.biomeByTag[p.biomeIndex]
				: job.fallback;
			if (biome == null || biome.props == null || p.propIndex < 0 || p.propIndex >= biome.props.Length) return null;
			return biome.props[p.propIndex];
		}
	}
}
