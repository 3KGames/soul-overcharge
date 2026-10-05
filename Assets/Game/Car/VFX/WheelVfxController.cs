using System;
using Car.Controller;
using Game.Car.Controller.CarPhysics.Suspension;
using UnityEngine;
using VContainer;

namespace Game.Car.VFX
{
	public class WheelVfxController : MonoBehaviour
	{
		[SerializeField] private float rateIdle = 0.35f;
		[SerializeField] private float rateOffroad = 40f;
		[SerializeField] private float rateDrift = 16f;
		[SerializeField] private float speedForMaxRate = 50;

		[SerializeField] private Color roadSmoke = Color.white;
		[SerializeField] private Color offroadSmoke = Color.yellow;

		[Inject] private CarController _car;
		[Inject] private CarService _carService;
		[Inject] private RoadCheckService _road;

		private WheelVfxView[] _vfxs;

		private void Awake()
		{
			_vfxs = GetComponentsInChildren<WheelVfxView>(true);
			
			for (int i = 0; i < _vfxs.Length; i++)
				_vfxs[i].Initialize();
		}

		private void OnDisable()
		{
			for (int i = 0; i < _vfxs.Length; i++)
			{
				WheelVfxView vfx = _vfxs[i];

				if (vfx.Dust != null)
				{
					ParticleSystem.EmissionModule emission = vfx.Dust.emission;
					emission.rateOverTime = 0f;
				}

				if (vfx.Trail != null)
				{
					vfx.Trail.emitting = false;
				}
			}
		}

		private void Update()
		{
			bool isDrifting = _car.IsDrifting;
			float speed = Mathf.Abs(_carService.CurrentSpeed) * 3.6f;

			for (int i = 0; i < _vfxs.Length; i++)
			{
				WheelVfxView vfx = _vfxs[i];

				WheelAnchor wheel = vfx.Wheel;

				bool isOffroad = wheel.IsGrounded && _road.IsOffroad(wheel.GroundLayer);
				bool wantDust = wheel.IsGrounded && (isOffroad || isDrifting);
				bool wantTrail = vfx.TrailDrift && wheel.IsGrounded && isDrifting;
				
				TickDust(vfx, isOffroad, speed, wantDust);
				TickTrail(vfx, wantTrail);
			}
		}

		private void TickDust(WheelVfxView vfx, bool offroad, float speed, bool want)
		{
			ParticleSystem ps = vfx.Dust;

			float target = 0f;
			if (want)
			{
				float t = Mathf.Clamp01(speed / speedForMaxRate);
				target = (offroad ? rateOffroad : rateDrift) * Mathf.Lerp(rateIdle, 1f, t);
			}
			
			if (!ps.isPlaying)
				ps.Play();

			ParticleSystem.EmissionModule emission = ps.emission;
			emission.rateOverTime = target;

			bool tint = want && offroad;
			if (tint != vfx.OffroadTint)
			{
				vfx.OffroadTint = tint;
				
				ParticleSystem.MainModule main = ps.main;
				main.startColor = tint ? offroadSmoke : roadSmoke;
			}
		}

		private void TickTrail(WheelVfxView vfx, bool want)
		{
			TrailRenderer trail = vfx.Trail;

			if (trail.emitting != want)
				trail.emitting = want;
		}
	}
}