using UnityEngine;

public class RoadSegment : MonoBehaviour
{
    [Header("Точки стыковки")]
    public Transform entryPoint;
    public Transform exitPoint;

    [Header("Тип сегмента (для логики чередования)")]
    public SegmentType segmentType = SegmentType.Straight;

    public enum SegmentType
    {
        Straight,
        TurnLeft,
        TurnRight,
        HillUp,
        HillDown
    }

    void OnDrawGizmos()
    {
        if (entryPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(entryPoint.position, 0.3f);
            Gizmos.DrawRay(entryPoint.position, entryPoint.forward * 2f);
        }

        if (exitPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(exitPoint.position, 0.3f);
            Gizmos.DrawRay(exitPoint.position, exitPoint.forward * 2f);
        }
    }
}