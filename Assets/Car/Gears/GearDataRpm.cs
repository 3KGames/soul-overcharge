using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Serialization;

namespace Car.Gears
{
    [CreateAssetMenu(menuName = "ArcadeCar/GearData-RPM")]
	public class GearDataRpm : GearDataBase, ISerializationCallbackReceiver
    {
		[CurveRange(0, 0, 1f, 1f), Tooltip("X - RPM, Y - Acceleration")]
		[SerializeField] private	AnimationCurve	accelerationCurve;
		[Range(1f, 1000f)]
		[SerializeField] private	float			speedToRpmFactor = 300f;
		[Range(1f, 5000f)]
		[SerializeField] private	float			rpmIdle = 1000f;
		[Range(5000f, 10000f)]
		[SerializeField] private	float			rpmRedline = 7000f;
		[Range(0f, 0.03f)]
		[SerializeField] private	float			engineBrakeFactor = 0.02f;
		[Range(0f, 0.1f)]
		[SerializeField] private	float			lowRpmBrakeFactor = 0.02f;
		[Range(0f, 50f)]
        [SerializeField] private	float			accelerationModifier;
		[Range(-10f, 10f), Tooltip("Desmos: m")] 
		[SerializeField] private	float			speedShift = 1.3f;
        [SerializeField] private	Gear[]			gears;
		

		public override int   GearsCount          => gears.Length;
		public override IGear GetGear(int index)  => gears[index];


		public void OnBeforeSerialize() { }
		
		public void OnAfterDeserialize()
		{
			if (gears == null || gears.Length == 0)
				return;
			
			foreach (var gear in gears)
				gear.SetOwner(this);
		}
		

		[System.Serializable]
		private class Gear : IGear
        {
			[Range(0f, 10f)]
            [SerializeField] private	float			gearRatio;
			[Range(0f, 180f)]
            [SerializeField] private	float			maxSteerAngle;
            
			// RpmGearData
            [NonSerialized]  private	GearDataRpm		_owner;
            
			public float MaxSteerAngle => maxSteerAngle;

			public float EvaluateRpm(float speed)
			{
				float clampedSpeed = speed < 0 ? _owner.speedShift : speed + _owner.speedShift;
				return clampedSpeed * gearRatio * _owner.speedToRpmFactor;
			}
			
            public float EvaluateAcceleration(float speed)
			{
				float clampedSpeed = speed < 0 ? _owner.speedShift : speed + _owner.speedShift;
                float rpm = clampedSpeed * gearRatio * _owner.speedToRpmFactor;

                // Normalize RPM
                float normalizedRpm = Mathf.InverseLerp(_owner.rpmIdle, _owner.rpmRedline, rpm);
                normalizedRpm = Mathf.Clamp01(normalizedRpm);

                // Get torque from curve (0..1)
                float torque = _owner.accelerationCurve.Evaluate(normalizedRpm);

                // Calculate acceleration
                float accel = torque * gearRatio * _owner.accelerationModifier;
				
				// Too low rpm
				if (rpm < _owner.rpmIdle)
				{
					float lackRpm   = _owner.rpmIdle - rpm;                    // сколько «не хватает» до холостых
					float lugPenalty = lackRpm * _owner.lowRpmBrakeFactor;     // коэффициент подбираете в инспекторе
					accel -= lugPenalty;                                       // отрицательное ускорение
					if (accel < 0)
						accel = 0;
				}
				
                // Rpm overshoot
                if (rpm > _owner.rpmRedline)
                {
                    float excessRpm = rpm - _owner.rpmRedline;
                    float brake = -excessRpm * _owner.engineBrakeFactor;
                    accel += brake;
                }
                //Debug.Log($"Speed: {speed};   Rpm: {rpm};   Accel: {accel}");
                return accel;
            }
			
			public void SetOwner(GearDataRpm owner) => _owner = owner;
        }
    }
}