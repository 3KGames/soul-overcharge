using Common.Runtime;
using Game.Enemies.Flocking;
using Game.Enemies.Vehicle;
using UnityEngine;

namespace Game.Enemies.Behavior
{
	/// <summary>
	/// Singleton-фабрика создания IEnemyBrain по ScriptableObject профиля префаба.
	/// </summary>
	public sealed class EnemyBrainFactory
	{
		private readonly PlayerTracker _playerTracker;
		private readonly NarrowingDetectionService _narrowing;
		private readonly FlockSystem _flockSystem;

		public EnemyBrainFactory(
			PlayerTracker playerTracker,
			NarrowingDetectionService narrowing,
			FlockSystem flockSystem = null)
		{
			_playerTracker = playerTracker;
			_narrowing = narrowing;
			_flockSystem = flockSystem;
		}

		public IEnemyBrain Create(EnemyBehaviorProfileSO profile, int flockAgentIndex = -1)
		{
			if (profile == null)
			{
				Debug.LogError("[EnemyBrainFactory] Behavior profile is null.");
				return null;
			}

			return profile.Archetype switch
			{
				EnemyArchetype.NonCombatRunner => new NonCombatRunnerBrain(profile, _narrowing, _playerTracker),
				
				_ => new NonCombatRunnerBrain(profile, _narrowing, _playerTracker)
			};
		}
	}
}