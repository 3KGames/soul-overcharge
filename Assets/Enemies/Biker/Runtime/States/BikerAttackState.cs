using Common.Runtime.StateMachine;
using Enemies.Biker.Runtime;
using UnityEngine;

public class BikerAttackState : IUpdatableState<BikerStateType>
{
	private readonly BikerContext _ctx;
	private readonly IStateSwitcher<BikerStateType> _switcher;
	private bool _isAnimationStarted;

	public BikerStateType Kind => BikerStateType.Attack;

	public BikerAttackState(BikerContext ctx, IStateSwitcher<BikerStateType> switcher)
	{
		_ctx = ctx;
		_switcher = switcher;
	}

	public void Enter()
	{
		_ctx.Animator.SetTrigger("Attack");
		_isAnimationStarted = false;
	}

	public void Exit()
	{
		//_ctx.CloseAttackZone();
	}

	public void Update()
	{
		AnimatorStateInfo stateInfo = _ctx.Animator.GetCurrentAnimatorStateInfo(0);

		if (stateInfo.IsName("Attack Reverse"))
		{
			_switcher.Switch(BikerStateType.Parallel);
			return;
		}

		float distanceDiff = _ctx.DistanceToPlayer + _ctx.EffectiveDistanceOffset;
		float targetLateral = _ctx.GetLaneCenter(_ctx.TargetLane);
        
		_ctx.ApplyParallelMovement(distanceDiff, targetLateral);
	}
}