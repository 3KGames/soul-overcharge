using UnityEngine;

namespace Game.Level.Runtime.Terrain
{
	public enum DetailNoiseType
	{
		None,    // ровно: только базовая высота и ямы/холмы
		Smooth,  // плавные холмики (лес, поле)
		Billow,  // округлые бугры (снег)
		Ridged,  // острые гребни (скалы)
		Dunes,   // волны дюн по ветру (пустыня)
		Cells,   // плиты с трещинами (лава, сухая земля)
	}

	/// <summary>Расстояние до дороги. Без Unity API — можно звать из фоновых потоков.</summary>
	public static class RoadDistance
	{
		/// <summary>
		/// Расстояние от точки до края ближайшей дороги (отрицательное — точка на дороге) и высота дороги в ближайшей точке.
		/// Если ничего ближе maxDistance от осевой нет — возвращает float.MaxValue.
		/// </summary>
		public static float EdgeDistance(RoadLine[] lines, int count, float x, float z, float maxDistance, out float roadY)
		{
			float best = float.MaxValue;
			roadY = 0f;

			for (int i = 0; i < count; i++)
			{
				ref readonly RoadLine l = ref lines[i];
				float abx = l.b.x - l.a.x, abz = l.b.z - l.a.z;
				float apx = x - l.a.x, apz = z - l.a.z;
				float len2 = abx * abx + abz * abz;
				float t = len2 > 1e-8f ? (apx * abx + apz * abz) / len2 : 0f;
				if (t < 0f) t = 0f;
				else if (t > 1f) t = 1f;

				float dx = apx - abx * t, dz = apz - abz * t;
				float d2 = dx * dx + dz * dz;

				// грубый отсев по осевой (с учётом ширины)
				float reach = maxDistance + l.halfWidth;
				if (d2 > reach * reach) continue;

				float edge = (float)System.Math.Sqrt(d2) - l.halfWidth;
				if (edge < best)
				{
					best = edge;
					roadY = l.a.y + (l.b.y - l.a.y) * t;
				}
			}

			return best;
		}
	}

	// Здесь простые классы-снимки настроек. Их читают фоновые потоки, поэтому никаких вызовов Unity API в них нет.

	public sealed class StampData
	{
		public string name;
		public float[] lut;
		public float radiusMin, radiusMax;
		public float amplitudeMin, amplitudeMax;
		public float elongationMin, elongationMax;

		/// <summary>Профиль по нормированному радиусу t (0 центр, 1 край).</summary>
		public float Evaluate(float t)
		{
			if (t >= 1f) return 0f;
			if (t <= 0f) return lut[0];
			float f = t * (lut.Length - 1);
			int i = (int)f;
			float k = f - i;
			return lut[i] + (lut[i + 1] - lut[i]) * k;
		}
	}

	public sealed class PropData
	{
		public int entryIndex;
		public GameObject prefab; // только для главного потока
		public float radius;
		public float scaleMin, scaleMax;
		public bool flatten;
		public float flattenFalloff;
		public float minRoadDistance;
		public float maxRoadDistance;
		public float maxSlopeCos;
		public bool alignToNormal;
		public bool randomYaw;
		public bool faceCamera;
		public float sink;
	}

	public sealed class BiomeTerrainData
	{
		public BiomeTag tag;
		public string name;
		public Material material; // только для главного потока
		public int splatChannel;
		public float baseOffset;

		public DetailNoiseType detailType;
		public float detailAmplitude;
		public float detailFrequency;
		public int octaves;
		public float lacunarity;
		public float gain;
		public float warpStrength;
		public float windDirX, windDirZ;
		public float duneFrequency;
		public float duneSharpness;
		public float plateFrequency;
		public float crackWidth;

		public float stampDensity;
		public StampData[] stamps;
		public float[] stampCumulative;

		public float propDensity;
		public PropData[] props;
		public float[] propCumulative;

		/// <summary>Мелкий рельеф биома в точке, метры.</summary>
		public float Detail(float x, float z, int seed)
		{
			if (detailType == DetailNoiseType.None || detailAmplitude <= 0f) return 0f;

			if (warpStrength > 0f)
			{
				float wf = detailFrequency * 0.5f;
				float wx = TerrainNoise.Fbm(x * wf + 5.2f, z * wf + 1.3f, 2, 2f, 0.5f, seed + 71);
				float wz = TerrainNoise.Fbm(x * wf + 1.7f, z * wf + 9.2f, 2, 2f, 0.5f, seed + 89);
				x += wx * warpStrength;
				z += wz * warpStrength;
			}

			float fx = x * detailFrequency, fz = z * detailFrequency;
			float n;
			switch (detailType)
			{
				case DetailNoiseType.Smooth:
					n = TerrainNoise.Fbm(fx, fz, octaves, lacunarity, gain, seed);
					break;
				case DetailNoiseType.Billow:
					n = TerrainNoise.Billow(fx, fz, octaves, lacunarity, gain, seed);
					break;
				case DetailNoiseType.Ridged:
					n = TerrainNoise.Ridged(fx, fz, octaves, lacunarity, gain, seed);
					break;
				case DetailNoiseType.Dunes:
					n = Dunes(x, z, seed);
					break;
				case DetailNoiseType.Cells:
					n = Plates(x, z, seed);
					break;
				default:
					n = 0f;
					break;
			}
			return n * detailAmplitude;
		}

