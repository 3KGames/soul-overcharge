using Dreamteck.Splines;
using UnityEngine;

namespace Game.Common.Runtime
{
	public readonly struct RoadSample
	{
		public readonly Vector3 Position;
		public readonly Vector3 Tangent;
		public readonly Vector3 Up;
		public readonly Vector3 Right;
		public readonly float DistanceAlong;
		public readonly IRoadPath Path;
		public readonly float Curvature;

		public RoadSample(
			Vector3 position,
			Vector3 tangent,
			Vector3 up,
			float distanceAlong,
			IRoadPath path,
			float curvature)
		{
			Position = position;
			Tangent = tangent.normalized;
			Up = up.normalized;
			Right = Vector3.Cross(Up, Tangent);
			DistanceAlong = distanceAlong;
			Path = path;
			Curvature = curvature;
		}

		public RoadSample(in SplineSample sample, float distanceAlong, IRoadPath path, float curvature)
			: this(sample.position, sample.forward, sample.up, distanceAlong, path, curvature)
		{
		}


	}
	
	public static class IRoadPathExtensions
	{
		private const float CurvatureSampleStep = 0.5f;
		private const int MaxSegmentHops = 4;

		// TODO: Rewrite, always true function
		public static bool TrySampleAtDistanceWithLookahead(
			this IRoadPath path,
			float fromS,
			float lookahead,
			int currentLane,
			out RoadSample sample,
			out bool crossedSegment)
		{
			sample = default;
			crossedSegment = false;
			if (path == null)
				return false;

			IRoadPath cursor = path;
			float remainingS = fromS + lookahead;
			int lane = currentLane;

			for (int hop = 0; hop < MaxSegmentHops; hop++)
			{
				float len = cursor.Length;
				if (remainingS <= len)
				{
					var ss = cursor.SampleAtDistance(remainingS, Spline.Direction.Forward);
					sample = new RoadSample(in ss, remainingS, cursor, GetCurvatureAtDistance(cursor, remainingS));
					return true;
				}

				if (!cursor.TryGetNextPath(lane, out var next))
				{
					var ssEnd = cursor.SampleAtDistance(len, Spline.Direction.Forward);
					sample = new RoadSample(in ssEnd, len, cursor, GetCurvatureAtDistance(cursor, len));
					return true;
				}

				remainingS -= len;
				cursor = next;
				lane = cursor.LaneCount > 0 ? Mathf.Clamp(lane, 0, cursor.LaneCount - 1) : 0;
				crossedSegment = true;
			}

			float finalS = Mathf.Clamp(remainingS, 0f, cursor.Length);
			var finalSample = cursor.SampleAtDistance(finalS, Spline.Direction.Forward);
			sample = new RoadSample(in finalSample, finalS, cursor, GetCurvatureAtDistance(cursor, finalS));
			return true;
		}

		public static float GetCurvatureAtDistance(this IRoadPath path, float s)
		{
			if (path == null)
				return 0f;

			float len = path.Length;
			float s0 = Mathf.Max(0f, s - CurvatureSampleStep);
			float s1 = Mathf.Min(len, s + CurvatureSampleStep);
			float step = s1 - s0;

			if (step < 1e-4f)
				return 0f;

			var a = path.SampleAtDistance(s0, Spline.Direction.Forward);
			var b = path.SampleAtDistance(s1, Spline.Direction.Forward);
			Vector3 dT = b.forward - a.forward;
			// TODO: Check formula
			Vector3 right = Vector3.Cross(b.up, b.forward).normalized;
			return Vector3.Dot(dT, right) / step;
		}

		public static float GetLaneOffsetSafe(this IRoadPath path, int laneIndex)
		{
			if (path == null || path.LaneCount <= 0)
				return 0f;

			return path.GetLaneOffset(Mathf.Clamp(laneIndex, 0, path.LaneCount - 1));
		}

		public static float ClampLateralOffset(this IRoadPath path, float s, float lateralOffsetMeters, float edgeMargin = 0.6f)
		{
			if (path == null)
				return lateralOffsetMeters;

			var (leftWidth, rightWidth) = path.GetRoadWidthAt(Mathf.Clamp(s, 0f, path.Length));
			float minD = -Mathf.Abs(leftWidth) + edgeMargin;
			float maxD = Mathf.Abs(rightWidth) - edgeMargin;
			if (minD > maxD) return 0.5f * (minD + maxD);

			return Mathf.Clamp(lateralOffsetMeters, minD, maxD);
		}
	}
}