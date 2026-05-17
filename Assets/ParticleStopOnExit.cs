using UnityEngine;

public class ParticleStopOnExit : StateMachineBehaviour
{
    override public void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        var fx = animator.GetComponent<ParticleEventScript>();
        if (fx != null)
            fx.StopParticles();
    }
}
