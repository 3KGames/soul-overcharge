using System;
using Car.Controller;
using Car.Controller.CarPhysics;
using Car.Controller.CarPhysics.States;
using Car.Gears;
using Car.Souls.Services;
using Car.Health.Data;
using Common.Runtime;
using Common.Runtime.StateMachine;
using Car.Health;
using Car.Health.Services;
using Car.Souls.Data;
using Car.UI;
using Enemies;
using Game.Level.Runtime;
using Level.Runtime.States;
using NaughtyAttributes;
using UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Level.Runtime.Scopes
{
    public class LevelLifetimeScope : LifetimeScope
    {
        [SerializeField] private LayerMask roadMask;
        [SerializeField] private LayerMask offroadMask;

        [Expandable]
        [SerializeField] private CarPhysicsData physicsData;
        [Expandable]
        [SerializeField] private NitroData nitroData;
        [Expandable]
        [SerializeField] private GearDataRpm gearDataRpm;

        [Header("Souls / Health")]
        [SerializeField] private SoulData   soulData;
        [SerializeField] private HealthData healthData;

        protected override void Configure(IContainerBuilder builder)
        {
            // Данные
            builder.RegisterInstance(physicsData);
            builder.RegisterInstance(nitroData);
            builder.RegisterInstance(gearDataRpm);
            builder.RegisterInstance(new RoadCheckService(roadMask, offroadMask));
            builder.RegisterInstance(soulData);
            builder.RegisterInstance(healthData);

            // Ввод
            builder.Register<InputService>(Lifetime.Singleton)
                .AsSelf()
                .As<IInitializable>()
                .As<IDisposable>();

            // Машина
            builder.Register<TransmissionService>(Lifetime.Singleton);
            builder.Register<DriveCarState>(Lifetime.Singleton)
                .AsSelf()
                .As<BaseCarState>();
            builder.Register<DriftCarState>(Lifetime.Singleton)
                .AsSelf()
                .As<BaseCarState>();
            builder.Register<CarPhysicsService>(Lifetime.Singleton);
            builder.Register<NitroService>(Lifetime.Singleton);
            builder.Register<CarService>(Lifetime.Singleton);

            // Души и здоровье
            builder.Register<SoulService>(Lifetime.Singleton);
            builder.Register<SoulDrainService>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();
            builder.Register<HealthService>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

            // Аудио
            builder.Register<AudioVolumeService>(Lifetime.Singleton);

            // Game Over — после InputService и AudioVolumeService
            builder.Register<GameOverService>(Lifetime.Singleton);

            // Таймер
            builder.Register<GameTimer>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

            // Прочие сервисы
            builder.Register<TargetRegistry>(Lifetime.Singleton);
            builder.Register<PlayerTracker>(Lifetime.Singleton);

            // Компоненты на сцене
            builder.RegisterComponentInHierarchy<CarController>();
            builder.RegisterComponentInHierarchy<DynamicCameraController>();
            builder.RegisterComponentInHierarchy<GearDisplayUI>();
            builder.RegisterComponentInHierarchy<TachometerController>();
            builder.RegisterComponentInHierarchy<DualBarController>();
            builder.RegisterComponentInHierarchy<NitroBarController>();
            builder.RegisterComponentInHierarchy<PlayerInitializer>();
            builder.RegisterComponentInHierarchy<SettingsUI>();
            builder.RegisterComponentInHierarchy<CarHealthBridge>();
            builder.RegisterComponentInHierarchy<SoulDrainEffect>();
            builder.RegisterComponentInHierarchy<TimerUI>();
            builder.RegisterComponentInHierarchy<RoadGenerator>();
            builder.RegisterComponentInHierarchy<BossController>();
            builder.RegisterComponentInHierarchy<GameOverUI>();
        }
    }
}