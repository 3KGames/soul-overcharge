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
    public readonly struct CarPhysicsInput
    {
        public bool		Drift				{ get; }
        public float	Steer				{ get; }
        public IGear	Gear				{ get; }
        public Vector3	RoadNormal			{ get; }
		public float	TorqueMultiplier	{ get; }
		
        public CarPhysicsInput(bool drift, float steer, IGear gear, Vector3 normal, float torqueMultiplier)
        {
			Drift      			= drift;
			Steer      			= steer;
			Gear       			= gear;
			RoadNormal 			= normal;
			TorqueMultiplier	= torqueMultiplier;
		}
    }

    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [Inject] private InputService		_input;
        [Inject] private CarPhysicsService	_physics;
		[Inject] private NitroService		_nitro;
        [Inject] private RoadCheckService	_road;
		/// Field only for unsubscription
		[Inject] private DriftCarState		_driftState;
		
		

		[Expandable]
        [SerializeField] private GearDataBase gearData;
		[SerializeField] private Transform body;
		
        private Rigidbody _rb;
		
		// Drift rotation
		private Tween		_driftTween;
		private const float DriftAngle  = 45f;
		private const float DriftTime   = 0.25f;
		private const float RecoverTime = 0.35f;
        
        public int	CurrentSpeed		{ get; private set; } = 0;
        public int	CurrentGearIndex	{ get; private set; } = 0;
		
		public event System.Action<int> OnGearChanged;
        public event System.Action<int> OnSpeedChanged;


		private void Awake()
		{
			_rb = GetComponent<Rigidbody>();

			_driftState.OnDriftStarted	+= DriftStarted;
			_driftState.OnDriftEnded	+= DriftEnded;
			
			_driftState.OnDriftEnded	+= _nitro.DriftEnded;
		}

		private void OnDestroy()
		{
			_driftState.OnDriftStarted	-= DriftStarted;
			_driftState.OnDriftEnded	-= DriftEnded;
			
			_driftState.OnDriftEnded	-= _nitro.DriftEnded;
		}

        private void FixedUpdate()
        {
            HandleGearShift();
            HandleSpeedChange();
			
			_nitro.Tick(Time.fixedDeltaTime);

            var inputData = new CarPhysicsInput(
                _input.DriftHeld,
                _input.Steer,
                gearData.GetGear(CurrentGearIndex),
                _road.GetRoadNormal(transform.position),
				_nitro.GetTorqueMultiplier());
            _physics.Tick(Time.fixedDeltaTime, _rb, inputData);

            _input.ResetShiftBuffers();
        }

        private void HandleGearShift()
        {
            if (_input.ShiftUpTriggered && CurrentGearIndex < gearData.GearsCount - 1)
            {
                CurrentGearIndex++;
                OnGearChanged?.Invoke(CurrentGearIndex);
            }
            else if (_input.ShiftDownTriggered && CurrentGearIndex > 0)
            {
                CurrentGearIndex--;
                OnGearChanged?.Invoke(CurrentGearIndex);
            }
        }

        private void HandleSpeedChange()
        {
            int newSpeed = Mathf.CeilToInt(CarPhysicsService.GetForwardSpeed(_rb));
            if (newSpeed != CurrentSpeed)
            {
                CurrentSpeed = newSpeed;
                OnSpeedChanged?.Invoke(CurrentSpeed);
            }
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
