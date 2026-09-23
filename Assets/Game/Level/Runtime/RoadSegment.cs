using UnityEngine;

namespace Game.Level.Runtime
{
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

			if (entryPoint != null && exitPoint != null)
			{
				float inf = 10000f;
            
				float xMin = -inf, xMax = inf;
				float zMin = -inf, zMax = inf;

				Vector3 ePos = transform.InverseTransformPoint(entryPoint.transform.position);
				Vector3 eFwd = transform.InverseTransformDirection(entryPoint.transform.forward);

				Vector3 exPos = transform.InverseTransformPoint(exitPoint.transform.position);
				Vector3 exFwd = transform.InverseTransformDirection(exitPoint.transform.forward);

				int eX = Mathf.RoundToInt(eFwd.x);
				int eZ = Mathf.RoundToInt(eFwd.z);

				if (eX == 1) xMin = ePos.x;
				else if (eX == -1) xMax = ePos.x;
            
				if (eZ == 1) zMin = ePos.z;
				else if (eZ == -1) zMax = ePos.z;

				int exX = Mathf.RoundToInt(exFwd.x);
				int exZ = Mathf.RoundToInt(exFwd.z);

				if (exX == 1) xMax = exPos.x;
				else if (exX == -1) xMin = exPos.x; 
            
				if (exZ == 1) zMax = exPos.z;
				else if (exZ == -1) zMin = exPos.z; 

				float sizeX = Mathf.Max(0, xMax - xMin);
				float sizeZ = Mathf.Max(0, zMax - zMin);
				float centerY = (ePos.y + exPos.y) / 2f;

				Vector3 center = new Vector3((xMin + xMax) / 2f, centerY, (zMin + zMax) / 2f);
				Vector3 size = new Vector3(sizeX, 0.01f, sizeZ);

				Matrix4x4 oldMatrix = Gizmos.matrix;
            
				Gizmos.matrix = transform.localToWorldMatrix; 

				Gizmos.color = new Color(0f, 0.7f, 1f, 0.25f);
				Gizmos.DrawCube(center, size);

				Gizmos.color = new Color(0f, 0.7f, 1f, 0.8f);
				Gizmos.DrawWireCube(center, size);

				Gizmos.matrix = oldMatrix;
			}
		}}
}