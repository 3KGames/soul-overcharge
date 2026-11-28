using Game.Common.Runtime;
using Game.Enemies.Vehicle;
using UnityEngine;

namespace Game.Enemies.Behavior
{
	/// <summary>
	/// Stateless-сервис детекции сужения дороги с cross-segment lookahead.
	/// </summary>
	public sealed class NarrowingDetectionService
	{
		public bool TryFindSafeLane(
			EnemyVehicleModel model,
			float currentForwardSpeed,
			float minLookahead,
			float lookaheadTime,
			int desiredLane,
			out int safeLane)
		{
			safeLane = desiredLane;
			if (!model.HasRoad) return false;

			float dLook = Mathf.Max(minLookahead, Mathf.Max(0f, currentForwardSpeed) * lookaheadTime);
			if (!model.CurrentPath.TrySampleAtDistanceWithLookahead(
					model.DistanceAlongPath,
					dLook,
					desiredLane,
					out var sample,
					out _))
			{
				return false;
			}

			IRoadPath targetPath = sample.Path;
			int laneCount = targetPath.LaneCount;
			if (laneCount <= 0) return false;

			int checkLane = Mathf.Clamp(desiredLane, 0, laneCount - 1);
			if (targetPath.IsLaneActiveAt(checkLane, sample.DistanceAlong))
				return false;

			// Ищем ближайшую активную полосу от checkLane
			int bestLane = -1;
			int bestDist = int.MaxValue;
			for (int i = 0; i < laneCount; i++)
			{
				if (!targetPath.IsLaneActiveAt(i, sample.DistanceAlong)) continue;
				int dist = Mathf.Abs(i - checkLane);
				if (dist < bestDist)
				{
					bestDist = dist;
					bestLane = i;
				}
			}

			if (bestLane < 0) return false;

			safeLane = bestLane;
			return true;
		}
	}
}