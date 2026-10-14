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
		private float _targetSlipAngle;
		private float _lastSlipAngle;

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
			_targetSlipAngle = 0f;
			_lastSlipAngle = 0f;
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
                return new Transition<NoPayload>(CarState.DriftRecovery, new NoPayload());
            }

            return null;
        }

		public override void Tick(float dt, Rigidbody rb, CarPhysicsInput inputData)
		{
		    Vector3 localVelocity = rb.transform.InverseTransformDirection(rb.linearVelocity);
		    float currentSlipAngle = -Mathf.Atan2(localVelocity.x, localVelocity.z) * Mathf.Rad2Deg;
		
		    Vector3 flatVelocity = rb.linearVelocity;
		    flatVelocity.y = 0f;
		    float speed = flatVelocity.magnitude;
		
		    Vector3 flatForward = rb.transform.forward;
		    flatForward.y = 0f;
		    flatForward.Normalize();
		
		    Vector3 moveDir = speed > 0.05f ? flatVelocity / speed : flatForward;
		
		    if (speed > 0.05f)
		    {
		        float realignAngle = Mathf.Abs(currentSlipAngle) * _physicsData.DriftVelocityRealignCoefficient * dt;
		        moveDir = Vector3.RotateTowards(moveDir, flatForward, realignAngle * Mathf.Deg2Rad, 0f);
		        rb.linearVelocity = moveDir * speed + Vector3.up * rb.linearVelocity.y;
		    }
		
		    _transmission.Tick(dt, speed, inputData.Throttle, inputData.Brake);
		    float engagement = _transmission.Engagement;
		
		    bool fuelCut = _engine.UpdateRevLimiter(dt, inputData.Throttle);
		    float spinEngineTorque = _engine.CalculateTorque(inputData.Throttle);
		    if (fuelCut) spinEngineTorque = 0f;
		    float spinEngineFriction = _engine.CalculateFriction();
		    float engineOmega = _engine.AngularSpeed;
		
		    float clutchSpinTorque = _transmission.CalculateClutchTorque(_drivetrain.SpinWheelsOmega, engineOmega, engagement);
			_engine.UpdateAngularSpeed(dt, spinEngineTorque - spinEngineFriction - clutchSpinTorque);
		    float spinDriveTorque = _transmission.CalculateDriveTorque(clutchSpinTorque) * _physicsData.DriftAccelerationCoefficient;
		
			float gripWheelSideOmega = _drivetrain.GripWheelsOmega * _transmission.CurGearRatio;

			float clutchGripTorque;
			if (gripWheelSideOmega >= _engine.MaxOmega)
			{
				clutchGripTorque = _transmission.CalculateClutchTorque(_drivetrain.GripWheelsOmega, _engine.MaxOmega, engagement);
			}
			else
			{
				float gripEngineOmega = Mathf.Max(gripWheelSideOmega, _engine.IdleOmega);
				float gripEngineTorque = _engine.CalculateTorqueAt(inputData.Throttle, gripEngineOmega);
				float gripEngineFriction = _engine.CalculateFrictionAt(gripEngineOmega);
				float curClutchCapacity = _transmission.ClutchMax * Mathf.Clamp01(engagement);

				clutchGripTorque = Mathf.Clamp(gripEngineTorque - gripEngineFriction, -curClutchCapacity, curClutchCapacity);
			}

			float gripDriveTorque = _transmission.CalculateDriveTorque(clutchGripTorque) * _physicsData.DriftAccelerationCoefficient;
		
		    float brakeTorque = inputData.Brake * _physicsData.DriftBrakeTorqueNm;
		    float tireForce = _drivetrain.StepTireAndWheel(
		        dt,
		        driveTorque: gripDriveTorque,
		        brakeTorque: brakeTorque,
		        forwardSpeed: speed,
		        spinDriveTorque: spinDriveTorque);
		
		    float forceDrag = Consts.AirDragForce(_physicsData.AeroEfficiency, _physicsData.FrontArea, rb.linearVelocity.magnitude);
		    float effMass = _physicsData.BaseMass + _engine.EngineMass;
		    float acceleration = (tireForce - forceDrag) / effMass;
		
		    rb.AddForce(moveDir * acceleration, ForceMode.Acceleration);
		
		    float stickSensitivity = 200f;
		    _targetSlipAngle += inputData.Steer * stickSensitivity * dt;
		
		    float maxAngleLimit = 80f;
		    _targetSlipAngle = Mathf.Clamp(_targetSlipAngle, -maxAngleLimit, maxAngleLimit);
		
		    float slipRate = Mathf.DeltaAngle(_lastSlipAngle, currentSlipAngle) / dt;
		    _lastSlipAngle = currentSlipAngle;
		
		    float angleError = Mathf.DeltaAngle(_targetSlipAngle, currentSlipAngle);
		
		    float pPower = 3.5f;
		    float dPower = 1.0f;
		
		    float appliedTorque = -(angleError * pPower) - (slipRate * dPower);
		
		    float maxTorqueLimit = 50f;
		    appliedTorque = Mathf.Clamp(appliedTorque, -maxTorqueLimit, maxTorqueLimit);
		
		    rb.AddRelativeTorque(0f, appliedTorque, 0f, ForceMode.Acceleration);
		
		    float gripModifier = (inputData.IsOffroad ? _physicsData.OffroadGripMultiplier : 1f) *
		                         (inputData.HasSouls ? 1f : _physicsData.NoSoulsGripMultiplier);
		
		    CarPhysicsService.ApplyLateralFriction(rb, _physicsData.DriftSideFrictionCoefficient * gripModifier);
		
		    _driftTimer += dt;
		
		    _transmission.EndStep();
		}    
	}
}
