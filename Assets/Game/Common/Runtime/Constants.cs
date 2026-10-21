using UnityEngine;

namespace Common.Runtime
{
    public static class Consts
	{
		public const float AirPressure = 1.225f;
		
		// TODO: Move from here
		public static float AirDragForce(float coefficient, float area, float velocity)
		{
			return 0.5f * AirPressure * coefficient * area * velocity * velocity;
		}
		
		public static Vector3 AirDragForceLocal(
			float cdLateral,
			float cdVertical,
			float cdLongitudinal,
			float area,
			Vector3 localVelocity)
		{
			return new Vector3(
				-DragComponent(cdLateral,      area, localVelocity.x),
				-DragComponent(cdVertical,     area, localVelocity.y),
				-DragComponent(cdLongitudinal, area, localVelocity.z));
		}

		private static float DragComponent(float coefficient, float area, float velocity)
		{
			return 0.5f * AirPressure * coefficient * area * Mathf.Abs(velocity) * velocity;
		}
	}
}
