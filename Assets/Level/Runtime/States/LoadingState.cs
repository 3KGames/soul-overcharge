using Common.Runtime.StateMachine;

namespace Level.Runtime.States
{
    /*public static class LevelLoadContext
    {
        public static LevelData levelData;
    }*/
    
    public sealed class LoadingState : IGameState<GameState>
    {
        readonly IStateSwitcher<GameState> _fsm;
        readonly SceneLoader _loader;

        public LoadingState(IStateSwitcher<GameState> fsm, SceneLoader loader)
        {
            _fsm = fsm;
            _loader = loader;
        }
        public GameState Kind => GameState.Loading;
        public async void Enter()
        {            
            await _loader.LoadLevel();

            _fsm.Switch(GameState.Play);
        }
        public void Exit() { }
    }
}