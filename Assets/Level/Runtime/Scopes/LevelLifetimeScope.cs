using System;
using Car.Controller;
using Car.Controller.CarPhysics;
using Car.Controller.CarPhysics.States;
using Car.Gears;
using Car.Souls.Data;
using Car.Souls.Services; 
using Car.UI;
using Car.Health.Data;
using Car.Health.Services;
using Enemies;
using NaughtyAttributes;
using UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Level.Runtime.Scopes
{
    public class LevelLifetimeScope : LifetimeScope
    {
        [SerializeField] private LayerMask surfaceMask;
        [Expandable]
        [SerializeField] private CarPhysicsData physicsData;
        [Expandable]
        [SerializeField] private NitroData nitroData;
        [Expandable]
        [SerializeField] private GearDataRpm gearDataRpm;

        [SerializeField] private SoulData soulData;
        [SerializeField] private HealthData healthData;

        protected override void Configure(IContainerBuilder builder)
        {
            if (Parent == null)
            {
                Debug.LogError("LevelLifetimeScope: Parent container is null.");
                return;
            }

            // Physics & Car Data
            builder.RegisterInstance(physicsData);
            builder.RegisterInstance(nitroData);
            builder.RegisterInstance(gearDataRpm);
            builder.RegisterInstance(new RoadCheckService(surfaceMask));

            // Core Services & States
            builder.Register<TransmissionService>(Lifetime.Singleton);
            builder.Register<CarService>(Lifetime.Singleton);
            builder.Register<CarPhysicsService>(Lifetime.Singleton);
            builder.Register<NitroService>(Lifetime.Singleton);

            builder.Register<InputService>(Lifetime.Singleton)
                .AsSelf()
                .As<IInitializable>()
                .As<IDisposable>();

            builder.Register<DriveCarState>(Lifetime.Singleton)
                .AsSelf()
                .As<BaseCarState>();
            builder.Register<DriftCarState>(Lifetime.Singleton)
                .AsSelf()
                .As<BaseCarState>();

            // Soul & Health Systems
            builder.RegisterInstance(soulData);
            builder.RegisterInstance(healthData);

            builder.Register<SoulService>(Lifetime.Singleton);
            builder.Register<SoulDrainService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<HealthService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            // Combat & Target Systems
            builder.Register<TargetRegistry>(Lifetime.Singleton);

            // In-Hierarchy Components (MonoBehaviours)
            builder.RegisterComponentInHierarchy<CarController>();
            builder.RegisterComponentInHierarchy<DynamicCameraController>();
            builder.RegisterComponentInHierarchy<GearDisplayUI>();
            builder.RegisterComponentInHierarchy<TachometerController>();   
            builder.RegisterComponentInHierarchy<DebugSoulHealthTester>();
            builder.RegisterComponentInHierarchy<DualBarController>();
            builder.RegisterComponentInHierarchy<EnemyHealth>();

            /*builder.RegisterBuildCallback(resolver =>
            {
                resolver.InjectGameObject(gameObject);
            });*/
        }
    }
}