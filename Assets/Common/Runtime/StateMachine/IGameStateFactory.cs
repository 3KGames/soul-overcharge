namespace Common.Runtime.StateMachine
{
    public interface IGameStateFactory<StateType>
    {
        IGameState<StateType> Create(StateType state);
    }
}
