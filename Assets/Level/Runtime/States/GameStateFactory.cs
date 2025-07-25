using System;
using VContainer;
using Common.Runtime.StateMachine;

namespace Level.Runtime.States
{    
    public class GameStateFactory : IGameStateFactory<GameState>
    {
        private readonly IObjectResolver _resolver;

        public GameStateFactory(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public IGameState<GameState> Create(GameState state)
        {
            return state switch
            {
                GameState.Loading => _resolver.Resolve<LoadingState>(),
                GameState.Play => _resolver.Resolve<PlayState>(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}