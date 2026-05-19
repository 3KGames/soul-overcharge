using Common.Runtime.StateMachine;
using Enemies.Biker.Runtime;
using UnityEngine;

public class BikerAttackState : IUpdatableState<BikerStateType>
{
	private readonly BikerContext _ctx;
	private readonly IStateSwitcher<BikerStateType> _switcher;
	private float _attackDuration = 1.5f;
	private float _timer;

	public BikerStateType Kind => BikerStateType.Attack;

	public BikerAttackState(BikerContext ctx, IStateSwitcher<BikerStateType> switcher)
	{
		_ctx = ctx;
		_switcher = switcher;
	}

	public void Enter()
	{
		_timer = _attackDuration;
		_ctx.Animator.SetTrigger("Attack");
	}

	public void Exit() { }

	public void Update()
	{
		_timer -= Time.deltaTime;
		if (_timer <= 0f)
		{
			_switcher.Switch(BikerStateType.Parallel);
			return;
		}

		float distanceDiff = _ctx.DistanceToPlayer + _ctx.EffectiveDistanceOffset;
		float targetLateral = _ctx.GetLaneCenter(_ctx.TargetLane);
        
		_ctx.ApplyParallelMovement(distanceDiff, targetLateral);
	}
}