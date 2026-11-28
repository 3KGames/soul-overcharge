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
using Game.Car.Controller.CarPhysics.Suspension;
using Game.Car.VFX.Wheel.Runtime;
using Game.Enemies.Audio;
using Game.Enemies.Behavior;
using Game.Enemies.Vehicle;
using Game.Level.Runtime;
using Game.Level.Runtime.Config;
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
        [Header("Road Generator")]
        [SerializeField] private RoadGridSettings roadGridSettings;
        [SerializeField] private Game.Enemies.Flocking.FlockSystem flockSystem;

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
            builder.RegisterInstance(physicsData);
            builder.RegisterInstance(nitroData);
            builder.RegisterInstance(ResolveOrDefault(engineSO, nameof(engineSO)));
            builder.RegisterInstance(ResolveOrDefault(transmissionSO, nameof(transmissionSO)));
            builder.RegisterInstance(ResolveOrDefault(drivetrainSO, nameof(drivetrainSO)));
            builder.RegisterInstance(new RoadCheckService(roadMask, offroadMask));
            builder.RegisterInstance(ResolveOrDefault(roadGridSettings, nameof(roadGridSettings)));
            builder.RegisterInstance(soulData);
            builder.RegisterInstance(healthData);

            builder.Register<InputService>(Lifetime.Singleton)
                .AsSelf()
                .As<IInitializable>()
                .As<IDisposable>();

            builder.Register<EngineModel>(Lifetime.Singleton);
            builder.Register<TransmissionModel>(Lifetime.Singleton);
            builder.Register<DrivetrainModel>(Lifetime.Singleton);

            builder.Register<EngineService>(Lifetime.Singleton);
            builder.Register<DrivetrainService>(Lifetime.Singleton);
            builder.Register<TransmissionService>(Lifetime.Singleton);
            builder.Register<DriveCarState>(Lifetime.Singleton)
                .AsSelf()
                .As<BaseCarState>();
            builder.Register<DriftCarState>(Lifetime.Singleton)
                .AsSelf()
                .As<BaseCarState>();
			builder.Register<DriftRecoveryCarState>(Lifetime.Singleton)
				.AsSelf()
				.As<BaseCarState>();
            builder.Register<CarPhysicsService>(Lifetime.Singleton);
            builder.Register<NitroService>(Lifetime.Singleton);
            builder.Register<CarService>(Lifetime.Singleton);

            builder.Register<SoulService>(Lifetime.Singleton);
            builder.Register<SoulDrainService>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();
            builder.Register<HealthService>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<AudioVolumeService>(Lifetime.Singleton);

            builder.Register<GameOverService>(Lifetime.Singleton);

            builder.Register<GameTimer>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register<TargetRegistry>(Lifetime.Singleton);
            builder.Register<PlayerTracker>(Lifetime.Singleton);
			
			builder.Register<RoadTrackingService>(Lifetime.Singleton);
			builder.Register<PurePursuitService>(Lifetime.Singleton);
			builder.Register<EnemyVehiclePhysicsService>(Lifetime.Singleton);
			builder.Register<EnemyDrivetrainService>(Lifetime.Singleton);
			builder.Register<NarrowingDetectionService>(Lifetime.Singleton);
			builder.Register<EnemyBrainFactory>(Lifetime.Singleton);

			builder.Register<AgentSpatialRegistry>(Lifetime.Singleton);

			builder.RegisterEntryPoint<EnemyAudioLodManager>(Lifetime.Singleton)
				.AsSelf()
				.WithParameter("maxActiveVoices", 8)
				.WithParameter("maxAudibleDistance", 120f);

            builder.RegisterComponentInHierarchy<CarController>();
            builder.RegisterComponentInHierarchy<SuspensionController>();
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
            
            var flock = flockSystem != null ? flockSystem : new GameObject("FlockSystem").AddComponent<Game.Enemies.Flocking.FlockSystem>();
            builder.RegisterInstance(flock);
        }

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
