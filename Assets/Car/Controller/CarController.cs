using Car.Gears;
using UnityEngine;
using VContainer;

namespace Car.Controller
{
    public readonly struct CarPhysicsInput
    {
        public bool Drift { get; }
        public float Steer { get; }
        public GearData.Gear Gear { get; }
        public Vector3 RoadNormal { get; }
        
        public CarPhysicsInput(bool drift, float steer, GearData.Gear gear, Vector3 roadNormal)
        {
            Drift      = drift;
            Steer      = steer;
            Gear       = gear;
            RoadNormal = roadNormal;
        }
    }

    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [Inject] private InputService _input;
        [Inject] private CarPhysicsService _physics;
        [Inject] private RoadCheckService _road;

        [SerializeField] private GearData gearData;

        private Rigidbody _rb;
        public int CurrentSpeed { get; private set; } = 0;
        public int CurrentGearIndex { get; private set; } = 0;
        public System.Action<int> OnGearChanged;
        public System.Action<int> OnSpeedChanged;

        private void Awake() => _rb = GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            HandleGearShift();
            HandleSpeedChange();

            var inputData = new CarPhysicsInput(
                _input.DriftHeld,
                _input.Steer,
                gearData.gears[CurrentGearIndex],
                _road.GetRoadNormal(transform.position));
            _physics.UpdatePhysics(_rb, inputData);

            _input.ResetShiftBuffers();
        }

        private void HandleGearShift()
        {
            if (_input.ShiftUpTriggered && CurrentGearIndex < gearData.gears.Length - 1)
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
