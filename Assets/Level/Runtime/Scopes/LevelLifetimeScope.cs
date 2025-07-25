using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Level.Runtime.Scopes
{
    public class LevelLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            if (Parent == null)
            {
                Debug.LogError("LevelLifetimeScope: Parent container is null.");
                return;
            }
            
            // Bootstrap static tiles once the scene is ready
            //builder.RegisterEntryPoint<LevelBootstrap>(Lifetime.Scoped);
            
        }
    }
}