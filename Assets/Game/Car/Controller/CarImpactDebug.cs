using UnityEngine;

namespace Game.Car.Controller
{
	public class CarImpactDebug : MonoBehaviour
	{
	    public Rigidbody rb;
	    
	    public float impactForce = 15000f;
	    public float lengthOffset = 2f;
	    public float sideOffset = 1f;
	    public KeyCode hitRightFront = KeyCode.Alpha7;
	    public KeyCode hitRightCenter = KeyCode.Alpha8;
	    public KeyCode hitRightRear = KeyCode.Alpha9;
	    public KeyCode hitLeftFront = KeyCode.Alpha4;
	    public KeyCode hitLeftCenter = KeyCode.Alpha5;
	    public KeyCode hitLeftRear = KeyCode.Alpha6;
	
	    void Update()
	    {
	        if (UnityEngine.Input.GetKeyDown(hitRightFront))
	            ApplyImpact(new Vector3(sideOffset, 0, lengthOffset), -transform.right);
	
	        if (UnityEngine.Input.GetKeyDown(hitRightCenter))
	            ApplyImpact(new Vector3(sideOffset, 0, 0), -transform.right);
	
	        if (UnityEngine.Input.GetKeyDown(hitRightRear))
	            ApplyImpact(new Vector3(sideOffset, 0, -lengthOffset), -transform.right);
	
	
	        if (UnityEngine.Input.GetKeyDown(hitLeftFront))
	            ApplyImpact(new Vector3(-sideOffset, 0, lengthOffset), transform.right);
	
	        if (UnityEngine.Input.GetKeyDown(hitLeftCenter))
	            ApplyImpact(new Vector3(-sideOffset, 0, 0), transform.right);
	
	        if (UnityEngine.Input.GetKeyDown(hitLeftRear))
	            ApplyImpact(new Vector3(-sideOffset, 0, -lengthOffset), transform.right);
	    }
	
	    private void ApplyImpact(Vector3 localHitPoint, Vector3 forceDirection)
	    {
	        Vector3 worldHitPoint = transform.TransformPoint(localHitPoint);
	        Vector3 forceVector = forceDirection * impactForce;
	        rb.AddForceAtPosition(forceVector, worldHitPoint, ForceMode.Impulse);
	        Debug.DrawRay(worldHitPoint, forceDirection * 3f, Color.red, 2f);
	    }
	}
}