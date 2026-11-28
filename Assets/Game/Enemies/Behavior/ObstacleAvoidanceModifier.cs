using Game.Common.Runtime;
using Game.Enemies.Vehicle;
using UnityEngine;

namespace Game.Enemies.Behavior
{
    /// <summary>
    /// Постобработка команды перед Pure Pursuit: сдвигает TargetLateralOffset в свободную сторону
    /// с гистерезисом или снижает TargetSpeed, если объехать невозможно.
    /// </summary>
    public sealed class ObstacleAvoidanceModifier : IVehicleCommandModifier
    {
        private readonly AgentSpatialRegistry _registry;
        private readonly EnemyBehaviorProfileSO _profile;

        private int _avoidSide; // -1 (влево) / +1 (вправо) / 0 (нет)
        private float _holdTimer;

        public ObstacleAvoidanceModifier(AgentSpatialRegistry registry, EnemyBehaviorProfileSO profile)
        {
            _registry = registry;
            _profile = profile;
        }

        public void Reset()
        {
            _avoidSide = 0;
            _holdTimer = 0f;
        }

        public void Modify(EnemyVehicleModel model, float dt)
        {
            if (!model.HasRoad) return;

            if (_holdTimer > 0f) _holdTimer = Mathf.Max(0f, _holdTimer - dt);

            bool blocked = _registry.TryFindNearestAhead(
                model.SpatialAgentId,
                model.CurrentPath,
                model.DistanceAlongPath,
                model.LateralOffset,
                _profile.AvoidanceLookahead,
                _profile.AvoidanceBandHalfWidth,
                out float distAhead,
                out float obsLatDelta);

            if (!blocked)
            {
                if (_holdTimer <= 0f) _avoidSide = 0;
                return;
            }

            float laneWidth = Mathf.Max(1f, model.CurrentPath.LaneWidth);
            float edgeMargin = model.Config.Steering.EdgeMargin;
            float baseOffset = model.TargetLateralOffset;

            if (_avoidSide == 0 || _holdTimer <= 0f)
            {
                // Препятствие справа (obsLatDelta > 0) -> пробуем уйти влево (-1), но проверяем, есть ли место
                int preferredSide = obsLatDelta >= 0f ? -1 : 1;
                float leftCandidate = model.CurrentPath.ClampLateralOffset(model.DistanceAlongPath, baseOffset - laneWidth, edgeMargin);
                float rightCandidate = model.CurrentPath.ClampLateralOffset(model.DistanceAlongPath, baseOffset + laneWidth, edgeMargin);

                float leftRoom = Mathf.Abs(leftCandidate - baseOffset);
                float rightRoom = Mathf.Abs(rightCandidate - baseOffset);

                if (preferredSide < 0)
                    _avoidSide = leftRoom >= 1.2f ? -1 : (rightRoom >= 1.2f ? 1 : -1);
                else
                    _avoidSide = rightRoom >= 1.2f ? 1 : (leftRoom >= 1.2f ? -1 : 1);

                _holdTimer = _profile.AvoidanceHoldTime;
            }

            float shifted = model.CurrentPath.ClampLateralOffset(
                model.DistanceAlongPath,
                baseOffset + _avoidSide * laneWidth * 0.85f,
                edgeMargin);

            model.TargetLateralOffset = shifted;

            // Если места для смещения нет или препятствие слишком близко — сбрасываем скорость
            float achievedShift = Mathf.Abs(shifted - baseOffset);
            float urgency = 1f - Mathf.Clamp01(distAhead / _profile.AvoidanceLookahead);
            float speedFactor = achievedShift < 0.7f
                ? Mathf.Lerp(0.75f, 0.2f, urgency)
                : Mathf.Lerp(0.95f, 0.65f, urgency);

            model.TargetSpeed *= speedFactor;
        }
    }
}