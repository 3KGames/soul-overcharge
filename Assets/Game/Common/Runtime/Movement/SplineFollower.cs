using Game.Common.Runtime;
using Dreamteck.Splines;
using System.Linq;
using UnityEngine;

namespace Game.Common.Runtime.Movement
{
    public struct SplineFollower
    {
        // State
        public IRoadPath CurrentPath { get; private set; }
        public float DistanceAlongPath { get; private set; }
        public int CurrentLane { get; private set; }
        
        // Config
        public float Speed { get; set; }
        public float MaxLateralAcceleration { get; set; }
        public float LateralDamping { get; set; }
        
        // Transition state
        public bool IsTransitioning { get; private set; }
        public IRoadPath NextPath { get; private set; }
        public float TransitionProgress { get; private set; }
        
        // Internal
        private float _targetLaneOffset;
        private float _currentLaneOffset;
        private float _lateralVelocity;

        public SplineFollower(float maxLateralAcceleration = 10f, float lateralDamping = 10f)
        {
            CurrentPath = null;
            DistanceAlongPath = 0f;
            CurrentLane = 0;
            Speed = 0f;
            MaxLateralAcceleration = maxLateralAcceleration;
            LateralDamping = lateralDamping;
            IsTransitioning = false;
            NextPath = null;
            TransitionProgress = 0f;
            _targetLaneOffset = 0f;
            _currentLaneOffset = 0f;
            _lateralVelocity = 0f;
        }
        
        public void SetPath(IRoadPath path, int lane = 0, float startDistance = 0f)
        {
            CurrentPath = path;
            CurrentLane = Mathf.Clamp(lane, 0, path != null ? path.LaneCount - 1 : 0);
            DistanceAlongPath = Mathf.Max(0f, startDistance);
            IsTransitioning = false;
            NextPath = null;
            TransitionProgress = 0f;
            
            if (path != null)
            {
                _targetLaneOffset = path.GetLaneOffset(CurrentLane);
                _currentLaneOffset = _targetLaneOffset;
                _lateralVelocity = 0f;
            }
        }
        
        public void RequestLaneChange(int targetLane)
        {
            if (CurrentPath == null) return;
            targetLane = Mathf.Clamp(targetLane, 0, CurrentPath.LaneCount - 1);
            if (targetLane == CurrentLane) return;
            
            // Validate lane is active at current position
            if (!CurrentPath.IsLaneActiveAt(targetLane, DistanceAlongPath))
            {
                // Find nearest active lane in the direction of the request
                int direction = (int)Mathf.Sign(targetLane - CurrentLane);
                int candidate = CurrentLane;
                for (int i = 1; i < CurrentPath.LaneCount; i++)
                {
                    int testLane = CurrentLane + direction * i;
                    if (testLane < 0 || testLane >= CurrentPath.LaneCount) break;
                    if (CurrentPath.IsLaneActiveAt(testLane, DistanceAlongPath))
                    {
                        candidate = testLane;
                        break;
                    }
                }
                targetLane = candidate;
            }
            
            if (targetLane == CurrentLane) return;
            
            CurrentLane = targetLane;
            _targetLaneOffset = CurrentPath.GetLaneOffset(CurrentLane);
        }
        
        public void RequestLaneChangeOffset(float offsetDelta)
        {
            // For flock system: apply lateral force as lane offset delta
            if (CurrentPath == null) return;
            float newOffset = _targetLaneOffset + offsetDelta;
            // Clamp to valid lane range
            float minOffset = CurrentPath.GetLaneOffset(0);
            float maxOffset = CurrentPath.GetLaneOffset(CurrentPath.LaneCount - 1);
            _targetLaneOffset = Mathf.Clamp(newOffset, minOffset, maxOffset);
            
            // Snap to nearest ACTIVE lane
            float bestDist = float.MaxValue;
            for (int i = 0; i < CurrentPath.LaneCount; i++)
            {
                if (!CurrentPath.IsLaneActiveAt(i, DistanceAlongPath)) continue;
                
                float dist = Mathf.Abs(CurrentPath.GetLaneOffset(i) - _targetLaneOffset);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    CurrentLane = i;
                }
            }
            _targetLaneOffset = CurrentPath.GetLaneOffset(CurrentLane);
        }
        
