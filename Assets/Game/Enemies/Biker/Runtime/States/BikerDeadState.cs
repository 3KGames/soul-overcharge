using Common.Runtime.StateMachine;
using Enemies.Biker.Runtime;
using UnityEngine;

public class BikerDeadState : IUpdatableState<BikerStateType>
{
	private readonly BikerContext _ctx;

	public BikerStateType Kind => BikerStateType.Dead;

	public BikerDeadState(BikerContext ctx)
	{
		_ctx = ctx;
	}

	public void Enter()
	{
		_ctx.Animator.SetTrigger("Die");
        
		_ctx.Rb.constraints = RigidbodyConstraints.None;
	}

	public void Exit() { }

	public void Update()
	{
		
	}
}