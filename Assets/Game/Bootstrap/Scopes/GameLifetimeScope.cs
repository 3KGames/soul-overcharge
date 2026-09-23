using Level.Runtime.States;
using VContainer;
using VContainer.Unity;
using Common.Runtime.StateMachine;
using UnityEngine.InputSystem;

namespace Level.Runtime.Scopes
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<InputAction>(Lifetime.Singleton)
                .AsSelf();
            
            builder.Register<SceneLoader>(Lifetime.Singleton);
            builder.Register<GameStateFactory>(Lifetime.Singleton)
                .As<IStateFactory<GameState>>();
            builder.RegisterEntryPoint<GameFlowStateMachine>()
                .As<IStateSwitcher<GameState>>();
            builder.Register<LoadingState>(Lifetime.Singleton);
            builder.Register<PlayState>(Lifetime.Singleton);
        }
    }
}