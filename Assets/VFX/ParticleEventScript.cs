using Car.Controller;
using Car.Controller.CarPhysics.States;
using UnityEngine;

using UnityEngine;
using VContainer;

public class ParticleEventScript : MonoBehaviour
{
    [SerializeField] private ParticleSystem psRight;
    [SerializeField] private ParticleSystem psLeft;
	
	[SerializeField] private TrailRenderer trailPrefabLeft;
	[SerializeField] private TrailRenderer trailPrefabRight;

	[SerializeField] private Transform pointLeft;
	[SerializeField] private Transform pointRight;
	
	[SerializeField] private Color roadSmokeColor = Color.white;
	[SerializeField] private Color offroadSmokeColor = Color.yellow;
	
	[Inject] private DriftCarState driftCarState;
	[Inject] private RoadCheckService roadCheckService;
	
	private TrailRenderer _currentLeft;
	private TrailRenderer _currentRight;

	public void Start()
	{
		driftCarState.OnDriftEnded += DisableTrail;
	}
	
	private void Update()
	{
		if (roadCheckService == null) return;

		if (psLeft.isEmitting)
		{
			UpdateParticleColor(psLeft, psLeft.transform.position);
		}

		if (psRight.isEmitting)
		{
			UpdateParticleColor(psRight, psRight.transform.position);
		}
	}
	
	private void UpdateParticleColor(ParticleSystem ps, Vector3 wheelPosition)
	{
		var roadInfo = roadCheckService.GetRoadInfo(wheelPosition);
        
		Color targetColor = roadInfo.isOffroad ? offroadSmokeColor : roadSmokeColor;
        
		var mainModule = ps.main;
		mainModule.startColor = targetColor;
	}
	
    public void PlayParticlesLeft()
    {
        psRight.Stop(true,
            ParticleSystemStopBehavior.StopEmitting);

		UpdateParticleColor(psLeft, psLeft.transform.position);
        psLeft.Play();
	}

    public void PlayParticlesRight()
    {
        psLeft.Stop(true,
            ParticleSystemStopBehavior.StopEmitting);

		UpdateParticleColor(psRight, psRight.transform.position);
        psRight.Play();
    }

    public void StopParticles()
    {
        psLeft.Stop(true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        psRight.Stop(true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
    }
	
	public void DisableTrail(float duration)
	{
		if (_currentLeft != null)
		{
			_currentLeft.transform.SetParent(null);
			Destroy(_currentLeft.gameObject, _currentLeft.time);
		}

		if (_currentRight != null)
		{
			_currentRight.transform.SetParent(null);
			Destroy(_currentRight.gameObject, _currentRight.time);
		}
		
		_currentLeft = null;
		_currentRight = null;
	}

	public void EnableTrail()
	{
		if (_currentLeft != null || _currentRight != null)
		{
			DisableTrail(0f); 
		}
		
		_currentLeft = Instantiate(trailPrefabLeft, pointLeft.position, pointLeft.rotation, pointLeft);
		_currentRight = Instantiate(trailPrefabRight, pointRight.position, pointRight.rotation, pointRight);
        
		_currentLeft.emitting = true;
		_currentRight.emitting = true;
	}
}
