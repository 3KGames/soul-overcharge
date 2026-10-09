using Car.Controller.CarPhysics;
using Car.Controller.CarPhysics.States;
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
		[Inject] private CarService _carService;
		[Inject] private DriftCarState _driftState;
		[Inject] private DriftRecoveryCarState _driftRecoveryState;
		[Inject] private InputService _input;

		[SerializeField] private Transform body;

        private Rigidbody _rb;

		private Tween		_driftTween;
		private const float DriftAngle  = 45f;
		private const float DriftTime   = 0.45f;
		private const float RecoverTime = 0.515f;

		private bool _isDrifting;
		private int _driftDir;
		private bool _isRecoveringFromDrift;

		public Rigidbody RB => _rb;
		public bool IsDrifting => _isDrifting;
		public bool IsRecoveringFromDrift => _isRecoveringFromDrift;
		
		public Vector3 LinearVelocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
		public Vector3 AngularVelocity => _isRecoveringFromDrift
			? new Vector3(0, _driftRecoveryState != null ? _driftRecoveryState.VirtualYawRate : 0, 0)
			: _rb != null ? _rb.angularVelocity : Vector3.zero; // TODO: UNITY_EDITOR macro

		private void Start()
		{
			_rb = GetComponent<Rigidbody>();

			_driftState.OnDriftStarted	+= DriftStarted;
			_driftState.OnDriftEnded	+= DriftEnded;
			_driftRecoveryState.OnDriftRecoveryStarted +=  DriftRecoveryStarted;
			_driftRecoveryState.OnDriftRecoveryEnded += DriftRecoveryEnded;
		}

		private void OnDestroy()
		{
			_driftState.OnDriftStarted	-= DriftStarted;
			_driftState.OnDriftEnded	-= DriftEnded;
			_driftRecoveryState.OnDriftRecoveryStarted -= DriftRecoveryStarted;
			_driftRecoveryState.OnDriftRecoveryEnded -= DriftRecoveryEnded;
		}

		private void Update()
		{
			//animator.SetBool("IsDrifting", _isDrifting);
			if (_isDrifting)
			{
				//animator.SetInteger(SpriteN, _driftDir );
			}
			else
			{
				int dir;
				if (Mathf.Approximately(_input.Steer, 0f))
					dir = 0;
				else
					dir = (int)Mathf.Sign(_input.Steer);
				//animator.SetInteger(SpriteN, dir);
			}
		}

        private void FixedUpdate()
        {
	        _carService.PhysicsUpdate(_rb);
        }

		private void DriftStarted(int dir)
		{
			_isDrifting = true;
			_driftDir = dir;
		}

		private void DriftEnded(float duration)
		{
			_isDrifting = false;
		}

		private void DriftRecoveryStarted()
		{
			_isRecoveringFromDrift = true;
		}

		private void DriftRecoveryEnded()
		{
			_isRecoveringFromDrift = false;
		}
    }
}
