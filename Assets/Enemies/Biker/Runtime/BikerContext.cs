using UnityEngine;
using Dreamteck.Splines;
using VContainer;
using Common.Runtime.StateMachine;
using Common.Runtime;

namespace Enemies.Biker.Runtime
{
    [RequireComponent(typeof(Rigidbody))]
    public class BikerContext : MonoBehaviour
    {
        [Header("Ссылки")]
        public Animator Animator;
        public Rigidbody Rb;

        [Header("Настройки: Погоня")]
        public float CatchUpBonusSpeed = 15f; 
        public float Acceleration = 20f;      

        [Header("Настройки: Параллельная езда")]
        public float ParallelDistanceOffset = 0f; 
        public float ParallelStickiness = 10f;    
        public float LateralChangeSpeed = 5f;     

        [Header("Настройки: Полосы и Обгон")]
        public float LaneWidth = 3f; 
        public float WaitBehindDistance = 15f;
        public float LaneChangeCooldown = 2f;

        [Header("Здоровье и Урон")]
        public float MaxHealth = 100f;
        public float CurrentHealth { get; private set; }
        public float RamDamageMultiplier = 2f; 
        public float MinImpactForce = 5f;      

        public float CurrentSpeed { get; private set; }
        public float CurrentAcceleration { get; private set; } 
        public float PlayerSpeed { get; private set; }
        public float DistanceToPlayer { get; private set; } 

        public RoadSegmentView CurrentRoad { get; private set; }
        public RoadSegmentView PlayerRoad { get; private set; }
        
        public int TargetLane { get; private set; }
        public int PlayerLane { get; private set; }
        public float EffectiveDistanceOffset { get; private set; }

        private Transform _playerTransform;
        private Rigidbody _playerRb;

        private BikerStateMachine _stateMachine;
        private PlayerTracker _playerTracker;
        private float _speedDampVelocity; 
        private float _laneChangeTimer;

        [Inject]
        public void Construct(BikerStateMachine stateMachine, PlayerTracker playerTracker)
        {
            _stateMachine = stateMachine;
            _playerTracker = playerTracker;
        }

        private void Awake()
        {
            CurrentHealth = MaxHealth;
            if (Rb == null) Rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            Rb.isKinematic = false;
            Rb.interpolation = RigidbodyInterpolation.Interpolate;
            Rb.useGravity = true;

            _playerTransform = _playerTracker.PlayerTransform;
            _playerRb = _playerTracker.PlayerRb;

            if (_playerTransform == null)
            {
                Debug.LogError("[Enemy] Игрок еще не зарегистрирован в PlayerTracker!");
                return;
            }

            CurrentRoad = FindClosestRoad(transform.position);
            PlayerRoad = FindClosestRoad(_playerTransform.position);

            int startLanes = CurrentRoad != null ? Mathf.Max(1, CurrentRoad.laneCount) : 1;
            TargetLane = Random.Range(0, startLanes);
            EffectiveDistanceOffset = ParallelDistanceOffset;

            _stateMachine.Switch(BikerStateType.Chase);
        }

        private void FixedUpdate()
        {
            if (CurrentRoad == null || _playerTransform == null) return;

            CurrentRoad = CheckRoadTransition(CurrentRoad, transform.position);
            PlayerRoad = CheckRoadTransition(PlayerRoad, _playerTransform.position);

            SplineSample currentSample = CurrentRoad.spline.Project(transform.position);
            CurrentSpeed = Vector3.Dot(Rb.linearVelocity, currentSample.forward);

            SplineSample playerSample = PlayerRoad.spline.Project(_playerTransform.position);
            PlayerSpeed = Vector3.Dot(_playerRb.linearVelocity, playerSample.forward);

            CalculateRelativeDistance();
            
            UpdateLaneLogic(playerSample);

            _stateMachine.Update();
        }

        private void UpdateLaneLogic(SplineSample playerSample)
        {
            int laneCount = Mathf.Max(1, CurrentRoad.laneCount);
            
            if (TargetLane >= laneCount) TargetLane = laneCount - 1;

            float playerLateralOffset = Vector3.Dot(_playerTransform.position - playerSample.position, playerSample.right);
            PlayerLane = CalculateLaneIndex(playerLateralOffset, laneCount);

            _laneChangeTimer -= Time.fixedDeltaTime;

            bool isBlocked = (TargetLane == PlayerLane && DistanceToPlayer > 0 && DistanceToPlayer < 40f);

            if (isBlocked)
            {
                if (_laneChangeTimer <= 0f && laneCount > 1)
                {
                    int newLane = TargetLane;
                    int attempts = 0;
                    
                    while ((newLane == TargetLane || newLane == PlayerLane) && attempts < 10)
                    {
                        newLane = Random.Range(0, laneCount);
                        attempts++;
                    }
                    
                    TargetLane = newLane;
                    _laneChangeTimer = LaneChangeCooldown;
                }
                else
                {
                    EffectiveDistanceOffset = -WaitBehindDistance;
                }
            }
            else
            {
                EffectiveDistanceOffset = ParallelDistanceOffset;
            }
        }

        private int CalculateLaneIndex(float offset, int laneCount)
        {
            float totalWidth = laneCount * LaneWidth;
            float startOffset = (-totalWidth / 2f) + (LaneWidth / 2f);
            
            int lane = Mathf.RoundToInt((offset - startOffset) / LaneWidth);
            return Mathf.Clamp(lane, 0, laneCount - 1);
        }

