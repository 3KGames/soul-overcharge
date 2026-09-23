namespace Common.Runtime.StateMachine
{
    public interface IStateFactory<StateType>
    {
        IState<StateType> Create(StateType state);
    }
}
