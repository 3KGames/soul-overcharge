using VContainer;
using VContainer.Unity;
using Common.Runtime.StateMachine;

namespace Enemies.Biker.Runtime
{
	public class BikerLifetimeScope : LifetimeScope
	{
		public BikerContext bikerContext;
		public TrackableTarget trackableTarget;

		protected override void Configure(IContainerBuilder builder)
		{
			builder.RegisterComponent(bikerContext);
			builder.RegisterComponent(trackableTarget);

			builder.Register<BikerStateMachine>(Lifetime.Scoped)
				   .AsImplementedInterfaces()
				   .AsSelf();

			builder.Register<BikerStateFactory>(Lifetime.Scoped)
				   .AsImplementedInterfaces();

			builder.Register<BikerChaseState>(Lifetime.Scoped);
			builder.Register<BikerParallelState>(Lifetime.Scoped);
			builder.Register<BikerAttackState>(Lifetime.Scoped);
			builder.Register<BikerDeadState>(Lifetime.Scoped);
		}
	}
}