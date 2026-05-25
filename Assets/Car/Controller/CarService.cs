using System;
using Car.Controller.CarPhysics;
using Car.Gears;
using Car.Souls.Services;
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

        public CarPhysicsInput(float throttle, float brake, bool drift, float steer, Vector3 normal, float torqueMultiplier, bool isOffroad = false, bool hasSouls = true)
        {
            Throttle         = throttle;
            Brake            = brake;
            Drift            = drift;
            Steer            = steer;
            RoadNormal       = normal;
            TorqueMultiplier = torqueMultiplier;
			IsOffroad        = isOffroad;
			HasSouls         = hasSouls;
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

        public Action PhysicsUpdated;
        public float   CurrentSpeed { get; private set; }

        public CarService(
            InputService input,
            CarPhysicsService physics,
            NitroService nitro,
            RoadCheckService road,
            TransmissionService transmission,
            SoulService souls)
        {
            _input        = input;
            _physics      = physics;
            _nitro        = nitro;
            _road         = road;
            _transmission = transmission;
            _souls        = souls;
        }

        public void PhysicsUpdate(Rigidbody rb)
        {
            HandleGearShift();

            if (_input.NitroHeld)
                _nitro.TryActivate(_souls);

            _nitro.Tick(Time.fixedDeltaTime, _souls);
			
			var roadInfo = _road.GetRoadInfo(rb.position);
			
			bool hasSouls = _souls != null && _souls.CurrentSouls > 0f;

			var inputData = new CarPhysicsInput(
				_input.Throttle,
				_input.Brake,
				_input.DriftHeld,
				_input.Steer,
				roadInfo.normal,
				_nitro.GetTorqueMultiplier(),
				roadInfo.isOffroad,
				hasSouls
			);

            _physics.Tick(Time.fixedDeltaTime, rb, inputData);
            CurrentSpeed = CarPhysicsService.GetForwardSpeed(rb);

            _input.ResetShiftBuffers();
            PhysicsUpdated?.Invoke();
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