using System;
using Car.Controller;
using Car.Controller.CarPhysics;
using Car.Controller.CarPhysics.Drivetrain;
using Car.Controller.CarPhysics.Engine;
using Car.Controller.CarPhysics.States;
using Car.Controller.CarPhysics.Transmission;
using Car.Souls.Services;
using Car.Health.Data;
using Common.Runtime;
using Common.Runtime.StateMachine;
using Car.Health;
using Car.Health.Services;
using Car.Souls.Data;
using Car.UI;
using Enemies;
using Game.Car.VFX;
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

        [Header("Car physics")]
        [SerializeField] private CarPhysicsData physicsData;
        [SerializeField] private EngineSO       engineSO;
        [SerializeField] private TransmissionSO transmissionSO;
        [SerializeField] private DrivetrainSO   drivetrainSO;

        [Expandable]
        [SerializeField] private NitroData nitroData;

        [Header("Souls / Health")]
        [SerializeField] private SoulData   soulData;
        [SerializeField] private HealthData healthData;

        protected override void Configure(IContainerBuilder builder)
        {
            // Данные
            builder.RegisterInstance(physicsData);
            builder.RegisterInstance(nitroData);
            builder.RegisterInstance(ResolveOrDefault(engineSO, nameof(engineSO)));
            builder.RegisterInstance(ResolveOrDefault(transmissionSO, nameof(transmissionSO)));
            builder.RegisterInstance(ResolveOrDefault(drivetrainSO, nameof(drivetrainSO)));
            builder.RegisterInstance(new RoadCheckService(roadMask, offroadMask));
            builder.RegisterInstance(soulData);
            builder.RegisterInstance(healthData);

            // Ввод
            builder.Register<InputService>(Lifetime.Singleton)
                .AsSelf()
                .As<IInitializable>()
                .As<IDisposable>();

            // Модели читают настройки из SO один раз при создании
            builder.Register<EngineModel>(Lifetime.Singleton);
            builder.Register<TransmissionModel>(Lifetime.Singleton);
            builder.Register<DrivetrainModel>(Lifetime.Singleton);

            // Машина
            builder.Register<EngineService>(Lifetime.Singleton);
            builder.Register<DrivetrainService>(Lifetime.Singleton);
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
			builder.RegisterComponentInHierarchy<WheelVfxController>();
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

        /// <summary>
        /// Если ассет не назначен в инспекторе, создаём временный экземпляр с
        /// дефолтными значениями, чтобы уровень не падал на пустом SO. Факт
        /// подмены логируется — молча брать дефолты нельзя, иначе настройки
        /// будут выглядеть применёнными, хотя их никто не задавал.
        /// </summary>
        private T ResolveOrDefault<T>(T current, string fieldName) where T : ScriptableObject
        {
            if (current != null) return current;

            T created = ScriptableObject.CreateInstance<T>();

            Debug.LogWarning(
                $"[LevelLifetimeScope] {typeof(T).Name} ({fieldName}) не назначен в инспекторе. " +
                "Используется временный экземпляр с дефолтными значениями: настройки " +
                "не сохраняются между запусками. Создайте ассет и назначьте его.",
                this);

            return created;
        }
    }
}
