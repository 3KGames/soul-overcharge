using NaughtyAttributes;
using UnityEngine;

namespace Car.Gears
{
    [CreateAssetMenu(menuName = "ArcadeCar/GearData-Speed")]
    public class SpeedGearData : GearDataBase
    {
        [SerializeField] private Gear[] gears;
        public override int  GearsCount          => gears.Length;
        public override IGear GetGear(int index) => gears[index];

        [System.Serializable]
        private class Gear : IGear
        {
            [MinMaxSlider(0.0f, 100.0f), 
             SerializeField] private Vector2 preferredSpeed;
            [CurveRange(-1f, -0.5f, 2f, 2f),
             SerializeField] private AnimationCurve accelerationCurve;
            [SerializeField] private float maxSteerAngle;

            public float MaxSteerAngle => maxSteerAngle;
            public float MinPrefSpeed => preferredSpeed.x;
            public float MaxPrefSpeed => preferredSpeed.y;

            public float EvaluateAcceleration(float speed)
            {
                float t = (speed - MinPrefSpeed) / (MaxPrefSpeed - MinPrefSpeed); // Inverse lerp without clamping
                float accel = accelerationCurve.Evaluate(t) * 5f;
                //Debug.Log($"Speed: {speed};   T: {t};   Accel: {accel}");
                return accel;
            }
        }
    }
}