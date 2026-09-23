using System;
using Car.Controller;
using Car.Controller.CarPhysics;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using NaughtyAttributes;

namespace VFX.Speed_Effect
{
	public class SpeedEffectView: MonoBehaviour
	{
		[SerializeField]
		private Image image;
		
		[SerializeField] 
		private float maxSpeed = 100f;
		
		[SerializeField]
		[CurveRange(0, 0, 1, 1)]
		private AnimationCurve curve;

		[Inject] 
		private CarController carController;
		
		public void Update()
		{
			float speedCoefficient = Mathf.Clamp01(CarPhysicsService.GetForwardSpeed(carController.RB) * 3.6f / maxSpeed);
			image.color = new Color(1f, 1f, 1f, curve.Evaluate(speedCoefficient));
		}
	}
}