using NaughtyAttributes;
using UnityEngine;

namespace Car.Souls.Data
{
    [CreateAssetMenu(menuName = "ArcadeCar/SoulData")]
    public class SoulData : ScriptableObject
    {
        [Min(1f)]
        [SerializeField] private float maxSouls = 100f;
        
        [Min(0f)]
        [SerializeField] private float startSouls = 0f;

        [Min(0f)]
        [SerializeField] private float soulsPerKill = 10f;

        [CurveRange(0f, 0f, 1f, 5f)]
        [SerializeField] private AnimationCurve drainBySpeedCurve =
            AnimationCurve.Linear(0f, 0.1f, 1f, 2f);

        [Min(0.1f)]
        [SerializeField] private float maxSpeedForCurve = 30f;

        [SerializeField] private float[] gearDrainMultipliers = { 1f, 1.2f, 1.5f, 2f, 2.5f };

        [Min(0f)]
        [SerializeField] private float idleDrainPerSecond = 0f;

        public float MaxSouls           => maxSouls;
        public float StartSouls         => Mathf.Clamp(startSouls, 0f, maxSouls);
        public float SoulsPerKill       => soulsPerKill;
        public AnimationCurve DrainBySpeedCurve => drainBySpeedCurve;
        public float MaxSpeedForCurve   => maxSpeedForCurve;
        public float IdleDrainPerSecond => idleDrainPerSecond;

        public float GetGearDrainMultiplier(int gearIndex)
        {
            if (gearDrainMultipliers == null || gearDrainMultipliers.Length == 0)
                return 1f;
            int clamped = Mathf.Clamp(gearIndex, 0, gearDrainMultipliers.Length - 1);
            return gearDrainMultipliers[clamped];
        }
    }
}