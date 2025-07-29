using NaughtyAttributes;
using UnityEngine;

namespace Car.Gears
{
    [CreateAssetMenu(menuName = "ArcadeCar/GearData-Speed")]
    public class GearDataSpeed : GearDataBase
    {
        [SerializeField]	private		Gear[]	gears;
		
        public override int  GearsCount          => gears.Length;
        public override IGear GetGear(int index) => gears[index];

		
        [System.Serializable]
        private class Gear : IGear
        {
            [CurveRange(-10f, -5f, 30f, 5f)]
			[SerializeField] private AnimationCurve accelerationCurve;
            [SerializeField] private float			maxSteerAngle;

            public float MaxSteerAngle	=> maxSteerAngle;

            public float EvaluateAcceleration(float speed)
            {
				return accelerationCurve.Evaluate(speed) * 5f;
            }
        }
    }
}