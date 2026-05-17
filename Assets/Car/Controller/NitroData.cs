using NaughtyAttributes;
using UnityEngine;

namespace Car.Controller
{
    [CreateAssetMenu(menuName = "ArcadeCar/NitroData")]
    public class NitroData : ScriptableObject
    {
        [Header("Boost")]
        [Range(0f, 10f)]
        [SerializeField] private float duration = 2f;

        [Min(1f)]
        [Tooltip("Множитель крутящего момента во время нитро")]
        [SerializeField] private float nitroMultiplier = 8f;

        [CurveRange(0f, 0f, 1f, 1f, EColor.Orange)]
        [Tooltip("Как изменяется ускорение за время нитро (X = прогресс, Y = сила)")]
        [SerializeField] private AnimationCurve ramp;

        [Header("Cooldown")]
        [Min(0f)]
        [Tooltip("Время перезарядки после окончания нитро (секунды)")]
        [SerializeField] private float cooldownDuration = 5f;

        [Header("Souls")]
        [Min(0f)]
        [Tooltip("Душ в секунду пока нитро активно")]
        [SerializeField] private float soulDrainPerSecond = 20f;

        public float          Duration           => duration;
        public float          NitroMultiplier    => nitroMultiplier;
        public AnimationCurve Ramp               => ramp;
        public float          CooldownDuration   => cooldownDuration;
        public float          SoulDrainPerSecond => soulDrainPerSecond;
    }
}