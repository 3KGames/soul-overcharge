using Common.Runtime.StateMachine;
using Enemies.Biker.Runtime;
using UnityEngine;

public class BikerChaseState : IUpdatableState<BikerStateType>
{
	private readonly BikerContext _ctx;
	private readonly IStateSwitcher<BikerStateType> _switcher;

	public BikerStateType Kind => BikerStateType.Chase;

	public BikerChaseState(BikerContext ctx, IStateSwitcher<BikerStateType> switcher)
	{
		_ctx = ctx;
		_switcher = switcher;
	}

	public void Enter() { _ctx.Animator.SetBool("IsChasing", true); }
	public void Exit()  { _ctx.Animator.SetBool("IsChasing", false); }

	public void Update()
	{
		float distanceDiff = _ctx.DistanceToPlayer + _ctx.EffectiveDistanceOffset;

		if (Mathf.Abs(distanceDiff) <= 2.5f)
		{
			_switcher.Switch(BikerStateType.Parallel);
			return;
		}

		float targetSpeed = _ctx.PlayerSpeed;

		if (distanceDiff > 0)
		{
			targetSpeed += _ctx.CatchUpBonusSpeed;
		}
		else
		{
			targetSpeed -= _ctx.CatchUpBonusSpeed;
			targetSpeed = Mathf.Max(0f, targetSpeed);
		}

		float targetLateral = _ctx.GetLaneCenter(_ctx.TargetLane);
		_ctx.ApplyChaseMovement(targetSpeed, targetLateral);
	}
}