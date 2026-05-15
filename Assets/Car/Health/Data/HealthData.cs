using NaughtyAttributes;
using UnityEngine;

namespace Car.Health.Data
{
    [CreateAssetMenu(menuName = "ArcadeCar/HealthData")]
    public class HealthData : ScriptableObject
    {
        [Min(1f)]
        [SerializeField] private float maxHealth = 100f;

        [Min(0f)]
        [SerializeField] private float minHealth = 5f;

        [CurveRange(0f, 0f, 1f, 1f)]
        [SerializeField] private AnimationCurve healthBySoulsCurve =
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));

        [Min(0f)]
        [SerializeField] private float damageToSoulsRatio = 0f;

        public float          MaxHealth          => maxHealth;
        public float          MinHealth          => Mathf.Min(minHealth, maxHealth);
        public AnimationCurve HealthBySoulsCurve => healthBySoulsCurve;
        public float          DamageToSoulsRatio => damageToSoulsRatio;
    }
}