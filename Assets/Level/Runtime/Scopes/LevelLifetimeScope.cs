using System;
using Car.Controller;
using Car.Controller.CarPhysics;
using Car.Controller.CarPhysics.States;
using Car.Gears;
using NaughtyAttributes;
using UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Level.Runtime.Scopes
{
    public class LevelLifetimeScope : LifetimeScope
    {
        [SerializeField] private LayerMask surfaceMask; //TODO: Remove form here, maybe has to be in RoadCheckService
		[Expandable]
		[SerializeField] private CarPhysicsData physicsData;
		[Expandable] 
		[SerializeField] private NitroData nitroData;
		[Expandable]
		[SerializeField] private GearDataRpm gearDataRpm;
		
        protected override void Configure(IContainerBuilder builder)
        {
            if (Parent == null)
            {
                Debug.LogError("LevelLifetimeScope: Parent container is null.");
                return;
            }
			
			// Scriptable Objects
			builder.RegisterInstance(physicsData);
			builder.RegisterInstance(nitroData);
			builder.RegisterInstance(gearDataRpm);

			builder.Register<TransmissionService>(Lifetime.Singleton);
			// Car controller
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
			builder.Register<CarService>(Lifetime.Singleton);
            builder.Register<CarPhysicsService>(Lifetime.Singleton);
			builder.Register<NitroService>(Lifetime.Singleton);
            builder.RegisterInstance(new RoadCheckService(surfaceMask)); //TODO: Fix this shit
            builder.RegisterComponentInHierarchy<CarController>();
			builder.RegisterComponentInHierarchy<DynamicCameraController>();
			// UI
            builder.RegisterComponentInHierarchy<GearDisplayUI>();
            builder.RegisterComponentInHierarchy<TachometerController>();
        }
    }
}