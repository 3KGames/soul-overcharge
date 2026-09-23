using System;
using UnityEngine;

namespace Game.Level.Runtime
{
	public enum EnemyTag
	{
		None,
		Tower,
		Biker,
		Bomber,
		SoulPickup,
		SoulVOZ,
	}

	[Serializable]
	public class EnemySpawnEntry
	{
		[Tooltip("Уникальный тег врага — совпадает с allowedEnemyTags в точках спавна")]
		public EnemyTag enemyTag = EnemyTag.None;

		[Tooltip("Префаб врага")]
		public GameObject prefab;

		[Tooltip("Сколько штук заспавнить на сегменте")]
		[Range(1, 100)]
		public int spawnCount = 1;

		[Tooltip("Шанс что этот тип врага появится на сегменте (0–1)")]
		[Range(0f, 1f)]
		public float spawnChance = 1f;
	}
}