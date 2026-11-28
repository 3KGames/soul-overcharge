using UnityEngine;

namespace Game.Enemies.Behavior
{
	public enum EnemyArchetype
	{
		NonCombatRunner,
		Rammer,
		Flock
	}

	[CreateAssetMenu(fileName = "EnemyBehaviorProfile", menuName = "Enemies/Behavior Profile")]
	public sealed class EnemyBehaviorProfileSO : ScriptableObject
	{
		public EnemyArchetype Archetype = EnemyArchetype.NonCombatRunner;

		[Header("Speed Behavior")]
		[Min(0f)] public float CruiseSpeed = 30f;
		[Min(0f)] public float MinSpeed = 8f;
		[Min(1f)] public float MaxSpeed = 55f;
		public bool MatchPlayerSpeed = true;
		public float TargetPlayerGapMeters = 10f;
		[Min(0f)] public float GapKp = 0.6f;
		[Range(0.01f, 1f)] public float PlayerSpeedEmaAlpha = 0.25f;
		[Min(0f)] public float PlayerSpeedPredictionTime = 0.4f;
		[Range(0f, 1f)] public float PredictionWeight = 0.4f;
		[Range(0f, 1f)] public float CruiseWeight = 0f; // 0 = только игрок, 1 = только CruiseSpeed

		[Header("Lane Behavior")]
		[Min(2f)] public float NarrowingLookaheadMin = 16f;
		[Min(0f)] public float NarrowingLookaheadTime = 1.5f;
		[Min(0f)] public float RandomLaneChangeLambda = 0.25f;
		[Min(0f)] public float LaneChangeCooldown = 2.5f;
		[Range(0f, 2f)] public float FlockOffsetWeight = 0.5f;

		[Min(1f)] public float RamTriggerDistance = 6f;
		[Min(0.2f)] public float RamCooldown = 1.8f;
		[Min(0f)] public float RamImpulse = 4500f;

		public bool EnableObstacleAvoidance = false;
		[Min(5f)] public float AvoidanceLookahead = 18f;
		[Min(0.5f)] public float AvoidanceBandHalfWidth = 2.0f;
		[Min(0.1f)] public float AvoidanceHoldTime = 0.8f;
	}
}