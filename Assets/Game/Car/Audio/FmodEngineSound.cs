using Car.Controller;
using Car.Controller.CarPhysics;
using Car.Controller.CarPhysics.States;
using UnityEngine;
using VContainer;
using FMODUnity;
using FMOD.Studio;
using Car.Gears;

public class FmodEngineSound : MonoBehaviour
{
    [Header("FMOD Settings")]
    [SerializeField] 
    private EventReference engineEvent;
	[SerializeField] 
	private EventReference driftEvent;
	
	[SerializeField]
	private float maxSlipSpeed = 10f;

    private EventInstance _engineInstance;
	private EventInstance _driftInstance;
    private TransmissionService _transmission;
	private InputService _inputService;
	private CarController _carController;

    [Inject]
    public void Construct(TransmissionService transmission, InputService inputService, CarController carController)
    {
        _transmission = transmission;
		_inputService = inputService;
		_carController = carController;
    }

    private void Start()
    {
        if (!engineEvent.IsNull)
        {
            _engineInstance = RuntimeManager.CreateInstance(engineEvent);
            
            RuntimeManager.AttachInstanceToGameObject(_engineInstance, gameObject, GetComponent<Rigidbody>());
            
            _engineInstance.start();
        }

        _transmission.RpmChanged += UpdateFmodRpm;
		
		_inputService.OnThrottleChanged += HandleThrottleChanged;
		
		if (!driftEvent.IsNull)
		{
			_driftInstance = RuntimeManager.CreateInstance(driftEvent);
            
			RuntimeManager.AttachInstanceToGameObject(_driftInstance, gameObject, GetComponent<Rigidbody>());
            
			_driftInstance.start();
		}
	}

	private void Update()
	{
		Vector3 worldVelocity = _carController.RB.linearVelocity;

		Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

		float lateralSlip = Mathf.Abs(localVelocity.x);

		float normalizedSlip = Mathf.Clamp01(lateralSlip / maxSlipSpeed);

		if (!_carController.IsDrifting)
		{
			normalizedSlip = 0f;
		}
		
		_driftInstance.setParameterByName("Slip", normalizedSlip);

		float speed = 3.6f * CarPhysicsService.GetForwardSpeed(_carController.RB);
		_driftInstance.setParameterByName("Speed", speed);
	}

    private void UpdateFmodRpm(float currentRpm)
    {
        if (_engineInstance.isValid())
        {
            _engineInstance.setParameterByName("RPM", currentRpm);
        }
    }
	
	private void HandleThrottleChanged(float throttleValue)
	{
		if (_engineInstance.isValid())
		{
			//Debug.Log("ASLOFKAS;LFKASL;");
			_engineInstance.setParameterByName("Load", Mathf.Abs(throttleValue));
		}
	}
	
    private void OnDestroy()
    {
        if (_transmission != null)
        {
            _transmission.RpmChanged -= UpdateFmodRpm;
        }
		
		if (_inputService != null)
		{
			_inputService.OnThrottleChanged -= HandleThrottleChanged;
		}

        if (_engineInstance.isValid())
        {
            _engineInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _engineInstance.release();
        }
		
		if (_driftInstance.isValid())
		{
			_driftInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			_driftInstance.release();
		}
    }
}