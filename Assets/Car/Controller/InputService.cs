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

        // Delegate fields to use for unsubscription
        private readonly Action<InputAction.CallbackContext> _onDriftPerformed;
        private readonly Action<InputAction.CallbackContext> _onDriftCanceled;
        private readonly Action<InputAction.CallbackContext> _onShiftUp;
        private readonly Action<InputAction.CallbackContext> _onShiftDown;

        public float	Steer				=> _actions.Car.Steer.ReadValue<float>();
        public bool		DriftHeld			{ get; private set; }
        public bool		ShiftUpTriggered	{ get; private set; }
        public bool		ShiftDownTriggered	{ get; private set; }


        public InputService()
        {
            _onDriftPerformed	= ctx => DriftHeld				= true;
            _onDriftCanceled	= ctx => DriftHeld				= false;
            _onShiftUp			= ctx => ShiftUpTriggered		= true;
            _onShiftDown		= ctx => ShiftDownTriggered	= true;
        }

        public void Initialize()
        {
            _actions.Enable();
            _actions.Car.Drift.performed		+= _onDriftPerformed;
            _actions.Car.Drift.canceled			+= _onDriftCanceled;
            _actions.Car.ShiftUp.performed		+= _onShiftUp;
            _actions.Car.ShiftDown.performed	+= _onShiftDown;
        }

        public void Dispose()
        {
            _actions.Car.Drift.performed		-= _onDriftPerformed;
            _actions.Car.Drift.canceled			-= _onDriftCanceled;
            _actions.Car.ShiftUp.performed		-= _onShiftUp;
            _actions.Car.ShiftDown.performed	-= _onShiftDown;
            _actions.Disable();
        }

        public void ResetShiftBuffers()
        {
            ShiftUpTriggered	= false;
            ShiftDownTriggered	= false;
        }
    }

}