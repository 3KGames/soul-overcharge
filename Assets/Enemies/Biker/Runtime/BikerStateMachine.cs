using Common.Runtime.StateMachine;
using VContainer;

namespace Enemies.Biker.Runtime
{
	public class BikerStateMachine : IStateSwitcher<BikerStateType>
	{
		private IUpdatableState<BikerStateType> _currentState;
		private readonly IStateFactory<BikerStateType> _factory;

		public BikerStateMachine(IStateFactory<BikerStateType> factory)
		{
			_factory = factory;
		}

		public void Switch(BikerStateType stateType)
		{
			_currentState?.Exit();
			_currentState = _factory.Create(stateType) as IUpdatableState<BikerStateType>;
			_currentState?.Enter();
		}

		public void Update()
		{
			_currentState?.Update();
		}
	}
}