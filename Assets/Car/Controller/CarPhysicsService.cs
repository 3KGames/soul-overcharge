using Car.Gears;
using UnityEngine;

namespace Car.Controller
{
    public class CarPhysicsService
    {
        private const float Downforce = 100f;
        private const float DriftFrictionCoefficient = 0.3f;
        private const float SideFrictionCoefficient = 5f;

        public void UpdatePhysics(Rigidbody rb, CarPhysicsInput inputData)
        {
            IGear gear = inputData.Gear;
            
            AlignToRoad(rb, inputData.RoadNormal);
            
            // Auto‑acceleration
            float speed = GetForwardSpeed(rb);
            float accel = gear.EvaluateAcceleration(speed);
            rb.AddForce(rb.transform.forward * accel, ForceMode.Acceleration);

            // Steering
            float steerAngle = gear.MaxSteerAngle * inputData.Steer;
            Quaternion delta = Quaternion.Euler(0f, steerAngle * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * delta);

            // Downforce
            rb.AddForce(-rb.transform.up * Downforce, ForceMode.Acceleration);

            // Lateral friction
            ApplyLateralFriction(rb, inputData.Drift ? DriftFrictionCoefficient : SideFrictionCoefficient);
        }

		public float GetForwardSpeed(Rigidbody rb)
		{
			return Vector3.Dot(rb.linearVelocity, rb.transform.forward);
		}

        
        private void AlignToRoad(Rigidbody rb, Vector3 roadNormal)
        {
            if (roadNormal == Vector3.zero) return;
            Quaternion toRoad = Quaternion.FromToRotation(rb.transform.up, roadNormal);
            rb.MoveRotation(toRoad * rb.rotation);
        }

        private void ApplyLateralFriction(Rigidbody rb, float coefficient = SideFrictionCoefficient)
        {
            Vector3 rightVel = Vector3.Project(rb.linearVelocity, rb.transform.right);
            rb.AddForce(-rightVel * coefficient, ForceMode.Acceleration);
        }
    }
}
