namespace Common.Runtime.StateMachine
{
    public interface IGameState<StateType>
    {
        StateType Kind { get; }
        void Enter(); 
        void Exit();
    }
}
