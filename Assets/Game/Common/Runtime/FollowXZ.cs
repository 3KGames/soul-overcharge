using UnityEngine;

public class FollowXZ : MonoBehaviour
{
	public Transform target;

	void LateUpdate()
	{
		if (target == null) return;

		float newX = target.position.x;
		float newZ = target.position.z;
		float currentY = transform.position.y; 

		transform.position = new Vector3(newX, currentY, newZ);
	}
}