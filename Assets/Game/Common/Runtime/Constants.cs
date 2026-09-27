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
	}
}