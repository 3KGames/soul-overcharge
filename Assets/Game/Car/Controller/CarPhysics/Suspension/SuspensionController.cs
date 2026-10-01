using System;
using Car.Controller;
using Car.Controller.CarPhysics;
using Car.Controller.CarPhysics.Drivetrain;
using NaughtyAttributes;
using UnityEngine;
using VContainer;

namespace Game.Car.Controller.CarPhysics.Suspension
{
	public class SuspensionController : MonoBehaviour
	{
		private CarPhysicsService _carPhysicsService;
		private DrivetrainService _drivetrainService;
		private InputService _inputService;
		
		[SerializeField] private WheelAnchor[] wheels;
		[SerializeField] private LayerMask groundMask;
		[SerializeField] private Rigidbody rb; // TODO: Car rigidbody inject?
		[SerializeField] private float steerRate = 240f;

		[Header("Bump Stop Collider")]
		[Tooltip("Cylinder mesh")]
		[SerializeField] private Mesh bumpStopMesh;
		[SerializeField] private PhysicsMaterial bumpStopPhysicMaterial;

		private float _steerDeg;

		private float[] _massPerWheel;

		[Inject]
		public void Construct(CarPhysicsService carPhysicsService, DrivetrainService drivetrainService, InputService inputService)
		{
			_carPhysicsService =  carPhysicsService;
			_drivetrainService = drivetrainService;
			_inputService = inputService;
		}
		
		private void Awake()
		{
			ValidateWheels();
			CalculateWeightDistibution();
			SpawnBumpStopColliders();
		}

		private void SpawnBumpStopColliders()
		{
			if (bumpStopMesh == null)
				return;

			Bounds bounds = bumpStopMesh.bounds;
			float radialExtent = Mathf.Max(bounds.size.x, bounds.size.z);
			float axialExtent = bounds.size.y;
			
			for (int i = 0; i < wheels.Length; i++)
			{
				WheelAnchor anchor = wheels[i];
				if (anchor == null)
					continue;
				SuspensionSO s = anchor.Settings;
				if (s == null)
					continue;

				Vector3 scale = new Vector3(
					s.TireWidth / axialExtent,
					(2f * s.WheelRadius) / radialExtent,
					(2f * s.WheelRadius) / radialExtent);

				Vector3 mountLocal = transform.InverseTransformPoint(anchor.MountWorld);
				Vector3 stopLocal = mountLocal + Vector3.down * s.MinLength;

				var go = new GameObject($"BumpStop_{i}");
				go.transform.SetParent(transform, false);
				go.transform.localPosition = stopLocal;
				go.transform.localRotation = Quaternion.identity;
				go.transform.localScale = scale;

				MeshCollider mc = go.AddComponent<MeshCollider>();
				mc.sharedMesh = bumpStopMesh;
				mc.convex = true;
				mc.material = bumpStopPhysicMaterial;
			}
		}

		private void ValidateWheels()
		{
			for (int i = 0; i < wheels.Length; i++)
			{
				if (wheels[i] == null)
					Debug.LogWarning($"\u005BDrivetrainView\u005D Wheel {i}: WheelRig isn't assigned", this);
				else if (wheels[i].settings == null)
					Debug.LogWarning($"\u005BDrivetrainView\u005D Wheel {i}: SuspensionSettings isn't assigned", this);
				else if (wheels[i].Hub == null)
					Debug.LogWarning($"\u005BDrivetrainView\u005D Wheel {i}: hub isn't assigned", this);
			}
		}

		private void FixedUpdate()
		{
			float dt = Time.fixedDeltaTime;

			Vector3 axis = rb.rotation * Vector3.up;
			float forwardSpeed = CarPhysicsService.GetForwardSpeed(rb);

			for (int i = 0; i < wheels.Length; i++)
			{
				WheelAnchor anchor = wheels[i];
				if (anchor == null)
					continue;
				SuspensionSO s = anchor.Settings;
				if (s == null)
					continue;

				Vector3 mountWorld = anchor.MountWorld;

				float probeRadius = s.TireWidth * 0.5f;
				float maxCastDist = s.MaxLength + s.WheelRadius + probeRadius + 0.05f;
				bool hit = Physics.SphereCast(mountWorld, probeRadius, -axis, out RaycastHit hitInfo, maxCastDist, groundMask);

				float groundDist = hit
					? hitInfo.distance + probeRadius
					: float.PositiveInfinity;

				float dist = Math.Min(s.MaxLength, groundDist - s.WheelRadius);
				dist = Math.Max(dist, 0f);
				anchor.D = dist;

				float comp = s.MaxLength - dist;
				comp = Mathf.Clamp(comp, 0f, s.Travel);
				anchor.Comp = comp;

				float velComp = Vector3.Dot(rb.GetPointVelocity(mountWorld), axis);

				// a = springCoeff · comp, springCoeff = ω0² = (2π·f0)²
				float accel = comp * s.SpringCoeff;

				// a = damperCoeff · v, damperCoeff = 2ζ·ω0
				if (comp > 0f)
					accel -= velComp * s.DamperCoeff;

				rb.AddForceAtPosition(accel * _massPerWheel[i] * axis, mountWorld, ForceMode.Force);

				float omega = anchor.Driven
					? _drivetrainService.DrivenWheelsOmega //TODO: Conversion by radius
					: forwardSpeed / s.WheelRadius;
				anchor.SpinDeg = Mathf.Repeat(anchor.SpinDeg + omega * Mathf.Rad2Deg * dt, 360f);
			}

			float targetSteer = _inputService.Steer * _drivetrainService.GetSteeringAngle(forwardSpeed);
			_steerDeg = Mathf.MoveTowards(_steerDeg, targetSteer, steerRate * dt);
		}

		private void LateUpdate()
		{
			Vector3 axis = rb.rotation * Vector3.up;

			for (int i = 0; i < wheels.Length; i++)
			{
				WheelAnchor anchor = wheels[i];
				if (anchor == null)
					continue;

				anchor.ApplyPose(axis, _steerDeg);
			}
		}
		
		[Button]
		private void CalculateWeightDistibution()
		{
			_massPerWheel =  new float[wheels.Length];
			float totalShares = 0f;

			for (int i = 0; i < wheels.Length; i++)
			{
				totalShares += wheels[i].loadShare;
			}

			for (int i = 0; i < wheels.Length; i++)
			{
				_massPerWheel[i] = rb.mass * (wheels[i].loadShare /  totalShares);
			}
		}

		private void OnDrawGizmosSelected()
		{
			if (wheels == null)
				return;

			for (int i = 0; i < wheels.Length; i++)
			{
				WheelAnchor anchor = wheels[i];
				if (anchor == null)
					continue;

				Vector3 mountWorld = anchor.MountWorld;
				SuspensionSO s = anchor.Settings;
				if (s == null)
					continue;

				Gizmos.color = Color.yellow;
				Gizmos.DrawRay(mountWorld, -transform.up * (s.MaxLength + s.WheelRadius));
			}
		}
	}
}