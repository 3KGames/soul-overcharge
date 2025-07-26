using InputSystem;
using UnityEngine;
using VContainer.Unity;

namespace Car.Controller
{
    using System;
    using UnityEngine.InputSystem;

    public class InputService : IInitializable, IDisposable
    {
        private readonly InputActions _actions = new InputActions();

        public float Steer => _actions.Car.Steer.ReadValue<float>();
        public bool ShiftUpTriggered => _actions.Car.ShiftUp.WasPerformedThisFrame();
        public bool ShiftDownTriggered => _actions.Car.ShiftDown.WasPerformedThisFrame();
        public bool DriftHeld => _actions.Car.Drift.IsPressed();

        public void Initialize() => _actions.Enable();
        public void Dispose() => _actions.Disable();
    }

}