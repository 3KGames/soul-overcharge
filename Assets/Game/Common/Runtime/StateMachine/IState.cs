namespace Common.Runtime.StateMachine
{
    public interface IState<TStateType>
    {
        TStateType Kind { get; }
        void Enter(); 
        void Exit();
    }
}
