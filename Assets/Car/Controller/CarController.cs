using Car.Controller.CarPhysics;
using Car.Controller.CarPhysics.States;
using Car.Gears;
using DG.Tweening;
using NaughtyAttributes;
using Unity.Mathematics.Geometry;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Car.Controller
{
	[RequireComponent(typeof(Rigidbody))]
	public class CarController : MonoBehaviour
	{
		private static readonly int SpriteN = Animator.StringToHash("SpriteN");
		private static readonly int DriftDir = Animator.StringToHash("DriftDir");

		[Inject] private CarService _carService;
		/// Field only for unsubscription
		[Inject] private DriftCarState _driftState;
		[Inject] private InputService _input;
		
		[SerializeField] private Transform body;
		[SerializeField] private Animator animator;
		[SerializeField] private SpriteRenderer spriteRenderer;
		
        private Rigidbody _rb;
		
		// Drift rotation
		private Tween		_driftTween;
		private const float DriftAngle  = 45f;
		private const float DriftTime   = 0.25f;
		private const float RecoverTime = 0.35f;

		private bool _isDrifting;
		private int _driftDir;

		public Rigidbody RB => _rb;

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

		private void Update()
		{
			animator.SetBool("IsDrifting", _isDrifting);
			//spriteRenderer.flipX = _isDrifting ? _driftDir > 0 : _input.Steer > 0f;
			if (_isDrifting)
			{
				animator.SetInteger(SpriteN, _driftDir /** _input.Steer*/);
			}
			else
			{
				int dir;
				if (Mathf.Approximately(_input.Steer, 0f))
					dir = 0;
				else
					dir = (int)Mathf.Sign(_input.Steer);
				animator.SetInteger(SpriteN, dir);
				Debug.Log(dir);
			}
		}

        private void FixedUpdate()
        {
	        _carService.PhysicsUpdate(_rb);
        }

		private void DriftStarted(int dir)
		{
			_driftTween.Kill();

			_isDrifting = true;
			_driftDir = dir;
			animator.SetInteger(DriftDir, dir);
			float targetY  = DriftAngle * dir;
			Vector3 endRot = new Vector3(0f, targetY, 0f);

			_driftTween = body.DOLocalRotate(endRot, DriftTime)
				.SetEase(Ease.OutQuad);
		}

		private void DriftEnded(float duration)
		{
			_driftTween.Kill();

			_isDrifting = false;
			Vector3 endRot = new Vector3(0f, 0f, 0f);
			
			_driftTween = body.DOLocalRotate(endRot, RecoverTime)
				.SetEase(Ease.OutQuad);
		}
    }
}
