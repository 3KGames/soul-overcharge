using Common.Runtime.StateMachine;
using Level.Runtime;
using UnityEngine;

namespace Car.Controller.CarPhysics.States
{
	public abstract class BaseCarState: IState<CarState>
	{
		public abstract CarState Kind { get; }
		
		public abstract void		Enter();
		/// Checks if the current state should be changed to another one.
		public abstract ITransition	EvaluateTransition(float dt, Rigidbody rb, CarPhysicsInput inputData);
		/// <returns>Next state, or null if the current state remains unchanged.</returns>
		public abstract void		Tick(float dt, Rigidbody rb, CarPhysicsInput inputData);
		public abstract void		Exit();
	}
}