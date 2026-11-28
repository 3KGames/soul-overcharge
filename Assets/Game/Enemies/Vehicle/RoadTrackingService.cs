using Dreamteck.Splines;
using Game.Common.Runtime;
using UnityEngine;

namespace Game.Enemies.Vehicle
{
	public sealed class RoadTrackingService
	{
		private const float TransitionZoneMeters = 2.5f;
		private const float HysteresisSqrMargin = 0.25f;

		public void Initialize(EnemyVehicleModel model, IRoadPath startPath, int startLane = 0,
							   float startDistance = 0f)
		{
			model.CurrentPath = startPath;
			if (startPath == null) return;

			model.CurrentLane = startPath.LaneCount > 0 ? Mathf.Clamp(startLane, 0, startPath.LaneCount - 1) : 0;
			model.DistanceAlongPath = Mathf.Clamp(startDistance, 0f, startPath.Length);
			model.LateralOffset = startPath.GetLaneOffsetSafe(model.CurrentLane);
			model.TargetLateralOffset = model.LateralOffset;

			var ss = startPath.SampleAtDistance(model.DistanceAlongPath, Spline.Direction.Forward);
			ApplyBasis(model, in ss);
			model.Curvature = startPath.GetCurvatureAtDistance(model.DistanceAlongPath);

			model.LastValidPath = startPath;
			model.LastValidDistance = model.DistanceAlongPath;
			model.LastValidLateralOffset = model.LateralOffset;
		}
		
		public bool Step(EnemyVehicleModel model, Vector3 worldPos, out IRoadPath previousPath)
        {
            previousPath = model.CurrentPath;
            if (model.CurrentPath == null) return false;

            IRoadPath path = model.CurrentPath;
            SplineSample sample = path.GetClosestSample(worldPos);
            float len = path.Length;
            float curS = Mathf.Clamp((float)(sample.percent * len), 0f, len);
            float curDistSq = (worldPos - sample.position).sqrMagnitude;

            if (curS >= len - TransitionZoneMeters && path.TryGetNextPath(model.CurrentLane, out var nextPath))
            {
                SplineSample nextSample = nextPath.GetClosestSample(worldPos);
                float nextLen = nextPath.Length;
                float nextS = Mathf.Clamp((float)(nextSample.percent * nextLen), 0f, nextLen);
                float nextDistSq = (worldPos - nextSample.position).sqrMagnitude;

                bool closerToNext = nextDistSq + HysteresisSqrMargin < curDistSq;
                bool passedEnd = curS >= len - 0.05f && nextS > 0.05f;
                if (closerToNext || passedEnd)
                {
                    path = nextPath;
                    sample = nextSample;
                    curS = nextS;
                }
            }
            // Переход назад при ударе/отбрасывании (на конец предыдущего сегмента, не в 0!)
            else if (curS <= TransitionZoneMeters && path.PreviousPath != null)
            {
                IRoadPath prevPath = path.PreviousPath;
                SplineSample prevSample = prevPath.GetClosestSample(worldPos);
                float prevLen = prevPath.Length;
                float prevS = Mathf.Clamp((float)(prevSample.percent * prevLen), 0f, prevLen);
                float prevDistSq = (worldPos - prevSample.position).sqrMagnitude;

                bool closerToPrev = prevDistSq + HysteresisSqrMargin < curDistSq;
                bool passedStart = curS <= 0.05f && prevS < prevLen - 0.05f;
                if (closerToPrev || passedStart)
                {
                    path = prevPath;
                    sample = prevSample;
                    curS = prevS;
                }
            }

            model.CurrentPath = path;
            model.DistanceAlongPath = curS;
            ApplyBasis(model, in sample);

            model.LateralOffset = Vector3.Dot(worldPos - model.ProjectedPosition, model.RoadRight);
            model.Curvature = path.GetCurvatureAtDistance(curS);
            model.CurrentLane = path.GetLaneAtPosition(worldPos);

            model.LastValidPath = path;
            model.LastValidDistance = curS;
            model.LastValidLateralOffset = path.ClampLateralOffset(curS, model.LateralOffset, 0.5f);

            return path != previousPath;
        }

        public bool TryGetRecoveryPose(EnemyVehicleModel model, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            IRoadPath targetPath = model.LastValidPath ?? model.CurrentPath;
            if (targetPath == null) return false;

            float s = Mathf.Clamp(model.LastValidDistance, 0f, targetPath.Length);
            SplineSample ss = targetPath.SampleAtDistance(s, Spline.Direction.Forward);
            Vector3 up = ss.up.sqrMagnitude > 1e-6f ? ss.up.normalized : Vector3.up;
            Vector3 fwd = ss.forward.sqrMagnitude > 1e-6f ? ss.forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(up, fwd).normalized;

            float safeOffset = targetPath.ClampLateralOffset(s, model.LastValidLateralOffset, 1.0f);
            position = ss.position + right * safeOffset + up * 0.35f;
            rotation = Quaternion.LookRotation(fwd, up);
            return true;
        }
		
		private static void ApplyBasis(EnemyVehicleModel model, in SplineSample sample)
		{
			model.ProjectedPosition = sample.position;
			model.RoadUp = sample.up.sqrMagnitude > 1e-6f ? sample.up.normalized : Vector3.up;
			model.RoadForward = sample.forward.sqrMagnitude > 1e-6f ? sample.forward.normalized : Vector3.forward;
			model.RoadRight = Vector3.Cross(model.RoadUp, model.RoadForward).normalized;
		}
	}
}