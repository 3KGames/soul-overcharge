using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Level.Runtime.Terrain
{
	[Serializable]
	public class StampEntry
	{
		public TerrainStampSO stamp;
		[Min(0f)] public float weight = 1f;
	}

	[Serializable]
	public class PropEntry
	{
		public GameObject prefab;
		[Min(0f)] public float weight = 1f;

		[Tooltip("Радиус занимаемого места при масштабе 1, м. Объекты не ставятся друг на друга")]
		[Min(0.1f)] public float radius = 1f;

		[Tooltip("Разброс масштаба (одинаковый по всем осям)")]
		public Vector2 scale = new Vector2(0.9f, 1.2f);

		[Tooltip("Выровнять землю под объектом: ямы, холмы и мелкий шум под ним убираются. Для домов, вышек, больших камней")]
		public bool flattenGround;

		[Tooltip("Ширина плавного перехода от ровной площадки к рельефу, м")]
		[Min(0f)] public float flattenFalloff = 3f;

		[Tooltip("Дополнительный отступ от края дороги, м (к общему propRoadClearance)")]
		[Min(0f)] public float minRoadDistance = 0f;

		[Tooltip("Не дальше от края дороги, м. 0 — без ограничения (столбы, отбойники, знаки ставьте с ограничением)")]
		[Min(0f)] public float maxRoadDistance = 0f;

		[Tooltip("Максимальный уклон земли, градусы. Круче — объект не ставится")]
		[Range(0f, 90f)] public float maxSlope = 35f;

		[Tooltip("Наклонить объект по склону")]
		public bool alignToNormal;

		[Tooltip("Случайный поворот вокруг вертикали")]
		public bool randomYaw = true;

		[Tooltip("Всегда поворачиваться к камере (для плоских спрайтов, иначе на поворотах дороги их видно с ребра)")]
		public bool faceCamera;

		[Tooltip("На сколько утопить в землю, м")]
		public float sink = 0.1f;
	}

	/// <summary>
	/// Как выглядит земля в одном биоме дороги. Связь с биомом — по biomeTag.
	/// </summary>
	[CreateAssetMenu(fileName = "BiomeTerrain_", menuName = "Road Generator/Terrain/Biome Terrain")]
	public class BiomeTerrainSO : ScriptableObject
	{
		[Tooltip("Тот же тег, что у биома в RoadGenerator")]
		public BiomeTag biomeTag = BiomeTag.None;

		[Header("Внешний вид")]
		[Tooltip("Материал чанка, где этот биом преобладает")]
		public Material material;

		[Tooltip("Канал цвета вершин (0 R, 1 G, 2 B, 3 A), в который пишется доля биома. Для шейдера со смешиванием текстур")]
		[Range(0, 3)] public int splatChannel;

		[Header("Базовая высота")]
		[Tooltip("Сдвиг всей земли биома вверх или вниз относительно высоты дороги, м")]
		public float baseOffset = 0f;

		[Header("Мелкий рельеф")]
		public DetailNoiseType detailType = DetailNoiseType.Smooth;

		[Tooltip("Размах мелкого рельефа, м (от -A до +A)")]
		[Min(0f)] public float detailAmplitude = 1.5f;

		[Tooltip("Размер одного бугра, м. Больше — более пологий рельеф")]
		[Min(1f)] public float detailScale = 40f;

		[Range(1, 6)] public int octaves = 4;
		[Range(1.5f, 3f)] public float lacunarity = 2f;
		[Range(0.2f, 0.8f)] public float gain = 0.5f;

		[Tooltip("Искривление шума, м. 0 — выкл. 5–15 — рельеф выглядит естественнее")]
		[Min(0f)] public float warpStrength = 0f;

		[Header("Дюны (если тип Dunes)")]
		[Tooltip("Направление ветра, градусы")]
		public float windAngle = 30f;
		[Tooltip("Расстояние между гребнями, м")]
		[Min(2f)] public float duneWavelength = 35f;
		[Tooltip("Острота гребня: 1 — широкие дюны, 3 — узкие острые гребни с плоскими низинами")]
		[Range(1f, 4f)] public float duneSharpness = 2f;

		[Header("Плиты (если тип Cells)")]
		[Tooltip("Размер плиты, м")]
		[Min(2f)] public float plateSize = 12f;
		[Tooltip("Ширина трещины, доля плиты")]
		[Range(0.02f, 0.5f)] public float crackWidth = 0.12f;

		[Header("Ямы и холмы")]
		[Tooltip("Шанс, что в ячейке сетки штампов (stampCellSize у TerrainStreamer) появится яма или холм")]
		[Range(0f, 1f)] public float stampDensity = 0.35f;
		public List<StampEntry> stamps = new List<StampEntry>();

		[Header("Объекты вокруг дороги")]
		[Tooltip("Шанс объекта в ячейке сетки объектов (propCellSize у TerrainStreamer)")]
		[Range(0f, 1f)] public float propDensity = 0.3f;
		public List<PropEntry> props = new List<PropEntry>();

#if UNITY_EDITOR
		void OnValidate() => TerrainAssetEvents.Raise();
#endif

		internal BiomeTerrainData Bake(float maxStampRadius)
		{
			var data = new BiomeTerrainData
			{
				tag = biomeTag,
				name = name,
				material = material,
				splatChannel = Mathf.Clamp(splatChannel, 0, 3),
				baseOffset = baseOffset,
				detailType = detailType,
				detailAmplitude = detailAmplitude,
				detailFrequency = 1f / Mathf.Max(1f, detailScale),
				octaves = Mathf.Clamp(octaves, 1, 6),
				lacunarity = lacunarity,
				gain = gain,
				warpStrength = warpStrength,
				windDirX = Mathf.Cos(windAngle * Mathf.Deg2Rad),
				windDirZ = Mathf.Sin(windAngle * Mathf.Deg2Rad),
				duneFrequency = 1f / Mathf.Max(2f, duneWavelength),
				duneSharpness = duneSharpness,
				plateFrequency = 1f / Mathf.Max(2f, plateSize),
				crackWidth = crackWidth,
				stampDensity = stampDensity,
				propDensity = propDensity,
			};

			var stampList = new List<StampData>();
			var stampWeights = new List<float>();
			if (stamps != null)
				foreach (var s in stamps)
					if (s != null && s.stamp != null && s.weight > 0f)
					{
						stampList.Add(s.stamp.Bake(maxStampRadius));
						stampWeights.Add(s.weight);
					}
			data.stamps = stampList.ToArray();
			data.stampCumulative = Cumulative(stampWeights);

			var propList = new List<PropData>();
			var propWeights = new List<float>();
			if (props != null)
				for (int i = 0; i < props.Count; i++)
				{
					var p = props[i];
					if (p == null || p.prefab == null || p.weight <= 0f) continue;
					propList.Add(new PropData
					{
						entryIndex = i,
						prefab = p.prefab,
						radius = Mathf.Max(0.1f, p.radius),
						scaleMin = Mathf.Max(0.01f, Mathf.Min(p.scale.x, p.scale.y)),
						scaleMax = Mathf.Max(0.01f, Mathf.Max(p.scale.x, p.scale.y)),
						flatten = p.flattenGround,
						flattenFalloff = Mathf.Max(0f, p.flattenFalloff),
						minRoadDistance = p.minRoadDistance,
						maxRoadDistance = p.maxRoadDistance,
						maxSlopeCos = Mathf.Cos(p.maxSlope * Mathf.Deg2Rad),
						alignToNormal = p.alignToNormal,
						randomYaw = p.randomYaw,
						faceCamera = p.faceCamera,
						sink = p.sink,
					});
					propWeights.Add(p.weight);
				}
			data.props = propList.ToArray();
			data.propCumulative = Cumulative(propWeights);
			return data;
		}

		static float[] Cumulative(List<float> weights)
		{
			var c = new float[weights.Count];
			float sum = 0f;
			for (int i = 0; i < weights.Count; i++)
			{
				sum += weights[i];
				c[i] = sum;
			}
			return c;
		}
	}
}
