using UnityEngine;
using NaughtyAttributes;

namespace Car.Gears
{
    [CreateAssetMenu(menuName = "ArcadeCar/GearData")]
    public class GearData : ScriptableObject
    {
        private const float MaxSpeedSliderVal = 100f;
        
        public Gear[] gears;

        [System.Serializable]
        public struct Gear
        {
            [MinMaxSlider(0.0f, 100.0f), 
             SerializeField]
            private Vector2 preferredSpeed;             // units per second
            
            [CurveRange(-1f, -0.5f, 2f, 2f)]
            public AnimationCurve accelerationCurve;    // X: 0..1 speed fraction, Y: acceleration multiplier
            
            public float maxSteerAngle;                 // degrees
            
            public float MinPrefSpeed => preferredSpeed.x;
            public float MaxPrefSpeed => preferredSpeed.y;
        }
    }
}