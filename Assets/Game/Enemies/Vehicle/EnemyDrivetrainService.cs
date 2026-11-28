using UnityEngine;

namespace Game.Enemies.Vehicle
{
    /// <summary>
    /// Stateless-сервис синтетического RPM/КПП и бокового скольжения (в радианах) для FMOD.
    /// </summary>
    public sealed class EnemyDrivetrainService
    {
        public void Step(EnemyVehicleModel model, Rigidbody rb, float dt)
        {
            var cfg = model.Config.Drivetrain;
            Vector3 localVel = Quaternion.Inverse(rb.rotation) * rb.linearVelocity;

            float forwardSpeed = localVel.z;
            float absForward = Mathf.Abs(forwardSpeed);

            // 1. Обороты, физически связанные со скоростью колёс и текущей передачей
            // (при сбросе газа на 180 км/ч RPM не падает до холостых!)
            float wheelOmega = absForward / Mathf.Max(0.1f, cfg.WheelRadius);
            float totalRatio = cfg.GetGearRatio(model.CurrentGear) * cfg.FinalDrive;
            float wheelDrivenRpm = wheelOmega * totalRatio * (30f / Mathf.PI);

            float targetRpm = Mathf.Clamp(
                wheelDrivenRpm + model.LastThrottle * cfg.ThrottleRpmBoost,
                cfg.IdleRpm,
                cfg.MaxRpm);

            float smooth = 1f - Mathf.Exp(-cfg.RpmResponseSpeed * dt);
            model.CurrentRpm = Mathf.Lerp(model.CurrentRpm, targetRpm, smooth);

            // 2. Переключение передач с гистерезисом и защитой от зависания за редлайном
            if (model.ShiftCooldownTimer > 0f)
                model.ShiftCooldownTimer = Mathf.Max(0f, model.ShiftCooldownTimer - dt);

            if (model.ShiftCooldownTimer <= 0f)
            {
                float safeShiftUp = Mathf.Min(cfg.ShiftUpRpm, cfg.MaxRpm * 0.95f);
                float safeShiftDown = Mathf.Max(cfg.ShiftDownRpm, cfg.IdleRpm * 1.15f);

                if (model.CurrentRpm >= safeShiftUp && model.CurrentGear < cfg.GearCount - 1)
                {
                    model.CurrentGear++;
                    model.ShiftCooldownTimer = cfg.ShiftCooldown;
                    model.NotifyGearChanged(model.CurrentGear);
                }
                else if (model.CurrentRpm <= safeShiftDown && model.CurrentGear > 0)
                {
                    model.CurrentGear--;
                    model.ShiftCooldownTimer = cfg.ShiftCooldown;
                    model.NotifyGearChanged(model.CurrentGear);
                }
            }

            // 3. Телеметрия (LateralSlip строго в радианах)
            model.ForwardSpeed = forwardSpeed;
            model.EngineLoad = model.LastThrottle;
            model.LateralSlipRadians = Mathf.Atan2(Mathf.Abs(localVel.x), Mathf.Max(1f, absForward));
        }
    }
}