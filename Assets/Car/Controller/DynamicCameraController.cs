using UnityEngine;
using Unity.Cinemachine;
using VContainer;
using Car.Controller;

public class DynamicCameraController : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float _maxSpeed = 20f;

    [Header("Orthographic Size (Zoom)")]
    [SerializeField] private float _minOrthoSize = 5f;
    [SerializeField] private float _maxOrthoSize = 10f;

    [Header("Follow Offset Z (Distance)")]
    [SerializeField] private float _minOffsetZ = -2.96f;
    [SerializeField] private float _maxOffsetZ = -8f;

    [Header("Smoothing")]
    [SerializeField] private float _smoothTime = 0.4f;

    private CarController _carController;
    private Rigidbody _rb;

    private CinemachineCamera _vcam;
    private CinemachineFollow _follow;

    private float _orthoSizeVelocity;
    private float _zOffsetVelocity;

    [Inject]
    public void Construct(CarController carController)
    {
        _carController = carController;
    }

    private void Awake()
    {
        _vcam = GetComponent<CinemachineCamera>();

        if (_vcam == null)
        {
            // Debug.LogError("[DynamicCameraController] CinemachineCamera не найдена!", this);
            enabled = false;
            return;
        }

        _follow = _vcam.GetCinemachineComponent(CinemachineCore.Stage.Body)
            as CinemachineFollow;

        if (_follow == null)
        {
            // Debug.LogError("[DynamicCameraController] CinemachineFollow не найден!", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (_rb == null)
        {
            _rb = _carController?.RB;
            return;
        }

        float speedFactor = Mathf.Clamp01(_rb.linearVelocity.magnitude / _maxSpeed);

        ApplyOrthoSize(speedFactor);
        ApplyFollowOffsetZ(speedFactor);
    }

    private void ApplyOrthoSize(float speedFactor)
    {
        float target = Mathf.Lerp(_minOrthoSize, _maxOrthoSize, speedFactor);

        _vcam.Lens.OrthographicSize = Mathf.SmoothDamp(
            _vcam.Lens.OrthographicSize,
            target,
            ref _orthoSizeVelocity,
            _smoothTime
        );
    }

    private void ApplyFollowOffsetZ(float speedFactor)
    {
        float targetZ = Mathf.Lerp(_minOffsetZ, _maxOffsetZ, speedFactor);

        Vector3 offset = _follow.FollowOffset;
        offset.z = Mathf.SmoothDamp(
            offset.z,
            targetZ,
            ref _zOffsetVelocity,
            _smoothTime
        );
        _follow.FollowOffset = offset;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_maxSpeed <= 0f)               _maxSpeed = 1f;
        if (_maxOrthoSize < _minOrthoSize) _maxOrthoSize = _minOrthoSize;
    }
#endif
}