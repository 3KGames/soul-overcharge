using UnityEngine;
using Unity.Cinemachine;
using VContainer;
using Car.Controller;
using Game.Car.Controller;
using NaughtyAttributes;

public class DynamicCameraController : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float _maxSpeed = 20f;

    [Header("Orthographic Size (Zoom)")]
	[MinMaxSlider(0f, 120f)]
    [SerializeField] private Vector2 _minMaxFOV = new (70f, 100);

    [Header("Follow Offset Z (Distance)")]

	[MinMaxSlider(-30f, 30f)]
	[SerializeField] private Vector2 _minMaxOffsetZ = new (-8f, -2.96f);

    [Header("Smoothing")]
    [SerializeField] private float _smoothTime = 0.4f;

    private CarController _carController;
    private Rigidbody _rb;

    private CinemachineCamera _vcam;
    private CinemachineCarFollow _follow;

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
            enabled = false;
            return;
        }

        _follow = _vcam.GetCinemachineComponent(CinemachineCore.Stage.Body)
            as CinemachineCarFollow;

        if (_follow == null)
        {
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

        float speedFactor = Mathf.Clamp01(_rb.linearVelocity.magnitude / (_maxSpeed / 3.6f));

        ApplyFOV(speedFactor);
        ApplyFollowOffsetZ(speedFactor);
    }

    private void ApplyFOV(float speedFactor)
    {
        float target = Mathf.Lerp(_minMaxFOV.x, _minMaxFOV.y, speedFactor);

		target = Camera.HorizontalToVerticalFieldOfView(target, Camera.main.aspect);

		_vcam.Lens.FieldOfView = target;
	}

    private void ApplyFollowOffsetZ(float speedFactor)
    {
        float targetZ = Mathf.Lerp(_minMaxOffsetZ.y, _minMaxOffsetZ.x, speedFactor);

        Vector3 offset = _follow.offset;
        offset.z = Mathf.SmoothDamp(
            offset.z,
            targetZ,
            ref _zOffsetVelocity,
            _smoothTime
        );
        _follow.offset = offset;
    }
}