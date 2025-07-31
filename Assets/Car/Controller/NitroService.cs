using UnityEngine;

namespace Car.Controller
{
	public class NitroService
	{
		private NitroData _nitroData;

		private float _timer;          
		private float _cooldown;       
		private float _torqueMultiplier = 1;

		public bool	IsBoosting => _timer > 0;

		public NitroService(NitroData nitroData)
		{
			_nitroData = nitroData;
		}
		
		public void DriftEnded(float duration)
		{
			_torqueMultiplier = duration switch
			{
				< 1.0f => _nitroData.MiniTurboSmall,
				< 2.0f => _nitroData.MiniTurboMedium,
				_	   => _nitroData.MiniTurboLarge
			};
			
			_timer    = _nitroData.Duration;   
		}

		public float GetTorqueMultiplier()
		{
			if (!IsBoosting) return 1f;

			float t = 1f - (_timer / _nitroData.Duration);
			return Mathf.Lerp(1f, _torqueMultiplier, _nitroData.Ramp.Evaluate(t));
		}

		public void Tick(float dt)
		{
			if (_timer    > 0) _timer    -= dt;
			if (_cooldown > 0) _cooldown -= dt;
		}
	}
}