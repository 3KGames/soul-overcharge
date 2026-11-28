using Game.Enemies.Vehicle;
using UnityEngine;
using VContainer;

namespace Game.Enemies.Audio
{
    /// <summary>
    /// Audio View (чистый DIP через IVehicleTelemetry):
    /// интерполирует FixedUpdate-данные на render rate и кормит FMOD-адаптер.
    /// </summary>
    [RequireComponent(typeof(EnemyAudioFmodAdapter))]
    public sealed class EnemyVehicleAudioView : MonoBehaviour
    {
        [SerializeField] private EnemyVehicleController _vehicleController;
        [SerializeField, Range(1f, 30f)] private float _rpmSmoothSpeed = 15f;
        [SerializeField, Range(1f, 30f)] private float _loadSmoothSpeed = 10f;
        [SerializeField, Min(0.05f)] private float _maxSlipRadians = 0.35f;

        private EnemyAudioFmodAdapter _adapter;
        private EnemyAudioLodManager _lodManager;
        private IVehicleTelemetry _telemetry;

        private float _visualRpm;
        private float _visualLoad;
        private float _visualSlip;

        [Inject]
        public void Construct(EnemyAudioLodManager lodManager)
        {
            _lodManager = lodManager;
        }

        private void Awake()
        {
            _adapter = GetComponent<EnemyAudioFmodAdapter>();
            if (_vehicleController == null) _vehicleController = GetComponent<EnemyVehicleController>();
        }

        private void Start()
        {
            if (_vehicleController != null)
            {
                _telemetry = _vehicleController.Telemetry;
                if (_telemetry != null)
                {
                    _visualRpm = _telemetry.CurrentRpm;
                    _telemetry.OnGearChanged += HandleGearChanged;
                }
            }

            _lodManager?.Register(_adapter);
        }

        private void OnDestroy()
        {
            if (_telemetry != null)
                _telemetry.OnGearChanged -= HandleGearChanged;

            _lodManager?.Unregister(_adapter);
        }

        private void Update()
        {
            if (_telemetry == null || _adapter.IsPaused) return;

            float dt = Time.deltaTime;
            _visualRpm = Mathf.Lerp(_visualRpm, _telemetry.CurrentRpm, 1f - Mathf.Exp(-_rpmSmoothSpeed * dt));
            _visualLoad = Mathf.Lerp(_visualLoad, _telemetry.EngineLoad, 1f - Mathf.Exp(-_loadSmoothSpeed * dt));

            float slipNorm = Mathf.Clamp01(_telemetry.LateralSlipRadians / _maxSlipRadians);
            _visualSlip = Mathf.Lerp(_visualSlip, slipNorm, 1f - Mathf.Exp(-_loadSmoothSpeed * dt));

            _adapter.UpdateEngineParameters(_visualRpm, _visualLoad, _visualSlip, _telemetry.CurrentGear);
        }

        private void HandleGearChanged(int newGear) => _adapter.PlayGearShiftOneShot();
    }
}