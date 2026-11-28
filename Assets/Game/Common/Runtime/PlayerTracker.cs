using System;
using UnityEngine;
using VContainer;
using Car.Controller;
using VContainer.Unity;

namespace Common.Runtime
{
    public class PlayerTracker : IInitializable, IDisposable
    {
        public Transform PlayerTransform { get; private set; }
        public Rigidbody PlayerRb        { get; private set; }
        public Vector3 PlayerAcceleration { get; private set; }
        public float   PlayerForwardSpeed { get; private set; }

        private CarService _carService;

        public PlayerTracker(CarService carService)
        {
            _carService = carService;
            PlayerAcceleration = Vector3.zero;
            PlayerForwardSpeed = 0f;
        }

        public void RegisterPlayer(Transform transform, Rigidbody rb)
        {
            PlayerTransform = transform;
            PlayerRb        = rb;
            PlayerAcceleration = Vector3.zero;
            PlayerForwardSpeed = 0f;
        }

        public void Initialize()
        {
            if (_carService != null)
                _carService.AccelerationUpdated += OnAccelerationUpdated;
        }

        public void Dispose()
        {
            if (_carService != null)
                _carService.AccelerationUpdated -= OnAccelerationUpdated;
        }

        private void OnAccelerationUpdated(Vector3 worldAccel, float forwardSpeed)
        {
            PlayerAcceleration = worldAccel;
            PlayerForwardSpeed = forwardSpeed;
        }
    }
}