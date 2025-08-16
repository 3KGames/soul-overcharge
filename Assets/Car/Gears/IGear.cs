namespace Car.Gears
{
    public interface IGear
    {
        float MaxSteerAngle { get; }
        float EvaluateRpm(float speed);
        float EvaluateAcceleration(float speed /*или rpm*/);
    }
}