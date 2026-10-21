using System.Collections.Generic;
using Car.Controller.CarPhysics.States;
using Common.Runtime;
using Common.Runtime.StateMachine;
using UnityEngine;

namespace Car.Controller.CarPhysics
{
	public enum CarState { Drive, Drift, DriftRecovery }

    public class CarPhysicsService
    {
		private readonly CarPhysicsData _physicsData;

		private Dictionary<CarState, BaseCarState> _states = new();
		private CarState _currentState;

		public CarPhysicsService(CarPhysicsData physicsData, IEnumerable<BaseCarState> states)
		{
			_physicsData = physicsData;

			foreach (var s in states)
				_states[s.Kind] = s;

			_currentState = CarState.Drive;
		}

        public void Tick(float dt, Rigidbody rb, CarPhysicsInput inputData)
		{
			ITransition transition = _states[_currentState].EvaluateTransition(dt, rb, inputData);

			if (transition != null && transition.NextState != _currentState)
				Switch(transition);

			_states[_currentState].Tick(dt, rb, inputData);

			ApplyAirResistance(rb, _physicsData);
			ApplyAngularDamping(rb, _physicsData);
		}

		private void Switch(ITransition transition)
		{
			_states[_currentState].Exit();

			_currentState = transition.NextState;
			var nextState = _states[_currentState];

			transition.ApplyTo(nextState);

			nextState.Enter();
		}

		public static float GetForwardSpeed(Rigidbody rb)
		{
			return Vector3.Dot(rb.linearVelocity, rb.transform.forward);
		}

        public static void AlignToRoad(Rigidbody rb, Vector3 roadNormal)
        {
            if (roadNormal == Vector3.zero) return;
            Quaternion toRoad = Quaternion.FromToRotation(rb.transform.up, roadNormal);
            rb.MoveRotation(toRoad * rb.rotation);
        }

        public static void ApplyLateralFriction(Rigidbody rb, Vector3 groundNormal, float coefficient)
        {
            if (coefficient <= 0.0001f)
                return;

            if (groundNormal.sqrMagnitude < 0.001f)
                groundNormal = Vector3.up;

            Vector3 groundRight = Vector3.ProjectOnPlane(rb.transform.right, groundNormal);
            if (groundRight.sqrMagnitude < 0.001f)
                return;
            groundRight.Normalize();

            Vector3 lateralVel = Vector3.Project(rb.linearVelocity, groundRight);
            rb.AddForce(-lateralVel * coefficient, ForceMode.Acceleration);
        }

		public static void ApplyAirResistance(Rigidbody rb, CarPhysicsData physicsData)
		{
			Vector3 localVelocity = rb.transform.InverseTransformDirection(rb.linearVelocity);

			Vector3 localDrag = Consts.AirDragForceLocal(
				physicsData.LateralAeroEfficiency,
				physicsData.VerticalAeroEfficiency,
				physicsData.AeroEfficiency,
				physicsData.FrontArea,
				localVelocity);

			rb.AddForce(rb.transform.TransformDirection(localDrag), ForceMode.Force);
		}

		public static void ApplyAngularDamping(Rigidbody rb, CarPhysicsData physicsData)
		{
			Vector3 localAngularVelocity = rb.transform.InverseTransformDirection(rb.angularVelocity);
			Vector3 localAngularAcceleration = -Vector3.Scale(localAngularVelocity, physicsData.AngularDampingLocal);

			rb.AddTorque(rb.transform.TransformDirection(localAngularAcceleration), ForceMode.Acceleration);
		}

		public static void DisableBuiltinDamping(Rigidbody rb)
		{
			rb.linearDamping = 0f;
			rb.angularDamping = 0f;
		}
	}
}