		float Dunes(float x, float z, int seed)
		{
			// Волны поперёк ветра, фаза и высота плавают по шуму — гребни не идеально прямые
			float along = x * windDirX + z * windDirZ;
			float phaseNoise = TerrainNoise.Fbm(x * duneFrequency * 0.35f, z * duneFrequency * 0.35f, 2, 2f, 0.5f, seed + 11);
			float phase = along * duneFrequency + phaseNoise * 1.5f;
			// |sin(pi*x)| повторяется раз в длину волны; 1 - |sin| даёт острый гребень,
			// степень делает гребень уже, а низины между дюнами шире и положе
			float s = (float)System.Math.Sin(phase * 3.14159265f);
			float crest = 1f - (s < 0f ? -s : s);
			crest = (float)System.Math.Pow(crest, duneSharpness);
			float heightMod = 0.6f + 0.4f * TerrainNoise.Fbm(x * duneFrequency * 0.2f, z * duneFrequency * 0.2f, 2, 2f, 0.5f, seed + 23);
			float ripples = TerrainNoise.Fbm(x * detailFrequency * 4f, z * detailFrequency * 4f, 2, 2f, 0.5f, seed + 37) * 0.08f;
			return (crest * 2f - 1f) * heightMod + ripples;
		}

		float Plates(float x, float z, int seed)
		{
			TerrainNoise.Cellular(x * plateFrequency, z * plateFrequency, seed + 5, out float f1, out float f2);
			// f2 - f1 ~ 0 на границе плит → трещина
			float edge = TerrainNoise.SmoothStep(0f, crackWidth, f2 - f1);
			float tilt = TerrainNoise.Fbm(x * plateFrequency * 0.5f, z * plateFrequency * 0.5f, 2, 2f, 0.5f, seed + 3) * 0.3f;
			return edge * 2f - 1f + tilt;
		}

		public int PickStamp(float r01)
		{
			return Pick(stampCumulative, r01);
		}

		public int PickProp(float r01)
		{
			return Pick(propCumulative, r01);
		}

		static int Pick(float[] cumulative, float r01)
		{
			if (cumulative == null || cumulative.Length == 0) return -1;
			float target = r01 * cumulative[cumulative.Length - 1];
			for (int i = 0; i < cumulative.Length; i++)
				if (target < cumulative[i]) return i;
			return cumulative.Length - 1;
		}
	}

	/// <summary>Отрезок осевой линии дороги в координатах сетки генератора (метры).</summary>
	public struct RoadLine
	{
		public Vector3 a, b;
		public float halfWidth;
	}

	/// <summary>Что известно о клетке сетки: биом и средняя высота земли.</summary>
	public struct CellInfo
	{
		public BiomeTag biome;
		public float height;
		public bool fromRoad;
	}

	/// <summary>Объект, который решили поставить. Координаты — в сетке генератора.</summary>
	public struct PropPlacement
	{
		public int biomeIndex;
		public int propIndex;
		public float x, z;
		public float y;
		public Vector3 normal;
		public float yaw;
		public float scale;
		public float radius;
		public bool flatten;
		public float flattenFalloff;
		public bool rejected;
	}

	/// <summary>Снимок общих настроек рельефа для расчёта чанка.</summary>
	public struct TerrainBuildSettings
	{
		public int seed;
		public float cellSize;
		public int resolution;

		public float roadClearance;
		public float roadFalloff;
		public float sinkUnderRoad;
		public float shoulderDrop;

		public float stampCellSize;
		public float stampRoadOverlap;
		public float maxStampRadius;

		public bool props;
		public float propCellSize;
		public float propRoadClearance;

		public float uvScale;

		/// <summary>Ширина затухания рельефа к краю, за которым нет подробной земли, м.</summary>
		public float edgeFade;

		/// <summary>Глубина «юбки» по краю чанка — закрывает щели между чанками разной детализации, м.</summary>
		public float skirtDepth;

		/// <summary>На каком расстоянии от осевой дорога ещё влияет хоть на что-то.</summary>
		public float influence;
	}
}
