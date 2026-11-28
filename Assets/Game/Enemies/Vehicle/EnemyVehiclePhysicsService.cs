using UnityEngine;

namespace Game.Enemies.Vehicle
{
    public sealed class EnemyVehiclePhysicsService
    {
        private readonly PurePursuitService _purePursuit;
        private readonly RoadTrackingService _roadTracking;
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[1];

        public EnemyVehiclePhysicsService(PurePursuitService purePursuit, RoadTrackingService roadTracking)
        {
            _purePursuit = purePursuit;
            _roadTracking = roadTracking;
        }

        public void ConfigureRigidbody(Rigidbody rb, EnemyVehicleConfigSO config)
        {
            var body = config.Body;
            rb.mass = body.Mass;
            rb.linearDamping = body.LinearDamping;
            rb.angularDamping = body.AngularDamping;
            rb.centerOfMass = body.CenterOfMass;
            rb.maxAngularVelocity = body.MaxAngularVelocity;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Step(EnemyVehicleModel model, Rigidbody rb, float dt)
        {
            if (dt <= 1e-5f) return;

            Quaternion rot = rb.rotation;
            Vector3 forward = rot * Vector3.forward;
            Vector3 right = rot * Vector3.right;
            Vector3 up = rot * Vector3.up;
            Vector3 localVel = Quaternion.Inverse(rot) * rb.linearVelocity;
            Vector3 angVel = rb.angularVelocity;

            float forwardSpeed = localVel.z;
            float lateralSpeed = localVel.x;
            float currentYawRate = Vector3.Dot(angVel, up);

            SteeringSolution steering = _purePursuit.Evaluate(model, rb);
            model.LastGoalWorld = steering.GoalWorld;
            model.LastSteerAngleRad = steering.SteerAngleRad;

            float yawAccel = ComputeYawAcceleration(model, steering.TargetYawRate, currentYawRate, dt);

            float requestedSpeed = steering.IsTargetBehind
                ? Mathf.Min(model.TargetSpeed, model.Config.Steering.RecoveryTurnSpeed)
                : model.TargetSpeed;
            float fwdAccel = ComputeLongitudinalAcceleration(model.Config.Speed, forwardSpeed, requestedSpeed, model.Curvature);

            float latAccel = ComputeLateralDamping(model.Config.Stability, lateralSpeed, forwardSpeed, dt);

            Vector3 uprightAccel = ComputeUprightAcceleration(model.Config.Stability, up, forward, model.RoadUp, angVel);

            Vector3 hoverAccel = ComputeHoverAcceleration(model.Config.Body, rb, up, dt, model.RoadUp);

            rb.AddForce(forward * fwdAccel + right * latAccel + hoverAccel, ForceMode.Acceleration);
            rb.AddTorque(up * yawAccel + uprightAccel, ForceMode.Acceleration);

            if (model.PendingPushImpulse.HasValue)
            {
                rb.AddForce(model.PendingPushImpulse.Value, ForceMode.Impulse);
                model.PendingPushImpulse = null;
            }

            model.LastThrottle = Mathf.Clamp01(fwdAccel / model.Config.Speed.MaxDriveAccel);
            UpdateStuckRecovery(model, rb, forwardSpeed, requestedSpeed, dt);
        }

        private static float ComputeYawAcceleration(
            EnemyVehicleModel model,
            float targetYawRate,
            float currentYawRate,
            float dt)
        {
            var cfg = model.Config.Yaw;

            if (!model.YawControllerInitialized)
            {
                model.PrevYawRate = currentYawRate;
                model.FilteredYawAlpha = 0f;
                model.YawControllerInitialized = true;
            }

            float rawAlpha = (currentYawRate - model.PrevYawRate) / dt;
            model.PrevYawRate = currentYawRate;

            float filter = 1f - Mathf.Exp(-dt / cfg.DerivativeTau);
            model.FilteredYawAlpha = Mathf.Clamp(
                Mathf.Lerp(model.FilteredYawAlpha, rawAlpha, filter),
                -cfg.MaxAccel,
                cfg.MaxAccel);

            float accel = cfg.Kp * (targetYawRate - currentYawRate) - cfg.Kd * model.FilteredYawAlpha;
            return Mathf.Clamp(accel, -cfg.MaxAccel, cfg.MaxAccel);
        }

        private static float ComputeLongitudinalAcceleration(
            EnemyVehicleConfigSO.SpeedSettings cfg,
            float currentSpeed,
            float desiredSpeed,
            float curvature)
        {
            float cornerLimit = float.MaxValue;
            float absK = Mathf.Abs(curvature);
            if (cfg.CornerFrictionMu > 0f && absK > 1e-4f)
            {
                cornerLimit = Mathf.Sqrt((cfg.CornerFrictionMu * 9.81f) / absK);
            }

            float target = Mathf.Min(Mathf.Max(0f, desiredSpeed), cornerLimit);
            return Mathf.Clamp((target - currentSpeed) * cfg.Kp, -cfg.MaxBrakeAccel, cfg.MaxDriveAccel);
        }

        private static float ComputeLateralDamping(
            EnemyVehicleConfigSO.StabilitySettings cfg,
            float lateralSpeed,
            float forwardSpeed,
            float dt)
        {
            if (Mathf.Abs(lateralSpeed) < 0.01f) return 0f;

            float speedT = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 30f);
            float k = cfg.LateralDamping * Mathf.Lerp(1f, cfg.HighSpeedDampingMultiplier, speedT);
            return -(lateralSpeed * (1f - Mathf.Exp(-k * dt))) / dt;
        }