        public void SetSpeed(float speed)
        {
            Speed = Mathf.Max(0f, speed);
        }
        
        public readonly struct FrameResult
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Velocity;
            public readonly Vector3 LateralVelocity;
            public readonly bool PathCompleted;
            public readonly IRoadPath NextPath;
            
            public FrameResult(Vector3 pos, Quaternion rot, Vector3 vel, Vector3 latVel, bool completed, IRoadPath next)
            {
                Position = pos;
                Rotation = rot;
                Velocity = vel;
                LateralVelocity = latVel;
                PathCompleted = completed;
                NextPath = next;
            }
        }
        
        public FrameResult ComputeFrame(float dt)
        {
            if (CurrentPath == null)
                return new FrameResult(Vector3.zero, Quaternion.identity, Vector3.zero, Vector3.zero, false, null);
            
            // 1. Forward movement
            float prevDistance = DistanceAlongPath;
            DistanceAlongPath += Speed * dt;
            
            // 2. Check segment completion
            bool pathCompleted = DistanceAlongPath >= CurrentPath.Length;
            IRoadPath nextPath = null;
            
            if (pathCompleted)
            {
                // Get next path for current lane
                var nextPaths = CurrentPath.GetNextPaths(CurrentLane).ToList();
                if (nextPaths.Count > 0)
                {
                    nextPath = nextPaths[0]; // Take first available
                    
                    // Clamp current lane to next path's valid range FIRST
                    if (nextPath != null)
                    {
                        CurrentLane = Mathf.Clamp(CurrentLane, 0, nextPath.LaneCount - 1);
                    
                        // Validate current lane exists on next path at start (distance=0)
                        if (!nextPath.IsLaneActiveAt(CurrentLane, 0f))
                        {
                            // Find nearest active lane on next path
                            int candidateLane = CurrentLane;
                            for (int i = 1; i < nextPath.LaneCount; i++)
                            {
                                // Check both directions
                                int leftLane = CurrentLane - i;
                                int rightLane = CurrentLane + i;
                                bool found = false;
                                
                                if (leftLane >= 0 && nextPath.IsLaneActiveAt(leftLane, 0f))
                                {
                                    candidateLane = leftLane;
                                    found = true;
                                }
                                else if (rightLane < nextPath.LaneCount && nextPath.IsLaneActiveAt(rightLane, 0f))
                                {
                                    candidateLane = rightLane;
                                    found = true;
                                }
                                
                                if (found) break;
                            }
                            CurrentLane = candidateLane;
                        }
                        _targetLaneOffset = nextPath.GetLaneOffset(CurrentLane);
                        _currentLaneOffset = _targetLaneOffset;
                        _lateralVelocity = 0f;
                    }
                    
                    DistanceAlongPath -= CurrentPath.Length; // Carry over excess distance
                    IsTransitioning = true;
                    NextPath = nextPath;
                }
                else
                {
                    // End of road - clamp
                    DistanceAlongPath = CurrentPath.Length;
                }
            }
            
            // 3. Sample spline at current distance
            var sample = CurrentPath.SampleAtDistance(DistanceAlongPath);
            
            // 4. Lateral movement (lane keeping/changing)
            float lateralOffset = 0f;
            Vector3 lateralVel = Vector3.zero;
            
            if (CurrentPath != null)
            {
                // Spring-damper to target lane offset
                float springForce = (_targetLaneOffset - _currentLaneOffset) * MaxLateralAcceleration;
                float dampingForce = -_lateralVelocity * LateralDamping;
                float accel = springForce + dampingForce;
                
                _lateralVelocity += accel * dt;
                _currentLaneOffset += _lateralVelocity * dt;
                lateralOffset = _currentLaneOffset;
                
                lateralVel = sample.right * _lateralVelocity;
            }
            
            // 5. Compute final position/rotation
            Vector3 finalPos = sample.position + sample.right * lateralOffset;
            Quaternion finalRot = Quaternion.LookRotation(sample.forward, sample.up);
            
            // 6. Velocity (for Rigidbody)
            Vector3 forwardVel = sample.forward * Speed;
            Vector3 totalVel = forwardVel + lateralVel;
            
            return new FrameResult(finalPos, finalRot, totalVel, lateralVel, pathCompleted, nextPath);
        }
    }
}
