using Common.Runtime.StateMachine;

namespace Level.Runtime.States
{
    public sealed class PlayState : IGameState<GameState>
    {
        public GameState Kind => GameState.Play;
        public void Enter() { /* здесь может стартовать таймер уровня */ }
        public void Exit()  { }
    }
}