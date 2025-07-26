using Car.Gears;
using UnityEngine;
using VContainer;

namespace Car.Controller
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [Inject] private InputService _input;
        [Inject] private CarPhysicsService _physics;
        [Inject] private RoadCheckService _road;

        [SerializeField] private GearData gearData;

        private Rigidbody _rb;
        public int CurrentGearIndex { get; private set; } = 0;
        public System.Action<int> OnGearChanged;

        private void Awake() => _rb = GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            HandleGearShift();

            bool drift = _input.DriftHeld;
            float steer = _input.Steer;
            var gear = gearData.gears[CurrentGearIndex];
            Vector3 normal = _road.GetRoadNormal(transform.position);

            _physics.UpdatePhysics(_rb, steer, drift, gear, normal);
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
    }
}
