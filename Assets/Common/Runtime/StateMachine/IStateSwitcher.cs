namespace Common.Runtime.StateMachine
{
    public interface IStateSwitcher<TStateType>
    {
        void Switch(TStateType state);
    }
}
