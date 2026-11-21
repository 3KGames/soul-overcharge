using UnityEngine;

namespace Game.Level.Runtime.Terrain
{
	/// <summary>
	/// Одна форма ямы или холма. Профиль по радиусу: X = 0 центр, X = 1 край.
	/// Y = множитель высоты: -1 — дно ямы, +1 — вершина холма. На краю должен быть 0, иначе будет ступенька.
	/// </summary>
	[CreateAssetMenu(fileName = "Stamp_", menuName = "Road Generator/Terrain/Stamp (яма или холм)")]
	public class TerrainStampSO : ScriptableObject
	{
		[Tooltip("Профиль по радиусу: X 0 = центр, 1 = край. Y: -1 яма, +1 холм. В X = 1 должно быть 0")]
		public AnimationCurve profile = HillCurve();

		[Tooltip("Радиус в метрах, выбирается случайно в диапазоне. Больше половины клетки дороги нельзя — урежется")]
		public Vector2 radius = new Vector2(6f, 12f);

		[Tooltip("Высота (для холма) или глубина (для ямы) в метрах, случайно в диапазоне")]
		public Vector2 amplitude = new Vector2(1.5f, 3f);

		[Tooltip("Вытянутость: 1 — круг, 2 — овал в два раза уже по одной оси")]
		public Vector2 elongation = new Vector2(1f, 1.5f);

		const int LutSize = 64;

		internal StampData Bake(float maxRadius)
		{
			var lut = new float[LutSize + 1];
			var curve = profile ?? HillCurve();
			for (int i = 0; i <= LutSize; i++)
				lut[i] = curve.Evaluate(i / (float)LutSize);

			// край принудительно в ноль, чтобы не было ступеньки
			lut[LutSize] = 0f;

			float rMin = Mathf.Clamp(Mathf.Min(radius.x, radius.y), 0.5f, maxRadius);
			float rMax = Mathf.Clamp(Mathf.Max(radius.x, radius.y), 0.5f, maxRadius);
			float eMin = Mathf.Max(1f, Mathf.Min(elongation.x, elongation.y));
			float eMax = Mathf.Max(eMin, Mathf.Max(elongation.x, elongation.y));

			return new StampData
			{
				name = name,
				lut = lut,
				radiusMin = rMin,
				radiusMax = rMax,
				amplitudeMin = Mathf.Min(amplitude.x, amplitude.y),
				amplitudeMax = Mathf.Max(amplitude.x, amplitude.y),
				elongationMin = eMin,
				elongationMax = eMax,
			};
		}

		void OnValidate()
		{
#if UNITY_EDITOR
			TerrainAssetEvents.Raise();
#endif
			if (profile != null && profile.length > 0 && Mathf.Abs(profile.Evaluate(1f)) > 0.01f)
				Debug.LogWarning($"[TerrainStamp] {name}: профиль на краю (X = 1) не равен нулю — край будет обрезан в 0", this);
		}

		[ContextMenu("Пресет: холм")]
		void PresetHill() => SetProfile(HillCurve());

		[ContextMenu("Пресет: яма")]
		void PresetPit() => SetProfile(PitCurve());

		[ContextMenu("Пресет: кратер (яма с валом)")]
		void PresetCrater() => SetProfile(CraterCurve());

		[ContextMenu("Пресет: плато (плоская вершина)")]
		void PresetMesa() => SetProfile(MesaCurve());

		void SetProfile(AnimationCurve c)
		{
			profile = c;
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(this);
#endif
		}

		public static AnimationCurve HillCurve() => new AnimationCurve(
			new Keyframe(0f, 1f, 0f, 0f),
			new Keyframe(0.5f, 0.55f, -1.6f, -1.6f),
			new Keyframe(1f, 0f, 0f, 0f));

		public static AnimationCurve PitCurve() => new AnimationCurve(
			new Keyframe(0f, -1f, 0f, 0f),
			new Keyframe(0.55f, -0.6f, 1.8f, 1.8f),
			new Keyframe(1f, 0f, 0f, 0f));

		public static AnimationCurve CraterCurve() => new AnimationCurve(
			new Keyframe(0f, -0.7f, 0f, 0f),
			new Keyframe(0.45f, -0.55f, 1f, 1f),
			new Keyframe(0.75f, 0.35f, 0f, 0f),
			new Keyframe(1f, 0f, 0f, 0f));

		public static AnimationCurve MesaCurve() => new AnimationCurve(
			new Keyframe(0f, 1f, 0f, 0f),
			new Keyframe(0.55f, 1f, 0f, 0f),
			new Keyframe(0.85f, 0.2f, -2.5f, -2.5f),
			new Keyframe(1f, 0f, 0f, 0f));
	}
}
