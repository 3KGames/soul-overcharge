using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FMODUnity;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class SoulDrainEffect : MonoBehaviour
{
	public float flySpeed = 5f;

	[SerializeField]
	private ParticleSystem psGain;
	[SerializeField]
	private ParticleSystem psDrain;
	[SerializeField]
	private ParticleSystemForceField gainForceField;
	
	[SerializeField]
	private EventReference soulGainEvent;
	
	private CancellationTokenSource _stopGainCts;

	public void SetExternalDrainForce(ParticleSystemForceField forceField)
	{
		if (psDrain.externalForces.influenceCount == 0)
			psDrain.Play();
		psDrain.externalForces.AddInfluence(forceField);
	}

	public void RemoveExternalDrainForce(ParticleSystemForceField forceField)
	{
		psDrain.externalForces.RemoveInfluence(forceField);
		
		if (psDrain.externalForces.influenceCount == 0)
			psDrain.Stop();
	}

	public void SetExternalGainPos(Vector3 pos)
	{
		psGain.transform.position = pos;
		psGain.Play();
		RuntimeManager.PlayOneShot(soulGainEvent, transform.position);

		StopGainAfterDelayAsync().Forget();
	}

	private async UniTaskVoid StopGainAfterDelayAsync()
	{
		_stopGainCts?.Cancel();
		_stopGainCts = new CancellationTokenSource();

		try
		{
			await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: _stopGainCts.Token);
        
			RemoveExternalGainPos();
		}
		catch (OperationCanceledException)
		{
			
		}
	}

	public void RemoveExternalGainPos()
	{
		psGain.Stop();
	}
	
	private void OnDestroy()
	{
		_stopGainCts?.Cancel();
		_stopGainCts?.Dispose();
	}
}