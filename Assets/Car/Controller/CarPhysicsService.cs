using Car.Gears;
using UnityEngine;

namespace Car.Controller
{
    public class CarPhysicsService
    {
        private const float Downforce = 0f;
        private const float DriftFrictionMultiplier = 0.3f;

        public void UpdatePhysics(Rigidbody rb, float steerInput, bool drift, GearData.Gear gear, Vector3 roadNormal)
        {
            AlignToRoad(rb, roadNormal);

            // Auto‑acceleration
            float speed = rb.linearVelocity.magnitude;
            float t = Mathf.InverseLerp(0f, gear.maxSpeed, speed);
            float accel = gear.accelerationCurve.Evaluate(t);
            Debug.Log($"Speed: {speed};   T: {t};   Accel: {accel}");

            rb.AddForce(rb.transform.forward * accel, ForceMode.Acceleration);

            // Steering
            float steerAngle = gear.maxSteerAngle * steerInput;
            Quaternion delta = Quaternion.Euler(0f, steerAngle * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * delta);

            // Downforce
            rb.AddForce(-rb.transform.up * Downforce, ForceMode.Acceleration);

            // Drift (reduce lateral velocity)
            if (drift)
            {
                Vector3 lateral = Vector3.Project(rb.linearVelocity, rb.transform.right);
                rb.AddForce(-lateral * DriftFrictionMultiplier, ForceMode.Acceleration);
            }
        }

        private void AlignToRoad(Rigidbody rb, Vector3 roadNormal)
        {
            if (roadNormal == Vector3.zero) return;
            Quaternion toRoad = Quaternion.FromToRotation(rb.transform.up, roadNormal);
            rb.MoveRotation(toRoad * rb.rotation);
        }
    }

}
