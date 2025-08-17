using Car.Controller.CarPhysics;
using Car.Controller.CarPhysics.States;
using Car.Gears;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Car.Controller
{
	[RequireComponent(typeof(Rigidbody))]
	public class CarController : MonoBehaviour
	{
		[Inject] private CarService _carService;
		/// Field only for unsubscription
		[Inject] private DriftCarState _driftState;
		[Expandable]
		[SerializeField] private Transform body;
		
        private Rigidbody _rb;
		
		// Drift rotation
		private Tween		_driftTween;
		private const float DriftAngle  = 45f;
		private const float DriftTime   = 0.25f;
		private const float RecoverTime = 0.35f;

		private void Start()
		{
			_rb = GetComponent<Rigidbody>();

			_driftState.OnDriftStarted	+= DriftStarted;
			_driftState.OnDriftEnded	+= DriftEnded;
			
			//_driftState.OnDriftEnded	+= _nitro.DriftEnded;
		}

		private void OnDestroy()
		{
			_driftState.OnDriftStarted	-= DriftStarted;
			_driftState.OnDriftEnded	-= DriftEnded;
			
			//_driftState.OnDriftEnded	-= _nitro.DriftEnded;
		}

        private void FixedUpdate()
        {
	        _carService.PhysicsUpdate(_rb);
        }


		private void DriftStarted(float dir)
		{
			_driftTween.Kill();
			
			float targetY  = DriftAngle * dir;
			Vector3 endRot = new Vector3(0f, targetY, 0f);

			_driftTween = body.DOLocalRotate(endRot, DriftTime)
				.SetEase(Ease.OutQuad);
		}

		private void DriftEnded(float duration)
		{
			_driftTween.Kill();
			
			Vector3 endRot = new Vector3(0f, 0f, 0f);
			
			_driftTween = body.DOLocalRotate(endRot, RecoverTime)
				.SetEase(Ease.OutQuad);
		}
    }
}
