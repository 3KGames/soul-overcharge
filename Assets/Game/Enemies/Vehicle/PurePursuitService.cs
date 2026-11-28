using Game.Common.Runtime;
using UnityEngine;

namespace Game.Enemies.Vehicle
{
    public readonly struct SteeringSolution
    {
        public readonly Vector3 GoalWorld;
        public readonly float SteerAngleRad;
        public readonly float TargetYawRate;
        public readonly bool IsTargetBehind;

        public SteeringSolution(Vector3 goalWorld, float steerAngleRad, float targetYawRate, bool isTargetBehind)
        {
            GoalWorld = goalWorld;
            SteerAngleRad = steerAngleRad;
            TargetYawRate = targetYawRate;
            IsTargetBehind = isTargetBehind;
        }
    }

    /// <summary>
    /// Stateless-сервис расчёта точки упреждения и угла руления (Adaptive Pure Pursuit).
    /// </summary>
    public sealed class PurePursuitService
    {
        public SteeringSolution Evaluate(EnemyVehicleModel model, Rigidbody rb)
        {
            var cfg = model.Config.Steering;
            Vector3 pos = rb.position;
            Quaternion rot = rb.rotation;
            Vector3 localVel = Quaternion.Inverse(rot) * rb.linearVelocity;
            float forwardSpeed = localVel.z;

            Vector3 goalWorld = BuildGoal(model, cfg, pos, rot, forwardSpeed);

            // Перевод цели в локальные координаты машины (обязательно Quaternion.Inverse!)
            Vector3 localGoal = Quaternion.Inverse(rot) * (goalWorld - pos);
            localGoal.y = 0f;

            float maxSteerRad = cfg.MaxSteerAngleDeg * Mathf.Deg2Rad;
            float wb = Mathf.Max(0.5f, cfg.Wheelbase);

            // Защита от разворота на 180°: активно докручиваем машину к цели
            if (localGoal.z <= 0.25f)
            {
                float steer = (localGoal.x >= 0f ? 1f : -1f) * maxSteerRad;
                float turnSpeed = Mathf.Max(Mathf.Abs(forwardSpeed), cfg.RecoveryTurnSpeed);
                float recoveryYaw = (turnSpeed / wb) * Mathf.Tan(steer);
                return new SteeringSolution(goalWorld, steer, recoveryYaw, true);
            }

            float ldSq = localGoal.x * localGoal.x + localGoal.z * localGoal.z;
            if (ldSq < 1e-4f)
            {
                return new SteeringSolution(goalWorld, 0f, 0f, false);
            }

            // Unity (+X = right, +Y = up): localGoal.x > 0 -> kappa > 0 -> поворот направо (+Yaw)
            float kappa = (2f * localGoal.x) / ldSq;
            float steerRad = Mathf.Clamp(Mathf.Atan(wb * kappa), -maxSteerRad, maxSteerRad);
            float targetYawRate = (Mathf.Max(0f, forwardSpeed) / wb) * Mathf.Tan(steerRad);

            return new SteeringSolution(goalWorld, steerRad, targetYawRate, false);
        }

        private static Vector3 BuildGoal(
            EnemyVehicleModel model,
            EnemyVehicleConfigSO.SteeringSettings cfg,
            Vector3 pos,
            Quaternion rot,
            float forwardSpeed)
        {
            if (model.WorldTargetOverride.HasValue)
                return model.WorldTargetOverride.Value;

            if (!model.HasRoad)
                return pos + (rot * Vector3.forward) * cfg.MinLookahead;

            float baseL = Mathf.Clamp(
                Mathf.Max(0f, forwardSpeed) * cfg.LookaheadTime + cfg.MinLookahead,
                cfg.MinLookahead,
                cfg.MaxLookahead);

            float denom = 1f + cfg.CurvatureGain * Mathf.Abs(model.Curvature) * baseL;
            float lookahead = Mathf.Clamp(baseL / denom, cfg.MinLookahead * 0.7f, cfg.MaxLookahead);

            if (!model.CurrentPath.TrySampleAtDistanceWithLookahead(
                    model.DistanceAlongPath,
                    lookahead,
                    model.CurrentLane,
                    out var sample,
                    out _))
            {
                return pos + (rot * Vector3.forward) * cfg.MinLookahead;
            }

            float clampedOffset = sample.Path.ClampLateralOffset(
                sample.DistanceAlong,
                model.TargetLateralOffset,
                cfg.EdgeMargin);

            return sample.Position + sample.Right * clampedOffset;
        }
    }
}