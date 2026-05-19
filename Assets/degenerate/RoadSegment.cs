using degenerate;
using UnityEngine;

public class RoadSegment : MonoBehaviour
{
	public RoadSegmentView roadView;
	
    [Header("Точки стыковки")]
    public ConnectionPoint entryPoint;
    public ConnectionPoint exitPoint;

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
            Gizmos.DrawSphere(entryPoint.transform.position, 0.3f);
            Gizmos.DrawRay(entryPoint.transform.position, entryPoint.transform.forward * 2f);
        }

        if (exitPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(exitPoint.transform.position, 0.3f);
            Gizmos.DrawRay(exitPoint.transform.position, exitPoint.transform.forward * 2f);
        }
    }
}