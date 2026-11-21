using UnityEngine;

namespace Game.Level.Runtime.Terrain
{
	/// <summary>Поворачивает объект вокруг вертикали лицом к камере. Для плоских спрайтов вокруг дороги.</summary>
	public class TerrainBillboard : MonoBehaviour
	{
		static Camera _camera;

		void LateUpdate()
		{
			if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
			if (_camera == null) return;

			Vector3 dir = transform.position - _camera.transform.position;
			dir.y = 0f;
			if (dir.sqrMagnitude < 0.0001f) return;
			transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
		}
	}
}
