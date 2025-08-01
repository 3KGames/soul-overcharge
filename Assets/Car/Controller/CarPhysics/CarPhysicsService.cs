using System.Collections.Generic;
using Car.Controller.CarPhysics.States;
using Car.Gears;
using Common.Runtime.StateMachine;
using UnityEngine;

namespace Car.Controller.CarPhysics
{
	public enum CarState { Drive, Drift }
	
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

        public static void ApplyLateralFriction(Rigidbody rb, float coefficient)
        {
            Vector3 rightVel = Vector3.Project(rb.linearVelocity, rb.transform.right);
            rb.AddForce(-rightVel * coefficient, ForceMode.Acceleration);
        }
	}
}
