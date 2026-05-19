using Common.Runtime.StateMachine;
using VContainer;

namespace Enemies.Biker.Runtime
{
	public class BikerStateFactory : IStateFactory<BikerStateType>
	{
		private readonly IObjectResolver _resolver;

		public BikerStateFactory(IObjectResolver resolver)
		{
			_resolver = resolver;
		}

		public IState<BikerStateType> Create(BikerStateType state)
		{
			return state switch
			{
				BikerStateType.Chase => _resolver.Resolve<BikerChaseState>(),
				BikerStateType.Parallel => _resolver.Resolve<BikerParallelState>(),
				BikerStateType.Attack => _resolver.Resolve<BikerAttackState>(),
				BikerStateType.Dead => _resolver.Resolve<BikerDeadState>(),
				_ => throw new System.ArgumentOutOfRangeException(nameof(state))
			};
		}
	}
}