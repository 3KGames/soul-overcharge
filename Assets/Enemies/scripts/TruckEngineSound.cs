using UnityEngine;
using VContainer;
using FMODUnity;
using FMOD.Studio;
using Car.Gears;
using Enemies;

public class TruckEngineSound : MonoBehaviour
{
    [SerializeField] 
    private EventReference engineEvent;

	[SerializeField] private SoulVOZ truck;

    private EventInstance _engineInstance;

    private void Start()
    {
        if (!engineEvent.IsNull)
        {
            _engineInstance = RuntimeManager.CreateInstance(engineEvent);
            
            RuntimeManager.AttachInstanceToGameObject(_engineInstance, gameObject);
            
			_engineInstance.setParameterByName("Speed Normalized", Mathf.InverseLerp(truck.MinSpeed, truck.MaxSpeed, truck.CurSpeed));
            _engineInstance.start();
        }
	}
	
    private void OnDestroy()
    {
        if (_engineInstance.isValid())
        {
            _engineInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _engineInstance.release();
        }
    }
}