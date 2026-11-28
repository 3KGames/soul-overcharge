using Game.Common.Runtime;
using UnityEngine;

namespace Game.Common.Runtime.Debug
{
    public class RoadDebugVisualizer : MonoBehaviour
    {
        [Header("Target")]
        public IRoadPath CurrentPath;
        
        [Header("Options")]
        public bool ShowWidth = true;
        public bool ShowLanes = true;
        public bool ShowPlayerProjection = true;
        public Transform PlayerTransform;
        
        [Header("Appearance")]
        public Color WidthColor = Color.yellow;
        public Color LaneColor = Color.green;
        public Color ProjectionColor = Color.magenta;
        public float SampleStep = 5f;
        public float GizmoSize = 0.3f;
        
        void OnDrawGizmos()
        {
            if (CurrentPath == null) return;
            
            float length = CurrentPath.Length;
            int laneCount = CurrentPath.LaneCount;
            if (length <= 0f || laneCount <= 0) return;
            
            // 1. Road width (left/right edges)
            if (ShowWidth)
            {
                Gizmos.color = WidthColor;
                for (float d = 0; d <= length; d += SampleStep)
                {
                    var (left, right) = CurrentPath.GetRoadWidthAt(d);
                    var sample = CurrentPath.SampleAtDistance(d);
                    
                    Vector3 leftPos = sample.position - sample.right * left;
                    Vector3 rightPos = sample.position + sample.right * right;
                    
                    Gizmos.DrawLine(leftPos, rightPos);
                    Gizmos.DrawWireSphere(leftPos, GizmoSize * 0.5f);
                    Gizmos.DrawWireSphere(rightPos, GizmoSize * 0.5f);
                }
            }
            
            // 2. Lane centers (only active lanes)
            if (ShowLanes)
            {
                Gizmos.color = LaneColor;
                
                for (int lane = 0; lane < laneCount; lane++)
                {
                    // Skip lanes that don't exist at segment start
                    if (!CurrentPath.IsLaneActiveAt(lane, 0f)) continue;
                    
                    float offset = CurrentPath.GetLaneOffset(lane);
                    Vector3 prevPos = Vector3.zero;
                    bool first = true;
                    
                    for (float d = 0; d <= length; d += SampleStep)
                    {
                        // Skip drawing where lane is inactive (narrowing)
                        if (!CurrentPath.IsLaneActiveAt(lane, d)) continue;
                        
                        var sample = CurrentPath.SampleAtDistance(d);
                        Vector3 pos = sample.position + sample.right * offset;
                        
                        if (!first)
                            Gizmos.DrawLine(prevPos, pos);
                        
                        prevPos = pos;
                        first = false;
                    }
                }
            }
            
            // 3. Player projection
            if (ShowPlayerProjection && PlayerTransform != null)
            {
                Gizmos.color = ProjectionColor;
                var proj = CurrentPath.GetClosestSample(PlayerTransform.position);
                
                Gizmos.DrawSphere(proj.position, GizmoSize);
                Gizmos.DrawLine(PlayerTransform.position, proj.position);
                
                // Show lane info
                int lane = CurrentPath.GetLaneAtPosition(PlayerTransform.position);
                Gizmos.DrawWireSphere(proj.position + Vector3.up * 0.5f, GizmoSize);
            }
        }
    }
}