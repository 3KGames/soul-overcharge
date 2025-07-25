using Level.Runtime.States;
using VContainer;
using VContainer.Unity;
using Common.Runtime.StateMachine;

namespace Level.Runtime.Scopes
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<SceneLoader>(Lifetime.Singleton);
            builder.Register<GameStateFactory>(Lifetime.Singleton)
                .As<IGameStateFactory<GameState>>();
            builder.RegisterEntryPoint<GameFlowStateMachine>()
                .As<IStateSwitcher<GameState>>();
            builder.Register<LoadingState>(Lifetime.Singleton);
            builder.Register<PlayState>(Lifetime.Singleton);
        }
    }
}