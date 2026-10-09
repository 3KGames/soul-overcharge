using DG.Tweening;
using Game.Car.Controller.CarPhysics.Suspension;
using UnityEngine;

namespace Game.Car.VFX.Wheel.Runtime
{
	public class WheelVfxView : MonoBehaviour
	{
		[SerializeField] private ParticleSystem dust;
		[SerializeField] private TrailRenderer trail;

		[SerializeField] private bool trailDrift;
		
		public ParticleSystem Dust => dust;
		public TrailRenderer Trail => trail;
		public WheelAnchor Wheel { get; set; }
		public bool TrailDrift => trailDrift;
		
		public float SmoothedRate { get; set; }
		public bool OffroadTint { get; set; }
		public Color DustColor { get; set; }
		public Tween DustColorTween { get; set; }

		public void Initialize()
		{
			if (dust == null)
				dust = GetComponentInChildren<ParticleSystem>(true);
			
			if (trail == null)
				trail = GetComponentInChildren<TrailRenderer>(true);
			
			if (Wheel == null)
				Wheel = GetComponentInParent<WheelAnchor>();
		}
	}
}