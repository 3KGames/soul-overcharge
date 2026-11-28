using Game.Common.Runtime;
using UnityEngine;

namespace Game.Enemies
{
    public class ChaserEnemy : EnemyController
    {
        [Header("Chase Behavior")]
        [Tooltip("Дистанция, на которой враг начинает догонять игрока")]
        public float CatchUpDistance = 20f;
        
        [Tooltip("Дистанция для атаки тараном")]
        public float PushDistance = 3f;
        
        [Tooltip("Кулдаун тарана")]
        public float PushCooldown = 1.5f;
        
        [Header("Debug")]
        public bool ShowDebugGizmos = false;
        
        private float _pushTimer;
        
        protected override void UpdateAI(float dt)
        {
            if (PlayerTransform == null || CurrentPath == null) return;
            
            _pushTimer -= dt;
            
            // Project player onto current path
            var playerSample = CurrentPath.GetClosestSample(PlayerTransform.position);
            float playerDist = (float)(playerSample.percent * CurrentPath.Length);
            float myDist = Follower.DistanceAlongPath;
            float distToPlayer = playerDist - myDist;
            
            // --- Speed Control ---
            float targetSpeed = BaseSpeed;
            if (distToPlayer > CatchUpDistance)
            {
                // Far behind — max speed
                targetSpeed = MaxSpeed;
            }
            else if (distToPlayer < -5f)
            {
                // Far ahead — slow down
                targetSpeed = BaseSpeed * 0.5f;
            }
            // Else: maintain BaseSpeed when near player
            
            Follower.SetSpeed(targetSpeed);
            
            // --- Lane Matching ---
            int playerLane = CurrentPath.GetLaneAtPosition(PlayerTransform.position);
            if (playerLane != Follower.CurrentLane)
            {
                Follower.RequestLaneChange(playerLane);
            }
            
            // --- Ram Attack ---
            if (_pushTimer <= 0f && Mathf.Abs(distToPlayer) < PushDistance)
            {
                int lateralDiff = playerLane - Follower.CurrentLane;
                if (lateralDiff == 0 || Mathf.Abs(lateralDiff) == 1)
                {
                    TryRamPlayer();
                    _pushTimer = PushCooldown;
                }
            }
        }
        
        private void TryRamPlayer()
        {
            var playerRb = PlayerTransform.GetComponent<Rigidbody>();
            if (playerRb == null) return;
            
            Vector3 pushDir = (PlayerTransform.position - transform.position).normalized;
            pushDir.y = 0f;
            
            // Add some forward component for more dramatic push
            pushDir += transform.forward * 0.2f;
            pushDir.Normalize();
            
            playerRb.AddForce(pushDir * RamForce, ForceMode.Impulse);
            
            OnRam?.Invoke(this, PlayerTransform);
            
            // Visual feedback
            if (ShowDebugGizmos)
                Debug.Log($"[ChaserEnemy] Ram! Force: {RamForce}");
        }
        
        public event System.Action<ChaserEnemy, Transform> OnRam;
        
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!ShowDebugGizmos || CurrentPath == null) return;
            
            // Draw chase distance
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, CatchUpDistance);
            
            // Draw push distance
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, PushDistance);
            
            // Draw player projection
            if (PlayerTransform != null)
            {
                var proj = CurrentPath.GetClosestSample(PlayerTransform.position);
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(proj.position, 0.5f);
                Gizmos.DrawLine(PlayerTransform.position, proj.position);
            }
        }
#endif
    }
}