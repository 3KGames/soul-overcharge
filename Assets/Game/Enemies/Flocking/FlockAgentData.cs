using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Game.Enemies.Flocking
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FlockAgentData
    {
        public float SeparationRadius;
        public float AlignmentWeight;
        public float CohesionWeight;
        public bool CanFlock;
        
        // Output (written by FlockSystem)
        public Vector3 LateralForce;
        public float SpeedMultiplier;
    }
}