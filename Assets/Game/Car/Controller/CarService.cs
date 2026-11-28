using System;
using Car.Controller.CarPhysics;
using Car.Souls.Services;
using Car.Controller.CarPhysics.Transmission;
using Game.Car.Controller.CarPhysics.Suspension;
using UnityEngine;

namespace Car.Controller
{
    public readonly struct CarPhysicsInput
    {
        public float   Throttle         { get; }
        public float   Brake            { get; }
        public bool    Drift            { get; }
        public float   Steer            { get; }
        public Vector3 RoadNormal       { get; }
        public float   TorqueMultiplier { get; }
		public bool    IsOffroad        { get; }
		public bool    HasSouls         { get; }

		public bool    IsGrounded          { get; }
		public float   GroundedFactor      { get; }
		public float   DriveGroundedFactor { get; }
		public float   SteerGroundedFactor { get; }
		public Vector3 GroundNormal        { get; }

        public CarPhysicsInput(
            float throttle,
            float brake,
            bool drift,
            float steer,
            Vector3 normal,
            float torqueMultiplier,
            bool isOffroad = false,
            bool hasSouls = true,
            bool isGrounded = true,
            float groundedFactor = 1f,
            float driveGroundedFactor = 1f,
            float steerGroundedFactor = 1f,
            Vector3 groundNormal = default)
        {
            Throttle         = throttle;
            Brake            = brake;
            Drift            = drift;
            Steer            = steer;
            RoadNormal       = normal;
            TorqueMultiplier = torqueMultiplier;
			IsOffroad        = isOffroad;
			HasSouls         = hasSouls;

			IsGrounded          = isGrounded;
			GroundedFactor      = groundedFactor;
			DriveGroundedFactor = driveGroundedFactor;
			SteerGroundedFactor = steerGroundedFactor;
			GroundNormal        = groundNormal == default ? Vector3.up : groundNormal;
        }
    }

    public class CarService
    {
        private readonly InputService        _input;
        private readonly CarPhysicsService   _physics;
        private readonly NitroService        _nitro;
        private readonly RoadCheckService    _road;
        private readonly TransmissionService _transmission;
        private readonly SoulService         _souls;
        private readonly SuspensionController _suspension;

        public Action PhysicsUpdated;
        public Action<Vector3, float> AccelerationUpdated;
        public float   CurrentSpeed { get; private set; }

        public CarService(
            InputService input,
            CarPhysicsService physics,
            NitroService nitro,
            RoadCheckService road,
            TransmissionService transmission,
            SoulService souls,
            SuspensionController suspension)
        {
            _input        = input;
            _physics      = physics;
            _nitro        = nitro;
            _road         = road;
            _transmission = transmission;
            _souls        = souls;
            _suspension   = suspension;
        }

        private Vector3 _prevVelocity;
        private bool _firstPhysicsUpdate = true;

        public void PhysicsUpdate(Rigidbody rb)
        {
            HandleGearShift();

            if (_input.NitroHeld)
                _nitro.TryActivate(_souls);

            _nitro.Tick(Time.fixedDeltaTime, _souls);

			var roadInfo = _road.GetRoadInfo(rb.position);

			bool hasSouls = _souls != null && _souls.CurrentSouls > 0f;

			bool    isGrounded          = true;
			float   groundedFactor      = 1f;
			float   driveGroundedFactor = 1f;
			float   steerGroundedFactor = 1f;
			Vector3 groundNormal        = roadInfo.normal;

			if (_suspension != null)
			{
				isGrounded          = _suspension.IsGrounded;
				groundedFactor      = _suspension.GroundedFactor;
				driveGroundedFactor = _suspension.DriveGroundedFactor;
				steerGroundedFactor = _suspension.SteerGroundedFactor;
				groundNormal        = _suspension.GroundNormal;
			}

			var inputData = new CarPhysicsInput(
				_input.Throttle,
				_input.Brake,
				_input.DriftHeld,
				_input.Steer,
				roadInfo.normal,
				_nitro.GetTorqueMultiplier(),
				roadInfo.isOffroad,
				hasSouls,
				isGrounded,
				groundedFactor,
				driveGroundedFactor,
				steerGroundedFactor,
				groundNormal
			);

            Vector3 prevVel = _firstPhysicsUpdate ? rb.linearVelocity : _prevVelocity;
            _physics.Tick(Time.fixedDeltaTime, rb, inputData);
            
            CurrentSpeed = CarPhysicsService.GetForwardSpeed(rb);
            Vector3 acceleration = (rb.linearVelocity - prevVel) / Time.fixedDeltaTime;
            _prevVelocity = rb.linearVelocity;
            _firstPhysicsUpdate = false;

            _input.ResetShiftBuffers();
            PhysicsUpdated?.Invoke();
            AccelerationUpdated?.Invoke(acceleration, CurrentSpeed);
        }

        private void HandleGearShift()
        {
            if (_input.ShiftUpTriggered)
                _transmission.ShiftUpSafe();
            else if (_input.ShiftDownTriggered)
                _transmission.ShiftDownSafe();
        }
    }
}