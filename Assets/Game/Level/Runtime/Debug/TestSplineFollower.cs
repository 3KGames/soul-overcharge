using UnityEngine;
using System.Linq;
using Game.Level.Runtime;
using Game.Common.Runtime.Movement;

/// <summary>
/// Временный тестер SplineFollower. Удалить после проверки дороги.
/// Положи на пустой GameObject в сцене, назначь startView в инспекторе.
/// </summary>
public class TestSplineFollower : MonoBehaviour
{
    [Header("Setup")]
    public RoadSegmentView startView;
    [Range(0, 7)] public int startLane = 0;
    public float speed = 15f;

    [Header("Debug Info (read-only)")]
    [SerializeField] string currentPathName;
    [SerializeField] int currentLane;
    [SerializeField] float distanceAlongPath;
    [SerializeField] bool isOnValidLane;
    [SerializeField] bool pathCompleted;

    SplineFollower _follower;

    void Start()
    {
        if (startView == null)
        {
            Debug.LogError("[TestSplineFollower] startView не назначен!");
            enabled = false;
            return;
        }

        if (startLane >= startView.laneCount)
        {
            Debug.LogWarning($"[TestSplineFollower] startLane {startLane} >= laneCount {startView.laneCount}, клампаю");
            startLane = Mathf.Clamp(startLane, 0, startView.laneCount - 1);
        }

        _follower = new SplineFollower(10f, 10f); // maxLateralAccel, lateralDamping
        _follower.SetPath(startView, startLane, 0f);
        _follower.SetSpeed(speed);
        UpdateTransform();
        LogState();
    }

    void Update()
    {
        if (_follower.CurrentPath == null) return;

        var result = _follower.ComputeFrame(Time.deltaTime);
        UpdateTransform(result);
        LogState(result);

        // Авто-рестарт при уходе за пределы (для удобства тестов)
        if (result.PathCompleted)
        {
            var nextPaths = _follower.CurrentPath.GetNextPaths(_follower.CurrentLane).ToList();
            if (nextPaths.Count == 0)
            {
                Debug.Log("[TestSplineFollower] Доехал до конца дороги (нет следующих путей), рестарт");
                _follower.SetPath(startView, startLane, 0f);
                _follower.SetSpeed(speed);
                UpdateTransform();
            }
        }
    }

    void UpdateTransform(SplineFollower.FrameResult result)
    {
        transform.position = result.Position;
        transform.rotation = result.Rotation;
    }

    void UpdateTransform()
    {
        if (_follower.CurrentPath == null) return;
        var sample = _follower.CurrentPath.SampleAtDistance(_follower.DistanceAlongPath);
        transform.position = sample.position + sample.right * _follower.CurrentPath.GetLaneOffset(_follower.CurrentLane);
        transform.rotation = Quaternion.LookRotation(sample.forward, sample.up);
    }

    void LogState(SplineFollower.FrameResult result)
    {
        currentPathName = _follower.CurrentPath?.GetType().Name ?? "null";
        currentLane = _follower.CurrentLane;
        distanceAlongPath = _follower.DistanceAlongPath;
        isOnValidLane = _follower.CurrentPath != null && _follower.CurrentPath.IsLaneActiveAt(_follower.CurrentLane, _follower.DistanceAlongPath);
        pathCompleted = result.PathCompleted;

        // Лог каждые ~2 сек, чтобы не заспамить консоль
        if (Time.frameCount % 120 == 0)
        {
            Debug.Log($"[TestSplineFollower] Path: {currentPathName} | Lane: {currentLane} | Dist: {distanceAlongPath:F1}/{(_follower.CurrentPath?.Length ?? 0):F1} | ValidLane: {isOnValidLane} | Completed: {pathCompleted}");
        }
    }

    void LogState()
    {
        currentPathName = _follower.CurrentPath?.GetType().Name ?? "null";
        currentLane = _follower.CurrentLane;
        distanceAlongPath = _follower.DistanceAlongPath;
        isOnValidLane = _follower.CurrentPath != null && _follower.CurrentPath.IsLaneActiveAt(_follower.CurrentLane, _follower.DistanceAlongPath);

        Debug.Log($"[TestSplineFollower] INIT: Path: {currentPathName} | Lane: {currentLane} | Dist: {distanceAlongPath:F1} | ValidLane: {isOnValidLane}");
    }

    void OnDrawGizmosSelected()
    {
        if (_follower.CurrentPath == null) return;

        // Текущая позиция на сплайне
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // Направление движения
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 3f);

        // Показываем активные полосы на текущем path
        if (_follower.CurrentPath is RoadSegmentView view && view.leftEdgeProfile != null && view.rightEdgeProfile != null)
        {	
            float tileLen = view.Length / view.leftEdgeProfile.Length;
            for (int lane = 0; lane < view.laneCount; lane++)
            {
                bool active = view.IsLaneActiveAt(lane, _follower.DistanceAlongPath);
                Gizmos.color = active ? Color.green : Color.red;
                var sample = view.SampleAtDistance(_follower.DistanceAlongPath);
                Vector3 lanePos = sample.position + sample.right * view.GetLaneOffset(lane);
                Gizmos.DrawWireCube(lanePos, new Vector3(0.3f, 0.1f, 0.3f));
            }
        }
    }

    [ContextMenu("Restart from startView/startLane")]
    public void Restart()
    {
        _follower.SetPath(startView, startLane, 0f);
        _follower.SetSpeed(speed);
        UpdateTransform();
        LogState();
    }
}