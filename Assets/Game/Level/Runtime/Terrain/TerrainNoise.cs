namespace Game.Level.Runtime.Terrain
{
	/// <summary>
	/// Детерминированный шум и хэши. Без Unity API, поэтому можно звать из фоновых потоков.
	/// Одинаковые координаты и seed всегда дают одинаковый результат — на этом держатся швы между чанками.
	/// </summary>
	public static class TerrainNoise
	{
		public static uint Hash(int x, int y, int seed)
		{
			unchecked
			{
				uint h = (uint)seed * 0x9E3779B9u + 0x7F4A7C15u;
				h ^= (uint)x * 0x85EBCA6Bu;
				h = (h << 13) | (h >> 19);
				h *= 0xC2B2AE35u;
				h ^= (uint)y * 0x27D4EB2Fu;
				h = (h << 15) | (h >> 17);
				h *= 0x165667B1u;
				h ^= h >> 16;
				h *= 0x85EBCA6Bu;
				h ^= h >> 13;
				h *= 0xC2B2AE35u;
				h ^= h >> 16;
				return h;
			}
		}

		public static int Salt(int seed, int salt)
		{
			unchecked { return seed ^ (salt * 0x632BE5AB); }
		}

		public static int FastFloor(float v)
		{
			int i = (int)v;
			return v < i ? i - 1 : i;
		}

		static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

		static float Grad(uint h, float x, float y)
		{
			switch (h & 7u)
			{
				case 0: return x;
				case 1: return -x;
				case 2: return y;
				case 3: return -y;
				case 4: return (x + y) * 0.70710678f;
				case 5: return (-x + y) * 0.70710678f;
				case 6: return (x - y) * 0.70710678f;
				default: return (-x - y) * 0.70710678f;
			}
		}

		/// <summary>Градиентный шум, примерно -1..1, период решётки = 1.</summary>
		public static float Gradient(float x, float y, int seed)
		{
			int x0 = FastFloor(x), y0 = FastFloor(y);
			float fx = x - x0, fy = y - y0;
			float u = Fade(fx), v = Fade(fy);

			float n00 = Grad(Hash(x0, y0, seed), fx, fy);
			float n10 = Grad(Hash(x0 + 1, y0, seed), fx - 1f, fy);
			float n01 = Grad(Hash(x0, y0 + 1, seed), fx, fy - 1f);
			float n11 = Grad(Hash(x0 + 1, y0 + 1, seed), fx - 1f, fy - 1f);

			float a = n00 + (n10 - n00) * u;
			float b = n01 + (n11 - n01) * u;
			return (a + (b - a) * v) * 1.41421356f;
		}

		/// <summary>Сумма октав, -1..1. Плавные холмики.</summary>
		public static float Fbm(float x, float y, int octaves, float lacunarity, float gain, int seed)
		{
			float sum = 0f, amp = 1f, norm = 0f, f = 1f;
			for (int o = 0; o < octaves; o++)
			{
				sum += Gradient(x * f, y * f, seed + o * 1013) * amp;
				norm += amp;
				amp *= gain;
				f *= lacunarity;
			}
			return norm > 0f ? sum / norm : 0f;
		}

		/// <summary>Округлые бугры (сугробы), -1..1.</summary>
		public static float Billow(float x, float y, int octaves, float lacunarity, float gain, int seed)
		{
			float sum = 0f, amp = 1f, norm = 0f, f = 1f;
			for (int o = 0; o < octaves; o++)
			{
				float n = Gradient(x * f, y * f, seed + o * 1013);
				sum += ((n < 0f ? -n : n) * 2f - 1f) * amp;
				norm += amp;
				amp *= gain;
				f *= lacunarity;
			}
			return norm > 0f ? sum / norm : 0f;
		}

		/// <summary>Острые гребни (скалы), -1..1.</summary>
		public static float Ridged(float x, float y, int octaves, float lacunarity, float gain, int seed)
		{
			float sum = 0f, amp = 1f, norm = 0f, f = 1f;
			for (int o = 0; o < octaves; o++)
			{
				float n = Gradient(x * f, y * f, seed + o * 1013);
				float r = 1f - (n < 0f ? -n : n);
				sum += (r * r * 2f - 1f) * amp;
				norm += amp;
				amp *= gain;
				f *= lacunarity;
			}
			return norm > 0f ? sum / norm : 0f;
		}

		/// <summary>Клеточный шум: расстояние до ближайшей (f1) и второй (f2) точки, в долях клетки.</summary>
		public static void Cellular(float x, float y, int seed, out float f1, out float f2)
		{
			int cx = FastFloor(x), cy = FastFloor(y);
			f1 = 9f;
			f2 = 9f;
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				int gx = cx + dx, gy = cy + dy;
				uint h = Hash(gx, gy, seed);
				float px = gx + Rand01(h);
				float py = gy + Rand01(h * 747796405u + 2891336453u);
				float ddx = px - x, ddy = py - y;
				float d = (float)System.Math.Sqrt(ddx * ddx + ddy * ddy);
				if (d < f1) { f2 = f1; f1 = d; }
				else if (d < f2) f2 = d;
			}
		}

		public static float Rand01(uint h) => (h >> 8) * (1f / 16777216f);

		public static float SmoothStep01(float t)
		{
			if (t <= 0f) return 0f;
			if (t >= 1f) return 1f;
			return t * t * (3f - 2f * t);
		}

		public static float SmoothStep(float a, float b, float v)
		{
			if (b <= a) return v < a ? 0f : 1f;
			return SmoothStep01((v - a) / (b - a));
		}
	}

	/// <summary>Маленький генератор случайных чисел из хэша. Детерминированный, без общего состояния.</summary>
	public struct HashRandom
	{
		uint _state;

		public HashRandom(uint seed)
		{
			_state = seed == 0 ? 0x6D2B79F5u : seed;
		}

		public uint NextUInt()
		{
			unchecked
			{
				_state = _state * 747796405u + 2891336453u;
				uint word = ((_state >> (int)((_state >> 28) + 4u)) ^ _state) * 277803737u;
				return (word >> 22) ^ word;
			}
		}

		/// <summary>0..1 (1 не включается).</summary>
		public float Next01() => (NextUInt() >> 8) * (1f / 16777216f);

		public float Range(float a, float b) => a + (b - a) * Next01();
	}
}
