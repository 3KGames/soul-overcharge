using Game.Common.Runtime;
using Game.Common.Runtime.Movement;
using Game.Enemies.Flocking;
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
            if (PlayerTracker == null || PlayerTransform == null) return;
            
            float dt = Time.fixedDeltaTime;
            
            // 1. AI Decision
            UpdateAI(dt);
            
            // 2. Apply flock modifiers
            if (CanFlock)
                ApplyFlockModifiers(dt);
            
            // 3. Step follower
            var result = Follower.ComputeFrame(dt);
            
            // 4. Move
            MoveRigidbody(result);
            
            // 5. Handle segment transition
            if (result.PathCompleted && result.NextPath != null)
            {
                Follower.SetPath(result.NextPath, Follower.CurrentLane, 0f);
            }
        }
        
        /// <summary>AI logic: set target speed, request lane changes, combat.</summary>
        protected abstract void UpdateAI(float dt);
        
        protected virtual void MoveRigidbody(SplineFollower.FrameResult result)
        {
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
            int startLane = RandomLane();
            Follower.SetPath(segment, startLane, 0f);
            Follower.Speed = BaseSpeed;
        }
        
        /// <summary>Despawn cleanup. Override for effects.</summary>
        public virtual void Despawn()
        {
            Destroy(gameObject);
        }
        
        protected int RandomLane()
        {
            if (CurrentPath == null) return 0;
            
            // Pick only active lanes at segment start
            var activeLanes = new List<int>();
            for (int i = 0; i < CurrentPath.LaneCount; i++)
                if (CurrentPath.IsLaneActiveAt(i, 0f))
                    activeLanes.Add(i);
            
            return activeLanes.Count > 0 
                ? activeLanes[Random.Range(0, activeLanes.Count)] 
                : 0;
        }
    }
}