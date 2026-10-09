using Unity.Cinemachine;
using UnityEngine;

namespace Game.Car.Controller
{
    [AddComponentMenu("Cinemachine/Procedural/Rotation Control/Cinemachine Car Look At")]
    [SaveDuringPlay]
    [DisallowMultipleComponent]
    [CameraPipeline(CinemachineCore.Stage.Aim)]
    [RequiredTarget(RequiredTargetAttribute.RequiredTargets.LookAt)]
    public class CinemachineCarLookAt : CinemachineComponentBase
    {
		public override bool IsValid => enabled && LookAtTarget != null;
		public override CinemachineCore.Stage Stage => CinemachineCore.Stage.Aim;
		
		[Header("Static offset")]
        public float lookAheadDistance = 10f;
        public float heightOffset = 1.5f;

		[Header("Dynamic offset")]
        public float maxSideOffset = 2f;
        public float angularOffsetStrength = 1f;
		
		[Header("Smoothing")]
        public float offsetSmoothTime = 0.2f;
        public float speedSmoothTime = 0.1f;

        private float _currentSideOffset;
		private float _sideOffsetVelocity;
		
        private Vector3 _smoothedAngularVelocity;
        private Vector3 _dampingAngularVelocity;
		
        private Vector3 _smoothedLinearVelocity;
        private Vector3 _dampingLinearVelocity;
		
        private Rigidbody _targetRb;
        private Transform _prevTarget;

        public override void PrePipelineMutateCameraState(ref CameraState state, float deltaTime)
        {
            if (!IsValid) return;

            if (_prevTarget != LookAtTarget)
            {
                _prevTarget = LookAtTarget;
                _targetRb = LookAtTarget.GetComponent<Rigidbody>();
                
                ResetSmoothingState();
            }
        }
	
        public override void MutateCameraState(ref CameraState curState, float deltaTime)
        {
            if (!IsValid) 
				return;

            Transform car = LookAtTarget;

            // Velocity interpolation
            _smoothedLinearVelocity = Vector3.SmoothDamp(
                _smoothedLinearVelocity,
                _targetRb.linearVelocity,
                ref _dampingLinearVelocity,
                speedSmoothTime,
                Mathf.Infinity,
                deltaTime
            );
			
            _smoothedAngularVelocity = Vector3.SmoothDamp(
                _smoothedAngularVelocity,
                _targetRb.angularVelocity,
                ref _dampingAngularVelocity,
                speedSmoothTime,
                Mathf.Infinity,
                deltaTime
            );

            // Side offset
            float localYawRate = Vector3.Dot(_smoothedAngularVelocity, car.up);
            float targetSideOffset = localYawRate * angularOffsetStrength;
            targetSideOffset = Mathf.Clamp(targetSideOffset, -maxSideOffset, maxSideOffset);
			
            _currentSideOffset = Mathf.SmoothDamp(
                _currentSideOffset,
                targetSideOffset,
                ref _sideOffsetVelocity,
                offsetSmoothTime,
                Mathf.Infinity,
                deltaTime
            );

            Vector3 basePoint = car.position + (_smoothedLinearVelocity.normalized * lookAheadDistance + car.up * heightOffset);
            Vector3 finalLookPoint = basePoint + car.right * _currentSideOffset;
			
            // Rotation
            Vector3 dirToLookPoint = finalLookPoint - curState.GetCorrectedPosition();

            if (dirToLookPoint.sqrMagnitude > Epsilon)
            {
                curState.RawOrientation = Quaternion.LookRotation(dirToLookPoint, curState.ReferenceUp);
            }
        }
		
        public override void ForceCameraPosition(Vector3 pos, Quaternion rot)
        {
            base.ForceCameraPosition(pos, rot);
            ResetSmoothingState();
        }

        private void ResetSmoothingState()
        {
            _currentSideOffset = 0f;
            _sideOffsetVelocity = 0f;
            
            _dampingAngularVelocity = Vector3.zero;
            _dampingLinearVelocity = Vector3.zero;
            
            if (_targetRb != null)
            {
                _smoothedLinearVelocity = _targetRb.linearVelocity;
                _smoothedAngularVelocity = _targetRb.angularVelocity;
            }
            else
            {
                _smoothedLinearVelocity = Vector3.zero;
                _smoothedAngularVelocity = Vector3.zero;
            }
        }
    }
}