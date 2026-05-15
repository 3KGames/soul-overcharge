using InputSystem;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Car.Controller
{
    public class InputService : IInitializable, IDisposable
    {
        private readonly InputActions _actions = new InputActions();

        private readonly Action<InputAction.CallbackContext> _onDriftPerformed;
        private readonly Action<InputAction.CallbackContext> _onDriftCanceled;
        private readonly Action<InputAction.CallbackContext> _onShiftUp;
        private readonly Action<InputAction.CallbackContext> _onShiftDown;
        private readonly Action<InputAction.CallbackContext> _onThrottleUpdate;

        public event Action<float> OnThrottleChanged;

        public float Throttle           => _actions.Car.Throttle.ReadValue<float>();
        public float Brake              => _actions.Car.Brake.ReadValue<float>();
        public float Steer              => _actions.Car.Steer.ReadValue<float>();
        public bool  DriftHeld          { get; private set; }
        public bool  ShiftUpTriggered   { get; private set; }
        public bool  ShiftDownTriggered { get; private set; }

        public InputService()
        {
            _onDriftPerformed = ctx => DriftHeld           = true;
            _onDriftCanceled  = ctx => DriftHeld           = false;
            _onShiftUp        = ctx => ShiftUpTriggered    = true;
            _onShiftDown      = ctx => ShiftDownTriggered  = true;
            _onThrottleUpdate = ctx => OnThrottleChanged?.Invoke(ctx.ReadValue<float>());
        }

        public void Initialize()
        {
            _actions.Enable();

            _actions.Car.Drift.performed     += _onDriftPerformed;
            _actions.Car.Drift.canceled      += _onDriftCanceled;
            _actions.Car.ShiftUp.performed   += _onShiftUp;
            _actions.Car.ShiftDown.performed += _onShiftDown;

            _actions.Car.Throttle.performed  += _onThrottleUpdate;
            _actions.Car.Throttle.canceled   += _onThrottleUpdate;
        }

        public void Dispose()
        {
            _actions.Car.Drift.performed     -= _onDriftPerformed;
            _actions.Car.Drift.canceled      -= _onDriftCanceled;
            _actions.Car.ShiftUp.performed   -= _onShiftUp;
            _actions.Car.ShiftDown.performed -= _onShiftDown;

            _actions.Car.Throttle.performed  -= _onThrottleUpdate;
            _actions.Car.Throttle.canceled   -= _onThrottleUpdate;

            _actions.Disable();
        }

        public void ResetShiftBuffers()
        {
            ShiftUpTriggered   = false;
            ShiftDownTriggered = false;
        }
    }
}