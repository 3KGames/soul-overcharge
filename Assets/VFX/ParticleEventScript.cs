using UnityEngine;

using UnityEngine;

public class ParticleEventScript : MonoBehaviour
{
    [SerializeField] private ParticleSystem psRight;
    [SerializeField] private ParticleSystem psLeft;

    public void PlayParticlesLeft()
    {
        psRight.Stop(true,
            ParticleSystemStopBehavior.StopEmitting);

        psLeft.Play();
    }

    public void PlayParticlesRight()
    {
        psLeft.Stop(true,
            ParticleSystemStopBehavior.StopEmitting);

        psRight.Play();
    }

    public void StopParticles()
    {
        psLeft.Stop(true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        psRight.Stop(true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
