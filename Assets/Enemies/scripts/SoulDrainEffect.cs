using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class SoulDrainEffect : MonoBehaviour
{
	public float flySpeed = 5f;

	private ParticleSystem ps;

	void Start()
	{
		ps = GetComponent<ParticleSystem>();
	}

	public void SetExternalForce(ParticleSystemForceField forceField)
	{
		if (ps.externalForces.influenceCount == 0)
			ps.Play();
		ps.externalForces.AddInfluence(forceField);
	}

	public void ResetExternalForces()
	{
		ps.externalForces.RemoveAllInfluences();
	}

	public void RemoveExternalForce(ParticleSystemForceField forceField)
	{
		ps.externalForces.RemoveInfluence(forceField);
		
		if (ps.externalForces.influenceCount == 0)
			ps.Stop();
	}
}