        public float GetLaneCenter(int laneIndex)
        {
            int laneCount = Mathf.Max(1, CurrentRoad.laneCount);
            laneIndex = Mathf.Clamp(laneIndex, 0, laneCount - 1);
            
            float totalWidth = laneCount * LaneWidth;
            float startOffset = (-totalWidth / 2f) + (LaneWidth / 2f);
            return startOffset + (laneIndex * LaneWidth);
        }

        private RoadSegmentView FindClosestRoad(Vector3 targetPosition)
        {
            RoadSegmentView[] allRoads = Object.FindObjectsByType<RoadSegmentView>(FindObjectsSortMode.None);
            RoadSegmentView closestRoad = null;
            float minDistance = float.MaxValue;

            foreach (var road in allRoads)
            {
                if (road.spline == null) continue;
                SplineSample sample = road.spline.Project(targetPosition);
                float dist = Vector3.Distance(targetPosition, sample.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestRoad = road;
                }
            }
            return closestRoad;
        }

        private RoadSegmentView CheckRoadTransition(RoadSegmentView road, Vector3 pos)
        {
            if (road == null) return null;
            SplineSample sample = road.spline.Project(pos);

            if (sample.percent >= 0.99f && road.nextRoad != null) return road.nextRoad;
            if (sample.percent <= 0.01f && road.previousRoad != null) return road.previousRoad;

            return road; 
        }

        private void CalculateRelativeDistance()
        {
            float enemyDistOnSpline = CurrentRoad.spline.CalculateLength(0.0, CurrentRoad.spline.Project(transform.position).percent);
            float playerDistOnSpline = PlayerRoad.spline.CalculateLength(0.0, PlayerRoad.spline.Project(_playerTransform.position).percent);

            if (CurrentRoad == PlayerRoad)
            {
                DistanceToPlayer = playerDistOnSpline - enemyDistOnSpline;
                return;
            }

            float dist = CurrentRoad.roadLength - enemyDistOnSpline;
            RoadSegmentView curr = CurrentRoad.nextRoad;
            int maxDepth = 20; 
            
            while (curr != null && maxDepth > 0)
            {
                if (curr == PlayerRoad) 
                {
                    DistanceToPlayer = dist + playerDistOnSpline;
                    return;
                }
                dist += curr.roadLength;
                curr = curr.nextRoad;
                maxDepth--;
            }

            dist = -enemyDistOnSpline;
            curr = CurrentRoad.previousRoad;
            maxDepth = 20;

            while (curr != null && maxDepth > 0)
            {
                if (curr == PlayerRoad)
                {
                    DistanceToPlayer = dist - (curr.roadLength - playerDistOnSpline);
                    return;
                }
                dist -= curr.roadLength;
                curr = curr.previousRoad;
                maxDepth--;
            }

            DistanceToPlayer = 0f; 
        }

        private void ApplyMovement(float targetSpeed, float targetLateral, float accel)
        {
            float newSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, accel * Time.fixedDeltaTime);
            CurrentAcceleration = (newSpeed - CurrentSpeed) / Time.fixedDeltaTime;

            SplineSample currentSample = CurrentRoad.spline.Project(transform.position);
            float currentLateralOffset = Vector3.Dot(transform.position - currentSample.position, currentSample.right);
            
            float lateralVelocityAmount = (targetLateral - currentLateralOffset) * LateralChangeSpeed;

            Vector3 forwardVel = currentSample.forward * newSpeed;
            Vector3 lateralVel = currentSample.right * lateralVelocityAmount;
            
            Rb.linearVelocity = new Vector3(forwardVel.x + lateralVel.x, Rb.linearVelocity.y, forwardVel.z + lateralVel.z);

            Quaternion targetRot = Quaternion.LookRotation(currentSample.forward, currentSample.up);
            Rb.MoveRotation(Quaternion.Slerp(Rb.rotation, targetRot, 15f * Time.fixedDeltaTime));
        }

        public void ApplyChaseMovement(float targetSpeed, float targetLateral)
        {
            ApplyMovement(targetSpeed, targetLateral, Acceleration);
        }

        public void ApplyParallelMovement(float distanceError, float targetLateral)
        {
            float idealSpeed = PlayerSpeed + (distanceError * ParallelStickiness);
            float targetSpeed = Mathf.SmoothDamp(CurrentSpeed, idealSpeed, ref _speedDampVelocity, 0.15f);
            targetSpeed = Mathf.Max(0f, targetSpeed); 
            ApplyMovement(targetSpeed, targetLateral, Acceleration * 1.5f); 
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Player"))
            {
                float impactForce = collision.relativeVelocity.magnitude;
                if (impactForce >= MinImpactForce)
                {
                    TakeDamage(impactForce * RamDamageMultiplier, collision.relativeVelocity);
                }
            }
        }

        public void TakeDamage(float amount, Vector3 impactVelocity)
        {
            if (CurrentHealth <= 0) return; 

            CurrentHealth -= amount;
            if (CurrentHealth <= 0)
            {
                _stateMachine.Switch(BikerStateType.Dead);
                Rb.AddTorque(Random.insideUnitSphere * impactVelocity.magnitude * 2000f);
            }
        }
    }
}