using Car.Controller;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Car.Controller
{
    [AddComponentMenu("Cinemachine/Procedural/Position Control/Cinemachine Car Follow")]
    [SaveDuringPlay]
    [DisallowMultipleComponent]
    [CameraPipeline(CinemachineCore.Stage.Body)]
    [RequiredTarget(RequiredTargetAttribute.RequiredTargets.Tracking)]
    public class CinemachineCarFollow : CinemachineComponentBase
	{
		public override bool IsValid => enabled && FollowTarget != null;
		public override CinemachineCore.Stage Stage => CinemachineCore.Stage.Body;
		
		[Header("Speed when camera tracks only velocity")]
		public float speedForVelocityTracking = 50f;
		
        [Header("Position")]
        public Vector3 offset = new Vector3(0f, 2f, -6f);

        [Header("Dynamics (Local Space: X=Side, Y=Up, Z=Forward)")]
        [Tooltip("Strength of spring")]
        public Vector3 convergence = new Vector3(25f, 40f, 20f);

        [Tooltip("Damping")]
        public Vector3 damping = new Vector3(8f, 14f, 7f);
		
		[Tooltip("Camera inertia (0-1")]
		public Vector3 momentum = new Vector3(1f, 0.5f, 1f);

		private CarController _targetCarController;
        private bool _isInitialized;
		private Vector3 _currentPosition;
		private Vector3 _currentVelocity;
		private Transform _prevTarget;

		private float VelocityTrackingSpeedMS => speedForVelocityTracking / 3.6f; 

		public override void PrePipelineMutateCameraState(ref CameraState state, float deltaTime)
		{
			if (!IsValid) return;

			if (_prevTarget != FollowTarget)
			{
				_prevTarget = FollowTarget;
				_targetCarController = FollowTarget.GetComponent<CarController>();
        
				Vector3 targetWorldPos = FollowTarget.position + (FollowTarget.rotation * offset);
				ForceCameraPosition(targetWorldPos, state.RawOrientation);
			}
		}
		
        public override void MutateCameraState(ref CameraState curState, float deltaTime)
        {
            if (!IsValid) 
				return;

            Transform car = FollowTarget;
			Vector3 carVelocity = _targetCarController.LinearVelocity;
			
			// Offset lerp
			Quaternion referenceRotation = car.rotation;
			float speedSq = carVelocity.sqrMagnitude;
			if (speedSq > Epsilon)
			{
				Vector3 flatVelocity = carVelocity;
				flatVelocity.y = 0;
                
				if (flatVelocity.sqrMagnitude > Epsilon)
				{
					Quaternion velocityRotation = Quaternion.LookRotation(flatVelocity.normalized, car.up);
                    
					float speedFactor = Mathf.Clamp01(carVelocity.magnitude / VelocityTrackingSpeedMS);
					referenceRotation = Quaternion.Slerp(car.rotation, velocityRotation, speedFactor);
				}
			}

			Vector3 targetWorldPos = car.position + (referenceRotation * offset);
            Vector3 deltaWorld = targetWorldPos - _currentPosition;
			Vector3 localDelta = Quaternion.Inverse(referenceRotation) * deltaWorld;

            Vector3 relativeVelocity = _currentVelocity - carVelocity;
			Vector3 localRelVelocity = Quaternion.Inverse(referenceRotation) * relativeVelocity;

            // Convergence
            Vector3 localAccConvergence = Vector3.Scale(localDelta, convergence);

            // Damping 
            Vector3 localAccDamping = Vector3.Scale(localRelVelocity, damping);

			// Momentum & Velocity
			Vector3 frameMomentum = new Vector3(
				Mathf.Pow(momentum.x, deltaTime * 60f),
				Mathf.Pow(momentum.y, deltaTime * 60f),
				Mathf.Pow(momentum.z, deltaTime * 60f)
			);
			Vector3 localCamVel = Quaternion.Inverse(referenceRotation) * _currentVelocity;
			localCamVel = Vector3.Scale(localCamVel, frameMomentum);
			_currentVelocity = referenceRotation * localCamVel;

            // Acceleration
            Vector3 localAcceleration = localAccConvergence - localAccDamping;
			Vector3 worldAcceleration = referenceRotation * localAcceleration;

            // Position
            _currentVelocity += worldAcceleration * deltaTime;
            _currentPosition += _currentVelocity * deltaTime;

            curState.RawPosition = _currentPosition;
        }

		public override void ForceCameraPosition(Vector3 pos, Quaternion rot)
		{
			base.ForceCameraPosition(pos, rot);

			_currentPosition = pos;
            
			if (_targetCarController != null)
			{
				_currentVelocity = _targetCarController.LinearVelocity;
			}
			else
			{
				_currentVelocity = Vector3.zero;
			}
		}

		public override void OnTargetObjectWarped(Transform target, Vector3 positionDelta)
		{
			base.OnTargetObjectWarped(target, positionDelta);
            
			if (target == FollowTarget)
			{
				_currentPosition += positionDelta;
			}
		}
    }
}