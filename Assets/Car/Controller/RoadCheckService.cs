using UnityEngine;

namespace Car.Controller
{
    public class RoadCheckService
    {
        private readonly LayerMask _surfaceMask;
        private const float RayDistance = 3f;

        public RoadCheckService(LayerMask surfaceMask) => _surfaceMask = surfaceMask;

        public Vector3 GetRoadNormal(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up, Vector3.down, out var hit, RayDistance, _surfaceMask))
                return hit.normal;
            return Vector3.up; // fallback
        }
    }
}

