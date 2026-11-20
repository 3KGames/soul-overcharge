using Game.Common.Runtime.Movement;
using Game.Enemies.Flocking;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Enemies.Flocking
{
    // Burst-compatible minimal data for flocking (no managed references)
    [StructLayout(LayoutKind.Sequential)]
    public struct FlockFollowerData
    {
        public float DistanceAlongPath;
        public int CurrentLane;
        public float Speed;
    }
    
    public class FlockSystem : MonoBehaviour
    {
        [Header("Settings")]
        public float NeighborSearchRadius = 20f;
        public int MaxNeighbors = 20;
        public float SeparationStrength = 1f;
        public float AlignmentStrength = 0.5f;
        public float CohesionStrength = 0.3f;
        
        // Internal buffers (reused)
        private NativeArray<FlockFollowerData> _followers;
        private NativeArray<FlockAgentData> _agents;
        private NativeArray<Vector3> _lateralForces;
        private NativeArray<float> _speedModifiers;
        private NativeArray<int> _neighborCounts;
        
        private int _maxAgents = 256;
        
        void OnDestroy()
        {
            DisposeBuffers();
        }
        
        private void EnsureBuffers(int count)
        {
            if (_followers.IsCreated && _followers.Length >= count) return;
            
            DisposeBuffers();
            _maxAgents = Mathf.NextPowerOfTwo(Mathf.Max(count, 64));
            
            _followers = new NativeArray<FlockFollowerData>(_maxAgents, Allocator.Persistent);
            _agents = new NativeArray<FlockAgentData>(_maxAgents, Allocator.Persistent);
            _lateralForces = new NativeArray<Vector3>(_maxAgents, Allocator.Persistent);
            _speedModifiers = new NativeArray<float>(_maxAgents, Allocator.Persistent);
            _neighborCounts = new NativeArray<int>(_maxAgents, Allocator.Persistent);
        }
        
        private void DisposeBuffers()
        {
            if (_followers.IsCreated) _followers.Dispose();
            if (_agents.IsCreated) _agents.Dispose();
            if (_lateralForces.IsCreated) _lateralForces.Dispose();
            if (_speedModifiers.IsCreated) _speedModifiers.Dispose();
            if (_neighborCounts.IsCreated) _neighborCounts.Dispose();
        }
        
        /// <summary>
        /// Call once per frame BEFORE EnemyController.FixedUpdate.
        /// Fills flockData.LateralForce and flockData.SpeedMultiplier for each agent.
        /// </summary>
        public void ComputeFlockForces(
            SplineFollower[] followers,
            NativeArray<FlockAgentData> agents,
            float dt,
            out NativeArray<Vector3> lateralForces,
            out NativeArray<float> speedModifiers)
        {
            int count = followers.Length;
            EnsureBuffers(count);
            
            // Copy relevant data from SplineFollower to Burst-compatible struct
            for (int i = 0; i < count; i++)
            {
                _followers[i] = new FlockFollowerData
                {
                    DistanceAlongPath = followers[i].DistanceAlongPath,
                    CurrentLane = followers[i].CurrentLane,
                    Speed = followers[i].Speed
                };
            }
            _agents.CopyFrom(agents);
            
            // Clear outputs
            for (int i = 0; i < count; i++)
            {
                _lateralForces[i] = Vector3.zero;
                _speedModifiers[i] = 1f;
                _neighborCounts[i] = 0;
            }
            
            // Run flock job (no BurstCompile due to Mathf calls, but still runs on Job System)
            var job = new FlockJob
            {
                Followers = _followers,
                Agents = _agents,
                LateralForces = _lateralForces,
                SpeedModifiers = _speedModifiers,
                NeighborCounts = _neighborCounts,
                Count = count,
                SearchRadius = NeighborSearchRadius,
                MaxNeighbors = MaxNeighbors,
                SeparationStrength = SeparationStrength,
                AlignmentStrength = AlignmentStrength,
                CohesionStrength = CohesionStrength,
                dt = dt
            };
            
            JobHandle handle = job.Schedule(count, 32);
            handle.Complete();
            
            // Copy back
            lateralForces = _lateralForces;
            speedModifiers = _speedModifiers;
            
            // Write back to managed FlockAgentData for EnemyController
            for (int i = 0; i < count; i++)
            {
                var data = agents[i];
                data.LateralForce = _lateralForces[i];
                data.SpeedMultiplier = _speedModifiers[i];
                agents[i] = data;
            }
        }
        
        struct FlockJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<FlockFollowerData> Followers;
            [ReadOnly] public NativeArray<FlockAgentData> Agents;
            [WriteOnly] public NativeArray<Vector3> LateralForces;
            [WriteOnly] public NativeArray<float> SpeedModifiers;
            [WriteOnly] public NativeArray<int> NeighborCounts;
            
            public int Count;
            public float SearchRadius;
            public int MaxNeighbors;
            public float SeparationStrength;
            public float AlignmentStrength;
            public float CohesionStrength;
            public float dt;
            
            public void Execute(int i)
            {
                if (!Agents[i].CanFlock) return;
                
                var myFollower = Followers[i];
                
                float separation = 0f;
                float alignment = 0f;
                float cohesion = 0f;
                int neighbors = 0;
                
                float myDist = myFollower.DistanceAlongPath;
                
                // Spatial hash: 1D by distance along path
                // For simplicity, check all (optimize later with grid)
                for (int j = 0; j < Count; j++)
                {
                    if (i == j) continue;
                    if (!Agents[j].CanFlock) continue;
                    
                    var otherFollower = Followers[j];
                    
                    float otherDist = otherFollower.DistanceAlongPath;
                    float dist = math.abs(otherDist - myDist);
                    
                    if (dist < SearchRadius)
                    {
                        neighbors++;
                        
                        // Separation: push away laterally
                        int laneDiff = otherFollower.CurrentLane - myFollower.CurrentLane;
                        if (laneDiff != 0)
                        {
                            separation += -math.sign(laneDiff) * (1f / math.max(1, math.abs(laneDiff)));
                        }
                        
                        // Alignment: match speed
                        alignment += otherFollower.Speed;
                        
                        // Cohesion: move toward center of mass (distance)
                        cohesion += otherDist;
                    }
                    
                    if (neighbors >= MaxNeighbors) break;
                }
                
                if (neighbors > 0)
                {
                    alignment /= neighbors;
                    cohesion = (cohesion / neighbors) - myDist; // Relative to me
                    
                    // Apply weights
                    float latForce = separation * SeparationStrength 
                                   + (alignment - myFollower.Speed) * AlignmentStrength * 0.1f
                                   + cohesion * CohesionStrength * 0.01f;
                    
                    LateralForces[i] = new Vector3(latForce, 0, 0); // X = lateral in path space
                    SpeedModifiers[i] = 1f + (alignment - myFollower.Speed) * 0.01f;
                    NeighborCounts[i] = neighbors;
                }
            }
        }
    }
}