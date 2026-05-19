using Common.Runtime.StateMachine;
using Enemies.Biker.Runtime;
using UnityEngine;

public class BikerParallelState : IUpdatableState<BikerStateType>
{
	private readonly BikerContext _ctx;
	private readonly IStateSwitcher<BikerStateType> _switcher;
	private float _attackTimer;

	public BikerStateType Kind => BikerStateType.Parallel;

	public BikerParallelState(BikerContext ctx, IStateSwitcher<BikerStateType> switcher)
	{
		_ctx = ctx;
		_switcher = switcher;
	}

	public void Enter()
	{
		_ctx.Animator.SetBool("IsParallel", true);
		_attackTimer = Random.Range(2f, 5f);
	}

	public void Exit() { _ctx.Animator.SetBool("IsParallel", false); }

	public void Update()
	{
		float distanceDiff = _ctx.DistanceToPlayer + _ctx.EffectiveDistanceOffset;

		if (Mathf.Abs(distanceDiff) > 5.0f)
		{
			_switcher.Switch(BikerStateType.Chase);
			return;
		}

		_attackTimer -= Time.deltaTime;
		if (_attackTimer <= 0f)
		{
			_switcher.Switch(BikerStateType.Attack);
			return;
		}

		float targetLateral = _ctx.GetLaneCenter(_ctx.TargetLane);

		_ctx.ApplyParallelMovement(distanceDiff, targetLateral);
	}
}