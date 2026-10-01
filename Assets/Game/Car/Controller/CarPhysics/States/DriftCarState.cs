using System;
using Car.Controller.CarPhysics.Drivetrain;
using Car.Controller.CarPhysics.Engine;
using Car.Controller.CarPhysics.Transmission;
using Common.Runtime;
using UnityEngine;

namespace Car.Controller.CarPhysics.States
{
    public class DriftCarState : BaseCarState, ITransitionPayload<int>
    {
        private CarPhysicsData _physicsData;
        private readonly EngineService _engine;
        private TransmissionService _transmission;
        private readonly DrivetrainService _drivetrain;

        private float _driftTimer;

        public int DriftDir { get; private set; }

        public override CarState Kind => CarState.Drift;

        /// <param name="dir">Drift direction (-1 = left,  +1 = right)</param>
        public delegate void DriftStartedHandler(int dir);
        public event DriftStartedHandler OnDriftStarted;

        /// <param name="duration">Drift duration (sec.)</param>
        public delegate void DriftEndedHandler(float duration);
        public event DriftEndedHandler OnDriftEnded;

        public DriftCarState(CarPhysicsData physicsData, EngineService engine, DrivetrainService drivetrain, TransmissionService transmission)
        {
            _physicsData = physicsData;
            _engine = engine;
            _drivetrain = drivetrain;
            _transmission = transmission;
        }

        public void ApplyPayload(int driftDir)
        {
            DriftDir = driftDir;
        }

        public override void Enter()
        {
            _driftTimer = 0f;
            OnDriftStarted?.Invoke(DriftDir);
        }

        public override void Exit()
        {
        }

        public override ITransition EvaluateTransition(float dt, Rigidbody rb, CarPhysicsInput inputData)
        {
            if (!inputData.Drift)
            {
                OnDriftEnded?.Invoke(_driftTimer);
                return new Transition<NoPayload>(CarState.Drive, new NoPayload());
            }

            return null;
        }

        public override void Tick(float dt, Rigidbody rb, CarPhysicsInput inputData)
        {
            float forwardSpeed = CarPhysicsService.GetForwardSpeed(rb);

            float omegaWheels = _drivetrain.DrivenWheelsOmega;

            _transmission.Tick(dt, forwardSpeed, inputData.Throttle, inputData.Brake);
            float engagement = _transmission.Engagement;

            bool fuelCut = _engine.UpdateRevLimiter(dt, inputData.Throttle);
            float engineTorque = _engine.CalculateTorque(inputData.Throttle);
            if (fuelCut) engineTorque = 0f;
            float engineFriction = _engine.CalculateFriction();
            float clutchTorque = _transmission.CalculateClutchTorque(omegaWheels, _engine.AngularSpeed, engagement);

            float torqueEffective = engineTorque - engineFriction - clutchTorque;
            _engine.UpdateAngularSpeed(dt, torqueEffective);

            float torqueDrive = _transmission.CalculateDriveTorque(clutchTorque);
			float forceDrag = Consts.AirDragForce(_physicsData.AeroEfficiency, _physicsData.FrontArea, forwardSpeed);

            torqueDrive *= _physicsData.DriftAccelerationCoefficient;

            float brakeTorque = inputData.Brake * _physicsData.DriftBrakeTorqueNm;
            float tireForce = _drivetrain.StepTireAndWheel(dt, torqueDrive, brakeTorque, forwardSpeed);

            float effMass = _physicsData.BaseMass + _engine.EngineMass;
            float acceleration = (tireForce - forceDrag) / effMass;
            rb.AddForce(rb.transform.forward * acceleration, ForceMode.Acceleration);

            float gripModifier = (inputData.IsOffroad ? _physicsData.OffroadGripMultiplier : 1f) *
                                 (inputData.HasSouls ? 1f : _physicsData.NoSoulsGripMultiplier);

            float t = (inputData.Steer * DriftDir + 1f) * 0.5f;
            float driftAngleCoef = Mathf.Lerp(
                _physicsData.MinDriftAngleCoefficient,
                _physicsData.MaxDriftAngleCoefficient,
                t);

            float turnRadius = _drivetrain.GetTurnRadius(forwardSpeed);

            float safeTurnRadius = Mathf.Max(0.1f, turnRadius);

            float turnRate = (rb.linearVelocity.magnitude / safeTurnRadius) * driftAngleCoef * DriftDir * (inputData.HasSouls ? 1f : _physicsData.NoSoulsGripMultiplier);

            Quaternion deltaRotation = Quaternion.Euler(0f, turnRate * Mathf.Rad2Deg * dt, 0f);
            rb.MoveRotation(rb.rotation * deltaRotation);

            CarPhysicsService.ApplyLateralFriction(rb, _physicsData.DriftSideFrictionCoefficient * gripModifier);

            _driftTimer += dt;

            _transmission.EndStep();
        }
    }
}
