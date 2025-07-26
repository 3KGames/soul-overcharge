using System;
using Car.Controller;
using UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Level.Runtime.Scopes
{
    public class LevelLifetimeScope : LifetimeScope
    {
        [SerializeField] private LayerMask surfaceMask; //TODO: Remove form here, maybe has to be in RoadCheckService
        
        protected override void Configure(IContainerBuilder builder)
        {
            if (Parent == null)
            {
                Debug.LogError("LevelLifetimeScope: Parent container is null.");
                return;
            }

            builder.Register<InputService>(Lifetime.Singleton)
                .AsSelf()
                .As<IInitializable>()
                .As<IDisposable>();
            builder.Register<CarPhysicsService>(Lifetime.Singleton);
            builder.RegisterInstance(new RoadCheckService(surfaceMask));
            builder.RegisterComponentInHierarchy<CarController>();
            builder.RegisterComponentInHierarchy<GearDisplayUI>();

            // Bootstrap static tiles once the scene is ready
            //builder.RegisterEntryPoint<LevelBootstrap>(Lifetime.Scoped);

        }
    }
}