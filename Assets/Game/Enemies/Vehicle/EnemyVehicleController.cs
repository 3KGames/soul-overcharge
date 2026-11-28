using System;
using Game.Common.Runtime;
using Game.Enemies.Behavior;
using UnityEngine;
using VContainer;

namespace Game.Enemies.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class EnemyVehicleController : MonoBehaviour
    {
        [Header("Configs (SO)")]
        [SerializeField] private EnemyVehicleConfigSO _vehicleConfig;
        [SerializeField] private EnemyBehaviorProfileSO _behaviorProfile;
        [SerializeField] private Rigidbody _rb;

        [Header("Standalone Test (если нет спавнера / Brain)")]
        [SerializeField] private MonoBehaviour _initialRoadPath;
        [SerializeField] private int _testStartLane;
        [SerializeField] private float _testLateralOffset;
        [SerializeField] private float _testSpeed = 25f;

        // Stateless-сервисы из VContainer
        private RoadTrackingService _roadTracking;
        private EnemyVehiclePhysicsService _physics;
        private EnemyDrivetrainService _drivetrain;
        private EnemyBrainFactory _brainFactory;
        private AgentSpatialRegistry _spatialRegistry;

        // Состояние конкретного врага
        private EnemyVehicleModel _model;
        private IEnemyBrain _brain;
        private IVehicleCommandModifier _commandModifier;
        private bool _initialized;

        public EnemyVehicleModel Model => _model;
        public IVehicleTelemetry Telemetry => _model;
        public event Action<EnemyVehicleController, IRoadPath, IRoadPath> OnSegmentChanged;

        [Inject]
        public void Construct(
            RoadTrackingService roadTracking,
            EnemyVehiclePhysicsService physics,
            EnemyDrivetrainService drivetrain,
            EnemyBrainFactory brainFactory,
            AgentSpatialRegistry spatialRegistry = null)
        {
            _roadTracking = roadTracking;
            _physics = physics;
            _drivetrain = drivetrain;
            _brainFactory = brainFactory;
            _spatialRegistry = spatialRegistry;
            EnsureInitialized();
        }

        private void Awake()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_vehicleConfig == null)
            {
                Debug.LogError($"[EnemyVehicleController] Missing EnemyVehicleConfigSO on {name}", this);
                enabled = false;
                return;
            }

            _model = new EnemyVehicleModel(_vehicleConfig);
        }

        private void Start()
        {
            // Фолбэк для изолированной тестовой сцены без VContainer
            EnsureStandaloneFallbackIfNotInjected();
            EnsureInitialized();

            if (!_model.HasRoad && _initialRoadPath is IRoadPath startPath)
            {
                InitializeOnRoad(startPath, _testStartLane, 0f);
            }
        }

        private void OnDestroy()
        {
            if (_spatialRegistry != null && _model != null && _model.SpatialAgentId >= 0)
            {
                _spatialRegistry.Unregister(_model.SpatialAgentId);
                _model.SpatialAgentId = -1;
            }
        }

        public void InitializeOnRoad(IRoadPath startPath, int startLane = 0, float startDistance = 0f, EnemyBehaviorProfileSO overrideProfile = null)
        {
            EnsureInitialized();

            if (overrideProfile != null && overrideProfile != _behaviorProfile)
            {
                _behaviorProfile = overrideProfile;
                _brain = _brainFactory != null ? _brainFactory.Create(_behaviorProfile) : null;
            }

            _model.ResetRuntimeState();
            _roadTracking.Initialize(_model, startPath, startLane, startDistance);
            _brain?.Reset();
            _commandModifier?.Reset();

            if (_spatialRegistry != null && _model.SpatialAgentId < 0)
            {
                _model.SpatialAgentId = _spatialRegistry.Register(_model.CurrentPath, _model.DistanceAlongPath, _model.LateralOffset);
            }
        }

        public void SetBrain(IEnemyBrain brain) => _brain = brain;
        public void SetCommandModifier(IVehicleCommandModifier modifier) => _commandModifier = modifier;

        private void FixedUpdate()
        {
            if (!_initialized || !_model.HasRoad) return;

            float dt = Time.fixedDeltaTime;

            // 1. Замкнутый трекинг по позиции Rigidbody
            if (_roadTracking.Step(_model, _rb.position, out var previousPath))
            {
                OnSegmentChanged?.Invoke(this, previousPath, _model.CurrentPath);
            }

            // 2. Обновление позиции в пространственном реестре (Этап 7)
            if (_spatialRegistry != null && _model.SpatialAgentId >= 0)
            {
                _spatialRegistry.Update(_model.SpatialAgentId, _model.CurrentPath, _model.DistanceAlongPath, _model.LateralOffset);
            }

            // 3. Решение мозга (или тестовые поля инспектора)
            if (_brain != null)
            {
                _brain.Step(_model, _rb, dt);
            }
            else
            {
                _model.TargetLateralOffset = _testLateralOffset;
                _model.TargetSpeed = _testSpeed;
                _model.WorldTargetOverride = null;
                _model.PendingPushImpulse = null;
            }

            // 4. Модификатор объезда препятствий (Этап 7)
            _commandModifier?.Modify(_model, dt);

            // 5. Исполнение физики
            _physics.Step(_model, _rb, dt);

            // 6. Синтетический привод для аудио
            _drivetrain?.Step(_model, _rb, dt);
        }

        private void EnsureInitialized()
        {
            if (_initialized || _model == null || _physics == null) return;

            _physics.ConfigureRigidbody(_rb, _vehicleConfig);

            if (_brain == null && _brainFactory != null && _behaviorProfile != null)
            {
                _brain = _brainFactory.Create(_behaviorProfile);
            }

            // Этап 7: автоматическое подключение объезда препятствий, если в профиле включено
            if (_commandModifier == null && _spatialRegistry != null && _behaviorProfile != null && _behaviorProfile.EnableObstacleAvoidance)
            {
                _commandModifier = new ObstacleAvoidanceModifier(_spatialRegistry, _behaviorProfile);
            }

            _initialized = true;
        }

        private void EnsureStandaloneFallbackIfNotInjected()
        {
            if (_physics != null) return;

            _roadTracking = new RoadTrackingService();
            _physics = new EnemyVehiclePhysicsService(new PurePursuitService(), _roadTracking);
            _drivetrain = new EnemyDrivetrainService();
        }

        public void Despawn()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_model == null || !_model.HasRoad) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_model.ProjectedPosition, 0.35f);
            Gizmos.DrawLine(_model.ProjectedPosition, _model.ProjectedPosition + _model.RoadUp * 1.5f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_model.LastGoalWorld, 0.45f);
            Gizmos.DrawLine(transform.position, _model.LastGoalWorld);

            Gizmos.color = Color.yellow;
            Vector3 desired = _model.ProjectedPosition + _model.RoadRight * _model.TargetLateralOffset;
            Gizmos.DrawWireSphere(desired, 0.25f);
            Gizmos.DrawLine(_model.ProjectedPosition, desired);
        }
#endif
    }
}