using Car.Controller;
using UnityEngine;
using VContainer;
using FMODUnity;
using FMOD.Studio;
using Car.Gears;

public class FmodEngineSound : MonoBehaviour
{
    [Header("FMOD Settings")]
    [SerializeField, Tooltip("Выберите ивент двигателя из FMOD")] 
    private EventReference engineEvent;

    private EventInstance _engineInstance;
    private TransmissionService _transmission;
	private InputService _inputService;

    [Inject]
    public void Construct(TransmissionService transmission, InputService inputService)
    {
        _transmission = transmission;
		_inputService = inputService;
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

        if (_engineInstance.isValid())
        {
            _engineInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _engineInstance.release();
        }
    }
}