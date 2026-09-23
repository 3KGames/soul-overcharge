using System.Collections.Generic;
using VContainer.Unity;
using Common.Runtime.StateMachine;

namespace Level.Runtime
{    
    public enum GameState { Loading, Play, Completed }

    public sealed class GameFlowStateMachine : IStartable, IStateSwitcher<GameState>
    {
        readonly IStateFactory<GameState> _factory;
        readonly Dictionary<GameState, IState<GameState>> _states = new();
        GameState _current;

        public GameFlowStateMachine(IStateFactory<GameState> factory)
        {
            _factory = factory;
        }

        public void Start() => Switch(GameState.Loading);

        public void Switch(GameState next)
        { 
            if (_states.TryGetValue(_current, out var currentState))
                currentState.Exit();
            
            if (!_states.ContainsKey(next))
            {
                var nextState = _factory.Create(next);
                _states[next] = nextState;
            }
            
            _current = next;
            _states[_current].Enter();
        }
    }
}