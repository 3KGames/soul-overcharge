using Car.Gears;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Car.Controller
{
    public readonly struct CarPhysicsInput
    {
        public bool		Drift		{ get; }
        public float	Steer		{ get; }
        public IGear	Gear		{ get; }
        public Vector3	RoadNormal	{ get; }

        public CarPhysicsInput(bool drift, float steer, IGear gear, Vector3 normal)
        {
            Drift      = drift;
            Steer      = steer;
            Gear       = gear;
            RoadNormal = normal;
        }
    }

    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [Inject] private InputService		_input;
        [Inject] private CarPhysicsService	_physics;
        [Inject] private RoadCheckService	_road;

		[Expandable]
        [SerializeField] private GearDataBase gearData;

        private Rigidbody _rb;
        
        public int		CurrentSpeed		{ get; private set; } = 0;
        public int		CurrentGearIndex	{ get; private set; } = 0;
        
		public event System.Action<int> OnGearChanged;
        public event System.Action<int> OnSpeedChanged;

        
        private void Awake() => _rb = GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            HandleGearShift();
            HandleSpeedChange();

            var inputData = new CarPhysicsInput(
                _input.DriftHeld,
                _input.Steer,
                gearData.GetGear(CurrentGearIndex),
                _road.GetRoadNormal(transform.position));
            _physics.UpdatePhysics(_rb, inputData);

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
            int newSpeed = Mathf.CeilToInt(_physics.GetForwardSpeed(_rb));
            if (newSpeed != CurrentSpeed)
            {
                CurrentSpeed = newSpeed;
                OnSpeedChanged?.Invoke(CurrentSpeed);
            }
        }
    }
}
