using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Level.Runtime
{
	[Serializable]
	public struct SegmentExit
	{
		public ConnectionPoint point;

		[Tooltip("Клетка сразу за выходом (в сетке сегмента: вход — клетка 0,0, смотрит в +Y)")]
		public Vector2Int nextCell;

		[Tooltip("0 = прямо, 1 = вправо, 3 = влево")]
		public int turn;
	}

	public class RoadSegment : MonoBehaviour
	{
		public RoadSegmentView roadView;

		[Header("Точки стыковки")]
		public ConnectionPoint entryPoint;

		[Tooltip("1 выход — обычный сегмент, 2+ — развилка. Максимум один выход на сторону, назад нельзя.")]
		public ConnectionPoint[] exitPoints;

		[SerializeField, HideInInspector, FormerlySerializedAs("exitPoint")]
		private ConnectionPoint legacyExitPoint;

		[Header("Тип сегмента (для логики чередования)")]
		public SegmentType segmentType = SegmentType.Straight;

		[Header("Сетка — заполняется через контекстное меню Bake grid")]
		[Tooltip("Одинаковый у всех префабов и у RoadGenerator")]
		public float cellSize = 40f;

		[Tooltip("Если включено, Bake не перезаписывает cells (для сегментов нестандартной формы)")]
		public bool manualCells;

		public Vector2Int[] cells;
		public SegmentExit[] exits;

		public ConnectionPoint exitPoint =>
			exitPoints != null && exitPoints.Length > 0 ? exitPoints[0] : legacyExitPoint;

		public enum SegmentType
		{
			Straight,
			TurnLeft,
			TurnRight,
			HillUp,
			HillDown,
			Fork,
		}

		void OnValidate()
		{
			if ((exitPoints == null || exitPoints.Length == 0) && legacyExitPoint != null)
				exitPoints = new[] { legacyExitPoint };
		}

		public bool BakeGrid()
		{
			if ((exitPoints == null || exitPoints.Length == 0) && legacyExitPoint != null)
				exitPoints = new[] { legacyExitPoint };

			if (entryPoint == null || exitPoints == null || exitPoints.Length == 0)
			{
				Debug.LogError($"[RoadSegment] {name}: не назначены entryPoint или exitPoints", this);
				return false;
			}

			if (cellSize <= 0f)
			{
				Debug.LogError($"[RoadSegment] {name}: cellSize должен быть больше нуля", this);
				return false;
			}

			GetGridFrame(out Vector3 origin, out Vector3 fwd, out Vector3 right);
			bool ok = true;
			var baked = new List<SegmentExit>();
			var usedTurns = new HashSet<int>();
			Vector2Int min = Vector2Int.zero, max = Vector2Int.zero;

			foreach (var p in exitPoints)
			{
				if (p == null)
				{
					Debug.LogError($"[RoadSegment] {name}: пустой элемент в exitPoints", this);
					ok = false;
					continue;
				}

				Vector3 dir = Flat(p.transform.forward);
				float angle = Vector3.SignedAngle(fwd, dir, Vector3.up);
				int turn = (Mathf.RoundToInt(angle / 90f) % 4 + 4) % 4;
				Vector3 half = dir * (cellSize * 0.5f);
				Vector2Int inner = ToCell(p.transform.position - half, origin, fwd, right);
				Vector2Int next = ToCell(p.transform.position + half, origin, fwd, right);

				if (turn == 2)
				{
					Debug.LogError($"[RoadSegment] {name}: выход {p.name} смотрит назад относительно входа — так нельзя", this);
					ok = false;
				}

				if (!usedTurns.Add(turn))
				{
					Debug.LogError($"[RoadSegment] {name}: два выхода в одну сторону. Объедините их в один выход с нужным numLanes", this);
					ok = false;
				}

				if (Mathf.Abs(Mathf.DeltaAngle(angle, turn * 90f)) > 5f)
					Debug.LogWarning($"[RoadSegment] {name}: выход {p.name} повёрнут не кратно 90°", this);

				Vector3 diff = p.transform.position - (CellCenter(inner, origin, fwd, right) + half);
				diff.y = 0f;
				if (diff.magnitude > cellSize * 0.05f)
					Debug.LogWarning($"[RoadSegment] {name}: выход {p.name} не на середине края клетки (сдвиг {diff.magnitude:0.00} м)", this);

				baked.Add(new SegmentExit { point = p, nextCell = next, turn = turn });
				min = Vector2Int.Min(min, inner);
				max = Vector2Int.Max(max, inner);
			}

			exits = baked.ToArray();

			if (!manualCells || cells == null || cells.Length == 0)
			{
				var list = new List<Vector2Int>();
				for (int x = min.x; x <= max.x; x++)
					for (int y = min.y; y <= max.y; y++)
						list.Add(new Vector2Int(x, y));
				cells = list.ToArray();
			}

			if (Array.IndexOf(cells, Vector2Int.zero) < 0)
			{
				Debug.LogError($"[RoadSegment] {name}: в cells нет клетки входа (0,0)", this);
				ok = false;
			}

			foreach (var e in exits)
			{
				if (Array.IndexOf(cells, e.nextCell) >= 0)
				{
					Debug.LogError($"[RoadSegment] {name}: выход {e.point.name} ведёт внутрь самого сегмента", this);
					ok = false;
				}
			}

			if (exits.Length > 1)
				ValidateForkLanes();

			return ok;
		}

		void ValidateForkLanes()
		{
			int entryLanes = entryPoint.NumLanes;
			var used = new bool[Mathf.Max(entryLanes, 1)];

			foreach (var e in exits)
			{
				int first = e.point.FirstEntryLane;
				int last = first + e.point.NumLanes - 1;
				if (last >= entryLanes)
				{
					Debug.LogWarning($"[RoadSegment] {name}: выход {e.point.name} берёт полосы входа {first}..{last}, " +
					                 $"а у входа их только {entryLanes} (0..{entryLanes - 1}). Проверьте firstEntryLane", this);
					continue;
				}

				for (int l = first; l <= last; l++)
				{
					if (used[l])
						Debug.LogWarning($"[RoadSegment] {name}: полоса входа {l} ведёт сразу в несколько выходов. " +
						                 "Машина на этой полосе поедет в первый из них", this);
					used[l] = true;
				}
			}
		}

		[ContextMenu("Bake grid")]
		private void BakeGridFromMenu()
		{
			bool ok = BakeGrid();
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(this);
#endif
			Debug.Log($"[RoadSegment] {name}: {(ok ? "готово" : "есть ошибки, см. выше")}, клеток {cells?.Length ?? 0}, выходов {exits?.Length ?? 0}", this);
		}

		void GetGridFrame(out Vector3 origin, out Vector3 fwd, out Vector3 right)
		{
			fwd = Flat(entryPoint.transform.forward);
			right = Vector3.Cross(Vector3.up, fwd);
			origin = entryPoint.transform.position + fwd * (cellSize * 0.5f);
		}

		Vector2Int ToCell(Vector3 p, Vector3 origin, Vector3 fwd, Vector3 right)
		{
			Vector3 d = p - origin;
			return new Vector2Int(
				Mathf.RoundToInt(Vector3.Dot(d, right) / cellSize),
				Mathf.RoundToInt(Vector3.Dot(d, fwd) / cellSize));
		}

		Vector3 CellCenter(Vector2Int c, Vector3 origin, Vector3 fwd, Vector3 right) =>
			origin + right * (c.x * cellSize) + fwd * (c.y * cellSize);

		static Vector3 Flat(Vector3 v)
		{
			v.y = 0f;
			return v.normalized;
		}

		void OnDrawGizmos()
		{
			if (entryPoint != null)
			{
				Gizmos.color = Color.green;
				Gizmos.DrawSphere(entryPoint.transform.position, 0.3f);
				Gizmos.DrawRay(entryPoint.transform.position, entryPoint.transform.forward * 2f);
			}

			Gizmos.color = Color.red;
			if (exitPoints != null)
			{
				foreach (var p in exitPoints)
				{
					if (p == null) continue;
					Gizmos.DrawSphere(p.transform.position, 0.3f);
					Gizmos.DrawRay(p.transform.position, p.transform.forward * 2f);
				}
			}

			if (entryPoint == null || cells == null || cellSize <= 0f) return;

			GetGridFrame(out Vector3 origin, out Vector3 fwd, out _);
			if (fwd.sqrMagnitude < 0.001f) return;

			Matrix4x4 oldMatrix = Gizmos.matrix;
			Gizmos.matrix = Matrix4x4.TRS(origin, Quaternion.LookRotation(fwd, Vector3.up), Vector3.one);
			var size = new Vector3(cellSize * 0.96f, 0.05f, cellSize * 0.96f);

			foreach (var c in cells)
			{
				var center = new Vector3(c.x * cellSize, 0f, c.y * cellSize);
				Gizmos.color = new Color(0f, 0.7f, 1f, 0.25f);
				Gizmos.DrawCube(center, size);
				Gizmos.color = new Color(0f, 0.7f, 1f, 0.8f);
				Gizmos.DrawWireCube(center, size);
			}

			if (exits != null)
			{
				Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
				foreach (var e in exits)
					Gizmos.DrawWireCube(new Vector3(e.nextCell.x * cellSize, 0f, e.nextCell.y * cellSize), size);
			}

			Gizmos.matrix = oldMatrix;
		}
	}
}
