using UnityEngine;

public class PreviewWithRotation : MonoBehaviour
{
	[SerializeField]
	private float rotationSpeed = 30f;

	void FixedUpdate()
	{
		transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
	}
}