        private static Vector3 ComputeUprightAcceleration(
            EnemyVehicleConfigSO.StabilitySettings cfg,
            Vector3 vehicleUp,
            Vector3 vehicleForward,
            Vector3 roadUp,
            Vector3 angularVelocity)
        {
            float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(vehicleUp, roadUp), -1f, 1f));
            if (angle < 1e-3f) return Vector3.zero;

            Vector3 axis = Vector3.Cross(vehicleUp, roadUp);
            axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : vehicleForward;

            float angVel = Vector3.Dot(angularVelocity, axis);
            Vector3 accel = axis * (cfg.UprightStiffness * angle - cfg.UprightDamping * angVel);
            return Vector3.ClampMagnitude(accel, cfg.MaxUprightAccel);
        }

        private Vector3 ComputeHoverAcceleration(
            EnemyVehicleConfigSO.BodySettings body,
            Rigidbody rb,
            Vector3 up,
            float dt,
            Vector3 roadUp)
        {
            if (body.HoverHeight <= 0f || body.HoverFrequency <= 0f)
                return Vector3.zero;

            Vector3 rayOrigin = rb.position + up * body.HoverRaycastOffset;
            Vector3 rayDir = -up;
            float maxDist = body.HoverHeight + 1f;

            LayerMask mask = body.HoverGroundMask.value != 0 ? body.HoverGroundMask : Physics.DefaultRaycastLayers;

            int hits = Physics.RaycastNonAlloc(rayOrigin, rayDir, _hitBuffer, maxDist, mask, QueryTriggerInteraction.Ignore);
            if (hits == 0)
                return Vector3.zero;

            var hit = _hitBuffer[0];
            float dist = hit.distance;
            float compression = body.HoverHeight - dist;

            if (compression <= 0f)
                return Vector3.zero;

            float omega = 2f * Mathf.PI * body.HoverFrequency;
            float k = omega * omega;
            float c = 2f * body.HoverDampingRatio * omega;

            float vel = Vector3.Dot(rb.linearVelocity, up);

            float accel = compression * k - vel * c;

            accel = Mathf.Clamp(accel, -9.81f, 19.62f);

            Vector3 hoverDir = roadUp.sqrMagnitude > 1e-6f ? roadUp.normalized : Vector3.up;
            return hoverDir * accel;
        }

        private void UpdateStuckRecovery(
            EnemyVehicleModel model,
            Rigidbody rb,
            float forwardSpeed,
            float requestedSpeed,
            float dt)
        {
            var body = model.Config.Body;
            if (requestedSpeed <= 2f || Mathf.Abs(forwardSpeed) >= body.StuckSpeedThreshold)
            {
                model.StuckTimer = 0f;
                return;
            }

            model.StuckTimer += dt;
            if (model.StuckTimer < body.StuckDuration) return;

            model.StuckTimer = 0f;
            if (_roadTracking.TryGetRecoveryPose(model, out Vector3 recoverPos, out Quaternion recoverRot))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = recoverPos;
                rb.rotation = recoverRot;
                model.YawControllerInitialized = false;
            }
        }
    }
}