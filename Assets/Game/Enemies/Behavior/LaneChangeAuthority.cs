using Game.Common.Runtime;
using UnityEngine;

namespace Game.Enemies.Behavior
{
    /// <summary>
    /// Единый арбитр смены полос для одного врага:
    /// ForcedNarrowing всегда игнорирует cooldown, Random идёт по безопасному Пуассоновскому таймеру.
    /// </summary>
    public sealed class LaneChangeAuthority
    {
        private readonly float _lambda;
        private readonly float _cooldownDuration;

        private float _cooldownTimer;
        private float _poissonTimer;

        public LaneChangeAuthority(float randomLambda, float cooldownDuration)
        {
            _lambda = Mathf.Max(0f, randomLambda);
            _cooldownDuration = Mathf.Max(0f, cooldownDuration);
            Reset();
        }

        public void Reset()
        {
            _cooldownTimer = 0f;
            _poissonTimer = SampleNextPoissonWait();
        }

        public void Tick(float dt)
        {
            if (_cooldownTimer > 0f) _cooldownTimer = Mathf.Max(0f, _cooldownTimer - dt);
            if (_poissonTimer > 0f) _poissonTimer = Mathf.Max(0f, _poissonTimer - dt);
        }

        public int ApplyForcedNarrowing(int safeLane)
        {
            _cooldownTimer = _cooldownDuration;
            _poissonTimer = SampleNextPoissonWait();
            return safeLane;
        }

        public bool TryRequestRandomChange(IRoadPath path, float s, int currentLane, out int newLane)
        {
            newLane = currentLane;
            if (_lambda <= 1e-4f || _cooldownTimer > 0f || _poissonTimer > 0f || path == null || path.LaneCount < 2)
                return false;

            int dir = Random.value >= 0.5f ? 1 : -1;
            int candidate = currentLane + dir;

            if (!IsValidActiveLane(path, s, candidate))
            {
                candidate = currentLane - dir;
                if (!IsValidActiveLane(path, s, candidate))
                {
                    _poissonTimer = SampleNextPoissonWait();
                    return false;
                }
            }

            newLane = candidate;
            _cooldownTimer = _cooldownDuration;
            _poissonTimer = SampleNextPoissonWait();
            return true;
        }

        private static bool IsValidActiveLane(IRoadPath path, float s, int lane)
        {
            return lane >= 0 && lane < path.LaneCount && path.IsLaneActiveAt(lane, s);
        }

        private float SampleNextPoissonWait()
        {
            if (_lambda <= 1e-4f) return float.MaxValue;
            // Защита от Mathf.Log(0): Random.value в Unity включает 0.0 и 1.0
            float u = Mathf.Clamp(Random.value, 1e-4f, 1f - 1e-4f);
            return -Mathf.Log(u) / _lambda;
        }
    }
}