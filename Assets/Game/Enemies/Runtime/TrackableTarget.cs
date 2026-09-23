using UnityEngine;
using VContainer;

public class TrackableTarget : MonoBehaviour
{
	private TargetRegistry _registry;
	private bool _isRegistered;

	[Inject]
	public void Construct(TargetRegistry registry)
	{
		_registry = registry;

		if (gameObject.activeInHierarchy && !_isRegistered)
		{
			Register();
		}
	}

	private void OnEnable()
	{
		if (_registry != null && !_isRegistered)
		{
			Register();
		}
	}

	private void OnDisable()
	{
		if (_registry != null && _isRegistered)
		{
			_registry.Unregister(transform);
			_isRegistered = false;
		}
	}

	private void Register()
	{
		_registry.Register(transform);
		_isRegistered = true;
	}
}