using System;
using System.Collections.Generic;
using UnityEngine;

public class TargetRegistry
{
	public List<Transform> ActiveTargets { get; } = new List<Transform>();

	public event Action<Transform> OnTargetAdded;
	public event Action<Transform> OnTargetRemoved;

	public void Register(Transform target)
	{
		if (!ActiveTargets.Contains(target))
		{
			ActiveTargets.Add(target);
			OnTargetAdded?.Invoke(target);
		}
	}

	public void Unregister(Transform target)
	{
		if (ActiveTargets.Remove(target))
		{
			OnTargetRemoved?.Invoke(target);
		}
	}
}