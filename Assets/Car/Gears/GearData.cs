using UnityEngine;

namespace Car.Gears
{
    [CreateAssetMenu(menuName = "ArcadeCar/GearData")]
    public class GearData : ScriptableObject
    {
        public Gear[] gears;

        [System.Serializable]
        public struct Gear
        {
            public AnimationCurve accelerationCurve;    // X: 0..1 speed fraction, Y: acceleration multiplier
            public float maxSpeed;                      // units per second
            public float maxSteerAngle;                 // degrees
        }
    }
}