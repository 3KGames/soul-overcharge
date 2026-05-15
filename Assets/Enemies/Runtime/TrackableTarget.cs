using UnityEngine;
using VContainer;

public class TrackableTarget : MonoBehaviour
{
	private TargetRegistry _registry;

	[Inject]
	public void Construct(TargetRegistry registry)
	{
		_registry = registry;
	}

	private void OnEnable()
	{
		if (_registry != null)
			_registry.Register(transform);
	}

	private void OnDisable()
	{
		if (_registry != null)
			_registry.Unregister(transform);
	}
}