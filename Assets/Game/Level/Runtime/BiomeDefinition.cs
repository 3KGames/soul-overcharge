using System;
using UnityEngine;

namespace Game.Level.Runtime
{
	public enum BiomeTag
	{
		None,
		Desert,
		Forest,
		Snow,
		Lava,
	}

	[Serializable]
	public class RoadBiomeDefinition
	{
		public BiomeTag biomeTag = BiomeTag.None;

		[Range(0f, 10f)]
		public float weight = 1f;

		[Range(0f, 1f)]
		public float exitChance = 0.1f;

		[Header("Веса типов сегментов внутри этого биома")]
		[Range(0, 10)] public int weightStraight = 5;
		[Range(0, 10)] public int weightTurnLeft = 2;
		[Range(0, 10)] public int weightTurnRight = 2;
		[Range(0, 10)] public int weightHillUp = 1;
		[Range(0, 10)] public int weightHillDown = 1;
		[Tooltip("0 — развилок в этом биоме нет")]
		[Range(0, 10)] public int weightFork = 0;

		[Header("Префабы сегментов этого биома")]
		[Tooltip("Обязательно: однаклеточная прямая на каждое число полос, которое встречается на выходах")]
		public GameObject[] straightPrefabs;
		public GameObject[] turnLeftPrefabs;
		public GameObject[] turnRightPrefabs;
		public GameObject[] hillUpPrefabs;
		public GameObject[] hillDownPrefabs;
		[Tooltip("Сегменты с 2+ выходами")]
		public GameObject[] forkPrefabs;
	}
}
