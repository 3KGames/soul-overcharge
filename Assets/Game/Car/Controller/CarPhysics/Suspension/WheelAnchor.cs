using NaughtyAttributes;
using UnityEngine;

namespace Game.Car.Controller.CarPhysics.Suspension
{
	// TODO: Clean this file
	public class WheelAnchor : MonoBehaviour
	{
		[Header("Wheel")]
		[SerializeField] private Transform wheelMesh;
		[SerializeField] private Transform steerPivot;
		[SerializeField] private Transform hub;
		[SerializeField] private Transform spinPivot;

		[Header("Setup")]
		public SuspensionSO settings;
		public bool steerable;
		public bool driven;
		[Range(0f, 1f)]
		public float loadShare = 1f;

		[Header("Axes")]
		[Dropdown("GetSpinAxisOptions")]
		public Vector3 spinAxisLocal = Vector3.right;

		[Dropdown("GetSteerAxisOptions")]
		public Vector3 steerAxisLocal = Vector3.up;

		public float D { get; set; }
		public float Comp { get; set; }
		public float SpinDeg { get; set; }

		public SuspensionSO Settings => settings;
		public bool Driven => driven;
		public Transform Hub => hub;
		public Vector3 MountWorld => transform.position;
		
		public bool IsGrounded  { get; set; }
		public Vector3 GroundPoint { get; set; }
		public Vector3 GroundNormal { get; set; } = Vector3.up;
		public int GroundLayer { get; set; } = -1;

		private void Awake()
		{
			if (spinPivot != null && wheelMesh != null)
			{
				wheelMesh.SetParent(spinPivot, false);
				wheelMesh.localPosition = Vector3.zero;
			}
			else if (wheelMesh == null)
			{
				Debug.LogWarning("\u005BWheelRig\u005D wheelMesh не назначен — колесо не будет двигаться визуально", this);
			}
		}

		public void ApplyPose(Vector3 axis, float steerDeg)
		{
			Transform pivot = steerPivot;
			if (pivot == null)
				return;

			pivot.position = MountWorld - axis * D;

			if (steerable && steerPivot != null)
				steerPivot.localRotation = Quaternion.AngleAxis(steerDeg, steerAxisLocal);

			if (spinPivot != null)
				spinPivot.localRotation = Quaternion.AngleAxis(SpinDeg, spinAxisLocal);
		}

		private DropdownList<Vector3> GetSpinAxisOptions()
		{
			return new DropdownList<Vector3>()
			{
				{ "Right (+X)",   Vector3.right },
				{ "Left (-X)",    Vector3.left },
				{ "Up (+Y)",      Vector3.up },
				{ "Down (-Y)",    Vector3.down },
				{ "Forward (+Z)", Vector3.forward },
				{ "Back (-Z)",    Vector3.back }
			};
		}

		private DropdownList<Vector3> GetSteerAxisOptions()
		{
			return new DropdownList<Vector3>()
			{
				{ "Up (+Y)",      Vector3.up },
				{ "Down (-Y)",    Vector3.down },
				{ "Right (+X)",   Vector3.right },
				{ "Left (-X)",    Vector3.left },
				{ "Forward (+Z)", Vector3.forward },
				{ "Back (-Z)",    Vector3.back }
			};
		}

		private void OnDrawGizmosSelected()
		{
			if (settings == null)
				return;

			Gizmos.color = Color.yellow;
			Gizmos.DrawRay(MountWorld, -transform.up * (settings.MaxLength + settings.WheelRadius));

			Transform spinT = hub != null ? hub : transform;
			Gizmos.color = Color.red;
			Vector3 spinAxis = spinT.rotation * spinAxisLocal;
			Gizmos.DrawRay(spinT.position - spinAxis * settings.WheelRadius,
				spinAxis * settings.WheelRadius * 2f);

			Transform steerT = steerable && steerPivot != null ? steerPivot : transform;
			Gizmos.color = Color.blue;
			Vector3 steerAxis = steerT.rotation * steerAxisLocal;
			Gizmos.DrawRay(steerT.position, steerAxis * 0.5f);
		}
	}
}