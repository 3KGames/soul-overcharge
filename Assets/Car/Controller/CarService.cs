using System;
using Car.Controller.CarPhysics;
using Car.Controller.CarPhysics.States;
using Car.Gears;
using UnityEngine;
using VContainer;

namespace Car.Controller
{
    public readonly struct CarPhysicsInput
    {
		public float	Throttle			{ get; }
		public float	Brake				{ get; }
        public bool		Drift				{ get; }
        public float	Steer				{ get; }
        public Vector3	RoadNormal			{ get; }
        public float	TorqueMultiplier	{ get; }
		
        public CarPhysicsInput(float throttle, float brake, bool drift, float steer, Vector3 normal, float torqueMultiplier)
        {
			Throttle      		= throttle;
			Brake				= brake;
            Drift      			= drift;
            Steer      			= steer;
            RoadNormal 			= normal;
            TorqueMultiplier	= torqueMultiplier;
        }
    }
    
    public class CarService
    {
        private InputService		  _input;
        private CarPhysicsService	  _physics;
        private NitroService		  _nitro;
        private RoadCheckService	  _road;
        private TransmissionService	  _transmission;

        public Action PhysicsUpdated;

        public float CurrentSpeed { get; private set; }
        
        public CarService(InputService input, CarPhysicsService physics, NitroService nitro, RoadCheckService road, TransmissionService transmission)
        {
            _input = input;
            _physics = physics;
            _nitro = nitro;
            _road = road;
            _transmission = transmission;
        }

        public void PhysicsUpdate(Rigidbody rb)
        {
            HandleGearShift();
			
            _nitro.Tick(Time.fixedDeltaTime);
            
            var inputData = new CarPhysicsInput(
				_input.Throttle,
				_input.Brake,
                _input.DriftHeld,
                _input.Steer,
                _road.GetRoadNormal(rb.position),
                _nitro.GetTorqueMultiplier());
			
            _physics.Tick(Time.fixedDeltaTime, rb, inputData);
            CurrentSpeed = CarPhysicsService.GetForwardSpeed(rb);
            
            _input.ResetShiftBuffers();
            PhysicsUpdated?.Invoke();
        }
        
        private void HandleGearShift()
        {
            if (_input.ShiftUpTriggered)
            {
                Debug.Log("shiftup");
                _transmission.ShiftUpSafe();
            }
            else if (_input.ShiftDownTriggered)
            {
                _transmission.ShiftDownSafe();
            }
        }
    }
}