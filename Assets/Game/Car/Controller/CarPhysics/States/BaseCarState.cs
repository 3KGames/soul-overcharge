using Common.Runtime.StateMachine;
using Level.Runtime;
using UnityEngine;

namespace Car.Controller.CarPhysics.States
{
	public abstract class BaseCarState: IState<CarState>
	{
		public abstract CarState Kind { get; }

		public abstract void		Enter();
		public abstract ITransition	EvaluateTransition(float dt, Rigidbody rb, CarPhysicsInput inputData);
		public abstract void		Tick(float dt, Rigidbody rb, CarPhysicsInput inputData);
		public abstract void		Exit();
	}
}