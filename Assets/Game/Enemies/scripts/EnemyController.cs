using Game.Common.Runtime;
using Game.Common.Runtime.Movement;
using Game.Enemies.Flocking;
using Game.Level.Runtime;
using Game.Level.Runtime.Config;
using Common.Runtime;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Enemies
{
    public abstract class EnemyController : MonoBehaviour
    {
        [Header("Movement")]
        public SplineFollower Follower = new();
        public float BaseSpeed = 30f;
        public float MaxSpeed = 50f;
        
        [Header("Flocking")]
        public bool CanFlock = false;
        public float SeparationRadius = 10f;
        public float AlignmentWeight = 1f;
        public float CohesionWeight = 0.5f;
        
        [Header("Combat")]
        public float RamForce = 5000f;
        public float RamCooldown = 1f;
        
        // Injected dependencies
        protected PlayerTracker PlayerTracker { get; private set; }
        protected RoadGridSettings GridSettings { get; private set; }
        
        // Cached
        protected Transform PlayerTransform => PlayerTracker?.PlayerTransform;
        protected Rigidbody Rb { get; private set; }
        protected IRoadPath CurrentPath => Follower.CurrentPath;
        
        // Flock data (populated by FlockSystem)
        protected FlockAgentData FlockData { get; set; }
        
        [Inject]
        public void Construct(PlayerTracker playerTracker, RoadGridSettings gridSettings)
        {
            PlayerTracker = playerTracker;
            GridSettings = gridSettings;
            
            // Configure follower from grid settings
            Follower.MaxLateralAcceleration = GridSettings != null ? GridSettings.cellSize * 0.1f : 10f;
            Follower.LateralDamping = 15f;
            Debug.Log($"[EnemyController:{name}] Construct: PlayerTracker={playerTracker != null} GridSettings={gridSettings != null} cellSize={gridSettings?.cellSize}");
        }
        
        protected virtual void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            if (Rb == null)
                Debug.LogWarning($"[EnemyController] {name}: Rigidbody не найден — движение через Transform");
        }
        
        protected virtual void Start()
        {
            Follower.Speed = BaseSpeed;
        }
        
        protected virtual void FixedUpdate()
        {
            if (PlayerTracker == null || PlayerTransform == null) 
            {
                Debug.Log($"[EnemyController:{name}] SKIP: PlayerTracker={PlayerTracker != null} PlayerTransform={PlayerTransform != null}");
                return;
            }
            
            float dt = Time.fixedDeltaTime;
            
            // 1. AI Decision
            UpdateAI(dt);
            
            // 2. Apply flock modifiers
            if (CanFlock)
                ApplyFlockModifiers(dt);
            
            // 3. Step follower
            var result = Follower.ComputeFrame(dt);
            Debug.Log($"[EnemyController:{name}] ComputeFrame: Speed={Follower.Speed} Path={Follower.CurrentPath != null} Lane={Follower.CurrentLane} Dist={Follower.DistanceAlongPath:F2} ResultPos={result.Position} Vel={result.Velocity}");
            
            // 4. Move
            MoveRigidbody(result);
            
            // 5. Handle segment transition
            if (result.PathCompleted && result.NextPath != null)
            {
                // Передача владения врагом: старый сегмент -> новый сегмент
                var oldPathComp = Follower.CurrentPath as Component;
                var newPathComp = result.NextPath as Component;
                
                if (oldPathComp != null && newPathComp != null)
                {
                    var oldSegment = oldPathComp.GetComponentInParent<RoadSegment>();
                    var newSegment = newPathComp.GetComponentInParent<RoadSegment>();

                    oldSegment?.Spawner?.UnregisterEnemy(this);
                    newSegment?.Spawner?.RegisterEnemy(this);
                }

                Follower.SetPath(result.NextPath, Follower.CurrentLane, 0f);
            }
        }
        
        /// <summary>AI logic: set target speed, request lane changes, combat.</summary>
        protected abstract void UpdateAI(float dt);
        
        protected virtual void MoveRigidbody(SplineFollower.FrameResult result)
        {
            Debug.Log($"[EnemyController:{name}] MoveRigidbody: Rb={Rb != null} Pos={result.Position} Vel={result.Velocity}");
            if (Rb != null)
            {
                Rb.MovePosition(result.Position);
                Rb.MoveRotation(result.Rotation);
                Rb.linearVelocity = result.Velocity;
            }
            else
            {
                transform.SetPositionAndRotation(result.Position, result.Rotation);
            }
        }
        
        protected virtual void ApplyFlockModifiers(float dt)
        {
            if (FlockData.LateralForce.sqrMagnitude > 0.001f)
            {
                // Convert lateral force to lane offset delta
                // Force is in world space, project to current path right
                if (CurrentPath != null)
                {
                    var sample = CurrentPath.SampleAtDistance(Follower.DistanceAlongPath);
                    float lateralDot = Vector3.Dot(FlockData.LateralForce, sample.right);
                    Follower.RequestLaneChangeOffset(lateralDot * dt * 0.1f); // Tunable
                }
            }
            
            if (FlockData.SpeedMultiplier > 0f)
            {
                Follower.SetSpeed(Follower.Speed * FlockData.SpeedMultiplier);
            }
        }
        
        /// <summary>Called by TrackEnemySpawner to initialize on specific segment.</summary>
        public virtual void InitializeWithSegment(IRoadPath segment)
        {
            int startLane = RandomLane(segment);
            Follower.SetPath(segment, startLane, 0f);
            Follower.Speed = BaseSpeed;
            Debug.Log($"[EnemyController:{name}] InitializeWithSegment: segment={segment?.GetType().Name} lane={startLane} speed={BaseSpeed} LaneCount={segment?.LaneCount} Length={segment?.Length}");
        }
        
        /// <summary>Despawn cleanup. Override for effects.</summary>
        public virtual void Despawn()
        {
            // Immediate deactivation stops FixedUpdate, then destroy for GC
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        
        protected int RandomLane(IRoadPath path)
        {
            if (path == null) return 0;
            
            // Pick only active lanes at segment start
            var activeLanes = new List<int>();
            for (int i = 0; i < path.LaneCount; i++)
                if (path.IsLaneActiveAt(i, 0f))
                    activeLanes.Add(i);
            
            return activeLanes.Count > 0 
                ? activeLanes[Random.Range(0, activeLanes.Count)] 
                : 0;
        }
    }
}