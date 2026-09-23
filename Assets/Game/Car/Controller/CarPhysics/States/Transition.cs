namespace Car.Controller.CarPhysics.States
{
	public interface ITransition
	{
		CarState NextState { get; }
		
		void ApplyTo(BaseCarState state);
	}
	
	public sealed class Transition<TPayload> : ITransition
	{
		public CarState NextState  { get; }
		private TPayload Payload    { get; }

		public Transition(CarState nextState, TPayload payload)
		{
			NextState = nextState;
			Payload   = payload;
		}

		void ITransition.ApplyTo(BaseCarState state)
		{
			if (state is ITransitionPayload<TPayload> receiver)
				receiver.ApplyPayload(Payload);
		}
	}
	
	public interface ITransitionPayload<TPayload>
	{
		void ApplyPayload(TPayload payload);
	}
	
	public struct NoPayload { }
}