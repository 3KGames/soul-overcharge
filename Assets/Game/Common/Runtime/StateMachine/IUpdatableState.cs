namespace Common.Runtime.StateMachine
{
	public interface IUpdatableState<TStateType> : IState<TStateType>
	{
		void Update();
	}
}