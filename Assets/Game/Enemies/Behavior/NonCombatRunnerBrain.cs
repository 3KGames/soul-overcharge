using Common.Runtime;
using Game.Common.Runtime;
using Game.Enemies.Vehicle;
using UnityEngine;

namespace Game.Enemies.Behavior
{
    public sealed class NonCombatRunnerBrain : IEnemyBrain
    {
        private readonly EnemyBehaviorProfileSO _profile;
        private readonly NarrowingDetectionService _narrowing;
        private readonly PlayerTracker _playerTracker;
        private readonly LaneChangeAuthority _laneAuthority;

        private int _desiredLane;
        private bool _laneInitialized;
        private float _playerSpeedEma;
        private bool _emaInitialized;

        public NonCombatRunnerBrain(
            EnemyBehaviorProfileSO profile,
            NarrowingDetectionService narrowing,
            PlayerTracker playerTracker)
        {
            _profile = profile;
            _narrowing = narrowing;
            _playerTracker = playerTracker;
            _laneAuthority = new LaneChangeAuthority(profile.RandomLaneChangeLambda, profile.LaneChangeCooldown);
        }

        public void Reset()
        {
            _laneInitialized = false;
            _emaInitialized = false;
            _laneAuthority.Reset();
        }

        public void Step(EnemyVehicleModel model, Rigidbody rb, float dt)
        {
            if (!model.HasRoad) return;

            if (!_laneInitialized)
            {
                _desiredLane = Mathf.Clamp(model.CurrentLane, 0, Mathf.Max(0, model.CurrentPath.LaneCount - 1));
                _laneInitialized = true;
            }
            else if (model.CurrentPath.LaneCount > 0)
            {
                _desiredLane = Mathf.Clamp(_desiredLane, 0, model.CurrentPath.LaneCount - 1);
            }

            float forwardSpeed = Vector3.Dot(rb.linearVelocity, rb.rotation * Vector3.forward);
            _laneAuthority.Tick(dt);

            if (_narrowing.TryFindSafeLane(
                    model,
                    forwardSpeed,
                    _profile.NarrowingLookaheadMin,
                    _profile.NarrowingLookaheadTime,
                    _desiredLane,
                    out int safeLane))
            {
                _desiredLane = _laneAuthority.ApplyForcedNarrowing(safeLane);
            }
            else if (_laneAuthority.TryRequestRandomChange(
                    model.CurrentPath,
                    model.DistanceAlongPath,
                    _desiredLane,
                    out int randomLane))
            {
                _desiredLane = randomLane;
            }

            float targetOffset = model.CurrentPath.GetLaneOffsetSafe(_desiredLane);
            model.TargetLateralOffset = model.CurrentPath.ClampLateralOffset(
                model.DistanceAlongPath,
                targetOffset,
                model.Config.Steering.EdgeMargin);

            model.TargetSpeed = ComputeTargetSpeed(model);
            model.WorldTargetOverride = null;
            model.PendingPushImpulse = null;
        }

        private float ComputeTargetSpeed(EnemyVehicleModel model)
        {
            if (!_profile.MatchPlayerSpeed || _playerTracker == null || _playerTracker.PlayerTransform == null)
                return Mathf.Clamp(_profile.CruiseSpeed, _profile.MinSpeed, _profile.MaxSpeed);

            float rawPlayerSpeed = Mathf.Abs(_playerTracker.PlayerForwardSpeed);
            Vector3 playerAccel = _playerTracker.PlayerAcceleration;

            if (!_emaInitialized)
            {
                _playerSpeedEma = rawPlayerSpeed;
                _emaInitialized = true;
            }
            else
            {
                _playerSpeedEma = Mathf.Lerp(_playerSpeedEma, rawPlayerSpeed, _profile.PlayerSpeedEmaAlpha);
            }

            float predictionHorizon = _profile.PlayerSpeedPredictionTime;
            float playerForwardAccel = Vector3.Dot(playerAccel, _playerTracker.PlayerTransform.forward);
            float predictedSpeed = rawPlayerSpeed + Mathf.Abs(playerForwardAccel) * predictionHorizon;

            float trackedSpeed = Mathf.Lerp(_playerSpeedEma, predictedSpeed, _profile.PredictionWeight);

            float baseSpeed = Mathf.Lerp(_profile.CruiseSpeed, trackedSpeed, 1f - _profile.CruiseWeight);

            float gapError = 0f;
            if (IsPlayerOnSamePath(model, out float gap))
            {
                gapError = gap - _profile.TargetPlayerGapMeters;
            }
            else
            {
                float worldDist = Vector3.Distance(_playerTracker.PlayerTransform.position, model.ProjectedPosition);
                if (worldDist > _profile.TargetPlayerGapMeters)
                    gapError = worldDist - _profile.TargetPlayerGapMeters;
            }

            float desiredSpeed = baseSpeed + gapError * _profile.GapKp;
            return Mathf.Clamp(desiredSpeed, _profile.MinSpeed, _profile.MaxSpeed);
        }

        private bool IsPlayerOnSamePath(EnemyVehicleModel model, out float gap)
        {
            gap = 0f;
            if (model.CurrentPath == null) return false;

            var playerSample = model.CurrentPath.GetClosestSample(_playerTracker.PlayerTransform.position);
            float playerS = (float)(playerSample.percent * model.CurrentPath.Length);
            float distToPlayer = playerS - model.DistanceAlongPath;

            bool onPath = distToPlayer > -5f && distToPlayer < model.CurrentPath.Length + 5f;
            bool projectionValid = playerSample.percent >= 0f && playerSample.percent <= 1f;

            if (onPath && projectionValid && distToPlayer > 0f)
            {
                gap = distToPlayer;
                return true;
            }
            return false;
        }
    }
}