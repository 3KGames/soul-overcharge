namespace Common.Runtime.StateMachine
{
    public interface IStateSwitcher<StateType>
    {
        void Switch(StateType state);
    }
}
