namespace Car.Gears
{
    public interface IGear
    {
        float MaxSteerAngle { get; }
        float EvaluateAcceleration(float speed /*или rpm*/);
    }
}