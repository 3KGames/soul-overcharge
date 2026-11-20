using System;
using System.Collections.Generic;
using System.Linq;
using Dreamteck.Splines;
using Game.Level.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class RoadGeneratorWindow : EditorWindow
{
    public enum TransitionType { Narrowing, Widening }

    [Serializable]
    public class TransitionData
    {
        public TransitionType type = TransitionType.Narrowing;
        public int tileIndex = 5;
    }

    private readonly struct RoadLayout
    {
        public RoadLayout(float totalLength, float physicalLength, int totalTiles, float startOffset)
        {
            TotalLength = totalLength;
            PhysicalLength = physicalLength;
            TotalTiles = totalTiles;
            StartOffset = startOffset;
        }

        public float TotalLength { get; }
        public float PhysicalLength { get; }
        public int TotalTiles { get; }
        public float StartOffset { get; }
    }

    private Transform entryPoint;
    private Transform exitPoint;

    private SplineComputer targetSpline;
    private RoadSettingsSO settings;

    private RoadSegmentView previousRoad;
    private RoadSegmentView nextRoad;
    private RoadSegmentView roadView;

    private int laneCount = 3;
    private bool zeroYCoordinates = true;

    private int minDecals = 3;
    private int maxDecals = 7;
    private float minSpacing = 5f;

    [SerializeField] private List<TransitionData> leftTransitions = new List<TransitionData>();
    [SerializeField] private List<TransitionData> rightTransitions = new List<TransitionData>();

    // Fork Prefab Creation
    private Transform forkEntryPoint;
    private Transform[] forkExitPoints = new Transform[2];
    private int[] forkExitLanes = new int[] { 2, 1 };
    private bool ShowForkCreator = false;

    private Dictionary<int, List<Vector2Int>> roadTopologyMap = new Dictionary<int, List<Vector2Int>>();
    private int[] leftEdgeProfile;
    private int[] rightEdgeProfile;
    private bool[,] holesMap;

    private Vector2 scrollPos;

    [MenuItem("Tools/Road Generator")]
    public static void ShowWindow()
    {
        GetWindow<RoadGeneratorWindow>("Генератор Дорог");
    }

    // Add to the menu items section - add this new menu item
    [MenuItem("Tools/Road/Create Fork Prefab")]
    public static void CreateForkPrefab()
    {
        var window = GetWindow<RoadGeneratorWindow>("Fork Prefab Creator");
        window.ShowForkCreator = true;
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawSplineCreationSection();
        DrawDivider();
        DrawBasicSettingsSection();
        DrawConnectionsSection();
        DrawTransitionsSection();
        DrawDecalSettingsSection();
        DrawGenerateButton();

        if (ShowForkCreator)
        {
            DrawForkCreatorSection();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSplineCreationSection()
    {
        GUILayout.Label("Генерация прямого сплайна", EditorStyles.boldLabel);
        entryPoint = (Transform)EditorGUILayout.ObjectField("Точка входа", entryPoint, typeof(Transform), true);
        exitPoint = (Transform)EditorGUILayout.ObjectField("Точка выхода", exitPoint, typeof(Transform), true);

        if (GUILayout.Button("Создать сплайн из точек", GUILayout.Height(30)))
        {
            CreateStraightSpline();
        }
    }

    private void DrawDivider()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();
    }

    private void DrawBasicSettingsSection()
    {
        GUILayout.Label("Базовые настройки", EditorStyles.boldLabel);

        targetSpline = (SplineComputer)EditorGUILayout.ObjectField("Целевой Сплайн", targetSpline, typeof(SplineComputer), true);
        settings = (RoadSettingsSO)EditorGUILayout.ObjectField("Настройки (SO)", settings, typeof(RoadSettingsSO), false);
        laneCount = EditorGUILayout.IntSlider("Количество полос", laneCount, 1, 11);
        zeroYCoordinates = EditorGUILayout.Toggle("Занулить Y сплайна", zeroYCoordinates);
    }

    private void DrawConnectionsSection()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Соединения графа дорог", EditorStyles.boldLabel);
        previousRoad = (RoadSegmentView)EditorGUILayout.ObjectField("Предыдущая дорога", previousRoad, typeof(RoadSegmentView), true);
        nextRoad = (RoadSegmentView)EditorGUILayout.ObjectField("Следующая дорога", nextRoad, typeof(RoadSegmentView), true);
    }

    private void DrawTransitionsSection()
    {
        EditorGUILayout.Space();

        DrawTransitionsList("Переходы: Левая сторона", leftTransitions);
        DrawTransitionsList("Переходы: Правая сторона", rightTransitions);
    }

    private void DrawDecalSettingsSection()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Настройки ям (Декалей)", EditorStyles.boldLabel);
        minDecals = EditorGUILayout.IntField("Мин. количество ям", minDecals);
        maxDecals = EditorGUILayout.IntField("Макс. количество ям", maxDecals);
        minSpacing = EditorGUILayout.FloatField("Мин. расстояние (метры)", minSpacing);
    }

    private void DrawGenerateButton()
    {
        EditorGUILayout.Space();

        if (GUILayout.Button("Сгенерировать дорогу", GUILayout.Height(40)))
        {
            GenerateEverything();
        }
    }

    private void DrawTransitionsList(string label, List<TransitionData> list)
    {
        GUILayout.Label(label, EditorStyles.boldLabel);

        for (int i = 0; i < list.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            list[i].type = (TransitionType)EditorGUILayout.EnumPopup(list[i].type, GUILayout.Width(100));
            list[i].tileIndex = EditorGUILayout.IntField("Индекс тайла", list[i].tileIndex);

            if (GUILayout.Button("X", GUILayout.Width(30)))
            {
                list.RemoveAt(i);
                i--;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Добавить переход", GUILayout.Width(150)))
        {
            list.Add(new TransitionData());
        }
        EditorGUILayout.Space();
    }

    private void CreateStraightSpline()
    {
        if (entryPoint == null || exitPoint == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Пожалуйста, назначьте Точку входа и Точку выхода!", "OK");
            return;
        }

        GameObject splineObj = new GameObject("Generated_Road_Spline");
        splineObj.transform.SetParent(entryPoint.parent, false);

        SplineComputer spline = Undo.AddComponent<SplineComputer>(splineObj);
        spline.type = Spline.Type.Bezier;
        spline.SetPoints(CreateStraightSplinePoints());

        targetSpline = spline;

        Undo.RegisterCreatedObjectUndo(splineObj, "Create Straight Spline");
        Selection.activeGameObject = splineObj;

        Debug.Log("[RoadGenerator] Прямой сплайн успешно создан и установлен как целевой.");
    }

    private SplinePoint[] CreateStraightSplinePoints()
    {
        Vector3 direction = exitPoint.position - entryPoint.position;

        SplinePoint start = new SplinePoint();
        start.position = entryPoint.position;
        start.normal = entryPoint.up;
        start.size = 1f;
        start.color = Color.white;
        start.SetTangent2Position(entryPoint.position + direction * 0.33f);

        SplinePoint end = new SplinePoint();
        end.position = exitPoint.position;
        end.normal = exitPoint.up;
        end.size = 1f;
        end.color = Color.white;
        end.SetTangentPosition(exitPoint.position - direction * 0.33f);

        return new[] { start, end };
    }

    private void GenerateEverything()
    {
        if (!ValidateGenerationInputs()) return;

        AlignSplineHeight();
        ZeroSplineYIfNeeded();

        SetupRoadView();
        AssignMaterials();
        SplineMesh splineMesh = PrepareSplineMesh();

        RoadLayout layout = CalculateLayout();
        GenerateTopologyMap(layout.TotalTiles);
        BuildMeshChannels(splineMesh, layout);

        splineMesh.Rebuild();
        SetupMeshCollider();
        CleanupGeneratedChildren();

        SpawnTransitions(layout);
        SpawnDecals(layout);

        ApplyRoadViewData(layout);
        SetLayerRecursively(targetSpline.gameObject, settings.roadLayer);

        EditorUtility.SetDirty(targetSpline.gameObject);
        EditorUtility.SetDirty(roadView);
        EditorUtility.SetDirty(splineMesh);

        Debug.Log($"[RoadGenerator] Успешно создана дорога с динамической матричной топологией (режим Префаба поддерживается).");
    }

    private bool ValidateGenerationInputs()
    {
        if (targetSpline == null || settings == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Пожалуйста, выберите Spline Computer и файл настроек RoadSettingsSO!", "OK");
            return false;
        }

        return true;
    }

    private void AlignSplineHeight()
    {
        Undo.RecordObject(targetSpline.transform, "Set Spline Transform Y");
        Vector3 splinePos = targetSpline.transform.position;
        splinePos.y = settings.roadYCoordinate;
        targetSpline.transform.position = splinePos;
    }

    private void ZeroSplineYIfNeeded()
    {
        if (!zeroYCoordinates) return;

        Undo.RecordObject(targetSpline, "Zero Spline Y Coordinates");
        SplinePoint[] pts = targetSpline.GetPoints();

        for (int i = 0; i < pts.Length; i++)
        {
            pts[i].position = new Vector3(pts[i].position.x, 0f, pts[i].position.z);
            pts[i].tangent = new Vector3(pts[i].tangent.x, 0f, pts[i].tangent.z);
            pts[i].tangent2 = new Vector3(pts[i].tangent2.x, 0f, pts[i].tangent2.z);
        }

        targetSpline.SetPoints(pts);
    }

    private void SetupRoadView()
    {
        if (!targetSpline.TryGetComponent<RoadSegmentView>(out roadView))
        {
            roadView = Undo.AddComponent<RoadSegmentView>(targetSpline.gameObject);
        }
    }

    private void AssignMaterials()
    {
        if (targetSpline.TryGetComponent<MeshRenderer>(out var meshRenderer))
        {
            meshRenderer.materials = new[]
            {
                settings.cleanEmptyRoadMaterial,
                settings.cleanCenterLaneMaterial,
                settings.cleanSideLaneMaterial
            };
        }
        else
        {
            Debug.LogError("Spline Computer должен иметь MeshRenderer (создается вместе со SplineMesh)!");
        }
    }

    private SplineMesh PrepareSplineMesh()
    {
        if (!targetSpline.TryGetComponent<SplineMesh>(out var splineMesh))
        {
            splineMesh = Undo.AddComponent<SplineMesh>(targetSpline.gameObject);
        }

        while (splineMesh.GetChannelCount() > 0)
        {
            splineMesh.RemoveChannel(0);
        }

        return splineMesh;
    }

    // VisualLaneCount = slider (what gizmos/enemies see).
    // EffectiveLaneCount = VisualLaneCount + 1 (mesh channels: 0..EffectiveLaneCount-1).
    // Mesh channels 0 and EffectiveLaneCount-1 are rendered at half-width via material (±0.25 offset).
    // This gives VisualLaneCount visual lanes with correct total width.
    private int VisualLaneCount => Mathf.Max(1, laneCount);
    private int EffectiveLaneCount => VisualLaneCount + 1;

    private RoadLayout CalculateLayout()
    {
        float totalSplineLength = targetSpline.CalculateLength();
        float physicalLength = settings.laneWidth * settings.textureRatio;
        int totalTiles = Mathf.Max(1, Mathf.FloorToInt(totalSplineLength / physicalLength));

        int effLanes = EffectiveLaneCount;
        float totalWidth = effLanes * settings.laneWidth;
        float startOffset = (-totalWidth / 2f) + (settings.laneWidth / 2f);

        return new RoadLayout(totalSplineLength, physicalLength, totalTiles, startOffset);
    }

    private void BuildMeshChannels(SplineMesh splineMesh, RoadLayout layout)
    {
        int effLanes = EffectiveLaneCount;

        for (int meshLane = 0; meshLane < effLanes; meshLane++)
        {
            if (!roadTopologyMap.TryGetValue(meshLane, out var segments)) continue;

            foreach (Vector2Int segment in segments)
            {
                CreateLaneChannel(splineMesh, layout, meshLane, segment);
            }
        }
    }

    private void CreateLaneChannel(SplineMesh splineMesh, RoadLayout layout, int meshLane, Vector2Int segment)
    {
        SplineMesh.Channel channel = splineMesh.AddChannel($"Lane_{meshLane}_{segment.x}_{segment.y}");
        channel.AddMesh(settings.laneMesh);

        float currentOffset = layout.StartOffset + (meshLane * settings.laneWidth);
        SetChannelLateralOffset(channel, currentOffset);

        channel.autoCount = true;
        var mesh = channel.GetMesh(0);
        mesh.scale = new Vector3(settings.laneWidth, settings.roadThickness, settings.laneWidth);

        ConfigureChannelUVs(channel, layout);
        ConfigureChannelClip(channel, layout, segment);

        channel.overrideMaterialID = true;
        ApplyLaneMaterial(channel, mesh, layout, meshLane, segment, currentOffset);
    }

    private void SetChannelLateralOffset(SplineMesh.Channel channel, float x)
    {
        channel.minOffset = new Vector3(x, 0, 0);
        channel.maxOffset = new Vector3(x, 0, 0);
    }

    private void ConfigureChannelUVs(SplineMesh.Channel channel, RoadLayout layout)
    {
        channel.overrideUVs = SplineMesh.Channel.UVOverride.UniformV;
        channel.uvScale = new Vector2(1f, 1f / layout.PhysicalLength);
    }

    private void ConfigureChannelClip(SplineMesh.Channel channel, RoadLayout layout, Vector2Int segment)
    {
        double clipFromPercent = (segment.x == 0) ? 0.0 : targetSpline.Travel(0.0, segment.x * layout.PhysicalLength, Spline.Direction.Forward);
        double clipToPercent = (segment.y == layout.TotalTiles) ? 1.0 : targetSpline.Travel(0.0, segment.y * layout.PhysicalLength, Spline.Direction.Forward);

        channel.clipFrom = clipFromPercent;
        channel.clipTo = clipToPercent;
    }

    private void ApplyLaneMaterial(SplineMesh.Channel channel, SplineMesh.Channel.MeshDefinition mesh, RoadLayout layout, int meshLane, Vector2Int segment, float currentOffset)
    {
        int effLanes = EffectiveLaneCount;
        int midTile = Mathf.Clamp((segment.x + segment.y) / 2, 0, layout.TotalTiles - 1);

        bool isLeftEdge = meshLane == leftEdgeProfile[midTile];
        bool isRightEdge = meshLane == rightEdgeProfile[midTile];

        // First and last mesh channels are always half-width edges (via material)
        bool isFirstMeshLane = (meshLane == 0);
        bool isLastMeshLane = (meshLane == effLanes - 1);

        if (isLeftEdge && isRightEdge)
        {
            // Single lane road — center material
            channel.targetMaterialID = 1;
            return;
        }

        if (isLeftEdge || (isFirstMeshLane && leftEdgeProfile[midTile] == 0))
        {
            // Left edge of road — half width, shifted right by quarter
            channel.targetMaterialID = 2;
            mesh.scale = new Vector3(settings.laneWidth * 0.5f, settings.roadThickness, settings.laneWidth);
            mesh.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.X;
            SetChannelLateralOffset(channel, currentOffset + 0.25f * settings.laneWidth);
            return;
        }

        if (isRightEdge || (isLastMeshLane && rightEdgeProfile[midTile] == effLanes - 1))
        {
            // Right edge of road — half width, shifted left by quarter
            channel.targetMaterialID = 2;
            mesh.scale = new Vector3(settings.laneWidth * 0.5f, settings.roadThickness, settings.laneWidth);
            mesh.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.None;
            SetChannelLateralOffset(channel, currentOffset - 0.25f * settings.laneWidth);
            return;
        }

        // Center lanes — full width
        channel.targetMaterialID = ResolveCenterLaneMaterial(meshLane);
    }

    private int ResolveCenterLaneMaterial(int meshLane)
    {
        int effLanes = EffectiveLaneCount;
        int centerLaneId = (effLanes - 1) / 2;
        int idFromCenter = centerLaneId - meshLane;

        return (idFromCenter != 0 && meshLane % 2 == 0) ? 0 : 1;
    }

    private void SetupMeshCollider()
    {
        if (!targetSpline.TryGetComponent<MeshCollider>(out var meshCollider))
        {
            meshCollider = Undo.AddComponent<MeshCollider>(targetSpline.gameObject);
        }

        if (targetSpline.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
        {
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }
    }

    private void CleanupGeneratedChildren()
    {
        var childrenToRemove = new List<GameObject>();

        foreach (Transform child in targetSpline.transform)
        {
            if (child.name.StartsWith("Generated_Decal_") || child.name.StartsWith("Generated_Transition_"))
            {
                childrenToRemove.Add(child.gameObject);
            }
        }

        foreach (GameObject obj in childrenToRemove)
        {
            Undo.DestroyObjectImmediate(obj);
        }
    }

    private void SpawnTransitions(RoadLayout layout)
    {
        if (VisualLaneCount < 2) return;

        SpawnSideTransitions(SortTransitions(leftTransitions), false, layout);
        SpawnSideTransitions(SortTransitions(rightTransitions), true, layout);
    }

    private List<TransitionData> SortTransitions(List<TransitionData> transitions)
    {
        var sorted = new List<TransitionData>(transitions);
        sorted.Sort((a, b) => a.tileIndex.CompareTo(b.tileIndex));
        return sorted;
    }

    private int TransitionDelta(TransitionType type, bool isRightSide)
    {
        bool isNarrowing = type == TransitionType.Narrowing;
        return isRightSide ? (isNarrowing ? -1 : 1) : (isNarrowing ? 1 : -1);
    }

    private int TransitionPrefabLane(TransitionType type, int currentLane, bool isRightSide)
    {
        if (type == TransitionType.Narrowing)
        {
            return isRightSide ? currentLane - 1 : currentLane + 1;
        }

        return currentLane;
    }

    private int CalculateInitialEdgeLane(List<TransitionData> sorted, bool isRightSide, int effLanes)
    {
        int current = 0;
        int extreme = 0;

        foreach (TransitionData t in sorted)
        {
            current += TransitionDelta(t.type, isRightSide);

            if (isRightSide)
            {
                extreme = Mathf.Max(extreme, current);
            }
            else
            {
                extreme = Mathf.Min(extreme, current);
            }
        }

        // Left edge starts at 0 (left half-edge), right edge at effLanes-1 (right half-edge).
        // For no transitions (extreme=0): left=0, right=effLanes-1.
        // All effLanes channels active: 0 (half), 1..effLanes-2 (center), effLanes-1 (half).
        int initial = isRightSide ? (effLanes - 1) - extreme : -extreme;
        return Mathf.Clamp(initial, 0, effLanes - 1);
    }

    private void SpawnSideTransitions(List<TransitionData> sorted, bool isRightSide, RoadLayout layout)
    {
        if (sorted.Count == 0) return;

        int spawnLane = CalculateInitialEdgeLane(sorted, isRightSide, EffectiveLaneCount);

        for (int i = 0; i < sorted.Count; i++)
        {
            TransitionData t = sorted[i];
            int prefabLane = TransitionPrefabLane(t.type, spawnLane, isRightSide);

            SpawnTransitionPrefab(
                t.tileIndex,
                prefabLane,
                t.type,
                isRightSide,
                settings.leftTransitionPrefab,
                $"Generated_Transition_{(isRightSide ? "Right" : "Left")}_{i}",
                layout);

            spawnLane += TransitionDelta(t.type, isRightSide);
        }
    }

    private void SpawnTransitionPrefab(int tileIndex, int prefabLane, TransitionType type, bool isRightSide, GameObject prefab, string objName, RoadLayout layout)
    {
        if (prefab == null) return;

        float distanceOffset = type == TransitionType.Narrowing ? 0 : settings.transitionLength - 1;
        float currentDistance = ((tileIndex + distanceOffset) * layout.PhysicalLength) + (layout.PhysicalLength / 2f);

        double percent = targetSpline.Travel(0.0, currentDistance, Spline.Direction.Forward);
        SplineSample sample = targetSpline.Evaluate(percent);

        // prefabLane is in effective space (0..EffectiveLaneCount-1)
        float laneOffset = layout.StartOffset + (prefabLane * settings.laneWidth);
        Vector3 spawnPosition = sample.position + (sample.right * laneOffset);

        GameObject spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab, targetSpline.transform);
        spawned.transform.position = spawnPosition;
        spawned.transform.rotation = Quaternion.LookRotation(sample.forward, sample.up);

        float scaleX = isRightSide ? -settings.laneWidth : settings.laneWidth;
        float scaleZ = type == TransitionType.Widening ? settings.laneWidth : -settings.laneWidth;
        spawned.transform.localScale = new Vector3(scaleX, settings.roadThickness, scaleZ);
        spawned.name = objName;

        Undo.RegisterCreatedObjectUndo(spawned, "Spawn Transition");
    }

    private void SpawnDecals(RoadLayout layout)
    {
        if (settings.smallPotholePrefab == null && settings.mediumPotholePrefab == null && settings.largePotholePrefab == null)
        {
            return;
        }

        var potholePool = new List<GameObject>();
        if (settings.smallPotholePrefab != null) potholePool.Add(settings.smallPotholePrefab);
        if (settings.mediumPotholePrefab != null) potholePool.Add(settings.mediumPotholePrefab);
        if (settings.largePotholePrefab != null) potholePool.Add(settings.largePotholePrefab);

        int countToSpawn = Random.Range(minDecals, maxDecals + 1);
        int minTilesSpacing = Mathf.Max(1, Mathf.CeilToInt(minSpacing / layout.PhysicalLength));
        int currentTileIndex = 0;

        for (int i = 0; i < countToSpawn; i++)
        {
            int tilesToSkip = Random.Range(minTilesSpacing, (layout.TotalTiles / countToSpawn) + 1);
            currentTileIndex += tilesToSkip;

            if (currentTileIndex >= layout.TotalTiles) break;

            int lane = FindValidInnerLane(currentTileIndex);
            if (lane < 0) continue;

            SpawnDecal(layout, i, currentTileIndex, lane, potholePool);
        }
    }

    private int FindValidInnerLane(int tileIndex)
    {
        for (int attempts = 0; attempts < 20; attempts++)
        {
            int randomLane = Random.Range(0, EffectiveLaneCount);

            bool isLaneValid = IsLaneValidAtTile(randomLane, tileIndex);
            bool isLeftEdge = randomLane == leftEdgeProfile[tileIndex];
            bool isRightEdge = randomLane == rightEdgeProfile[tileIndex];

            if (isLaneValid && !isLeftEdge && !isRightEdge)
            {
                return randomLane;
            }
        }

        return -1;
    }

    private void SpawnDecal(RoadLayout layout, int index, int tileIndex, int lane, List<GameObject> potholePool)
    {
        float currentDistance = (tileIndex * layout.PhysicalLength) + (layout.PhysicalLength / 2f);
        double percent = targetSpline.Travel(0.0, currentDistance, Spline.Direction.Forward);
        SplineSample sample = targetSpline.Evaluate(percent);

        // lane is in effective space (0..EffectiveLaneCount-1)
        float laneOffsetValue = layout.StartOffset + (lane * settings.laneWidth);

        Vector3 spawnPosition = sample.position + (sample.right * laneOffsetValue);
        spawnPosition.y += Mathf.Max(0.5f, (settings.roadThickness / 2f) + 0.1f);

        GameObject selectedPrefab = potholePool[Random.Range(0, potholePool.Count)];

        GameObject spawnedDecal = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab, targetSpline.transform);
        spawnedDecal.transform.position = spawnPosition;

        float randomRotationY = Random.Range(0f, 360f);
        spawnedDecal.transform.rotation = Quaternion.LookRotation(sample.forward, sample.up) * Quaternion.Euler(90f, randomRotationY, 0f);
        spawnedDecal.name = $"Generated_Decal_Lane_{lane}_{index}";

        if (spawnedDecal.TryGetComponent<DecalProjector>(out var projectorComponent))
        {
            projectorComponent.size = new Vector3(settings.laneWidth, settings.laneWidth, 1f);
        }

        Undo.RegisterCreatedObjectUndo(spawnedDecal, "Spawn Decal Road");
    }

    private bool IsLaneValidAtTile(int laneIndex, int tileIndex)
    {
        if (!roadTopologyMap.TryGetValue(laneIndex, out var existingSegments)) return false;

        foreach (Vector2Int seg in existingSegments)
        {
            if (tileIndex >= seg.x && tileIndex < seg.y) return true;
        }

        return false;
    }

    private void ApplyRoadViewData(RoadLayout layout)
    {
        // roadView expects VISUAL lanes (0..VisualLaneCount-1).
        // Internal profiles/topology use EFFECTIVE lanes (0..EffectiveLaneCount-1).
        // Mapping:
        //   Effective 0 = left half-edge → visual lane 0 (left edge)
        //   Effective 1..VisualLaneCount-1 = center lanes → visual lanes 1..VisualLaneCount-1
        //   Effective VisualLaneCount = right half-edge → visual lane VisualLaneCount-1 (right edge)
        // So: leftEdgeVisual = leftEdgeProfile (no shift, clamp)
        //     rightEdgeVisual = rightEdgeProfile - 1 (shift right half-edge to last visual lane)
        
        roadView.laneCount = VisualLaneCount;
        roadView.roadLength = layout.TotalLength;
        roadView.previousRoad = previousRoad;
        roadView.nextRoad = nextRoad;

        // Convert edge profiles
        int totalTiles = layout.TotalTiles;
        int[] leftEdgeVisual = new int[totalTiles];
        int[] rightEdgeVisual = new int[totalTiles];
        for (int i = 0; i < totalTiles; i++)
        {
            leftEdgeVisual[i] = Mathf.Clamp(leftEdgeProfile[i], 0, VisualLaneCount - 1);
            rightEdgeVisual[i] = Mathf.Clamp(rightEdgeProfile[i] - 1, 0, VisualLaneCount - 1);
        }
        roadView.leftEdgeProfile = leftEdgeVisual;
        roadView.rightEdgeProfile = rightEdgeVisual;

        // Convert topology map: effective lanes 0..VisualLaneCount -> visual 0..VisualLaneCount-1
        var visualTopology = new Dictionary<int, List<Vector2Int>>();
        foreach (var kvp in roadTopologyMap)
        {
            int effLane = kvp.Key;
            if (effLane >= 0 && effLane <= VisualLaneCount)
            {
                int visualLane = Mathf.Clamp(effLane, 0, VisualLaneCount - 1);
                visualTopology[visualLane] = kvp.Value;
            }
        }
        roadView.SetTopologyMap(visualTopology);
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;

        Undo.RecordObject(obj, "Set Layer");
        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private void GenerateTopologyMap(int totalTiles)
    {
        roadTopologyMap.Clear();
        leftEdgeProfile = new int[totalTiles];
        rightEdgeProfile = new int[totalTiles];
        
        int effLanes = EffectiveLaneCount;
        holesMap = new bool[effLanes, totalTiles];

        var sortedLeft = SortTransitions(leftTransitions);
        var sortedRight = SortTransitions(rightTransitions);

        // Profiles use EFFECTIVE indices (0..effLanes-1)
        int startLeftEdge = CalculateInitialEdgeLane(sortedLeft, false, effLanes);
        int startRightEdge = CalculateInitialEdgeLane(sortedRight, true, effLanes);

        for (int i = 0; i < totalTiles; i++)
        {
            leftEdgeProfile[i] = startLeftEdge;
            rightEdgeProfile[i] = startRightEdge;
        }

        ApplyEdgeTransitions(sortedLeft, startLeftEdge, false, totalTiles, effLanes);
        ApplyEdgeTransitions(sortedRight, startRightEdge, true, totalTiles, effLanes);

        BuildLaneSegments(totalTiles, effLanes);

        int maxLeft = leftEdgeProfile.Max();
        int maxRight = rightEdgeProfile.Max();
        Debug.Log($"[RoadGenerator] visualLanes={VisualLaneCount} effLanes={effLanes} | leftEdge[0]={leftEdgeProfile[0]} max={maxLeft} | rightEdge[0]={rightEdgeProfile[0]} max={maxRight} | leftT={leftTransitions.Count} rightT={rightTransitions.Count}");
    }

    private void ApplyEdgeTransitions(List<TransitionData> sorted, int startLane, bool isRightSide, int totalTiles, int effLanes)
    {
        int[] edgeProfile = isRightSide ? rightEdgeProfile : leftEdgeProfile;
        int currentLane = startLane;

        foreach (TransitionData t in sorted)
        {
            int prefabLane = TransitionPrefabLane(t.type, currentLane, isRightSide);
            currentLane += TransitionDelta(t.type, isRightSide);
            currentLane = Mathf.Clamp(currentLane, 0, effLanes - 1);

            int changeStartIndex = t.type == TransitionType.Narrowing
                ? t.tileIndex
                : t.tileIndex + settings.transitionLength;

            for (int i = Mathf.Max(0, changeStartIndex); i < totalTiles; i++)
            {
                edgeProfile[i] = currentLane;
            }

            int startTile = Mathf.Max(0, t.tileIndex);
            int endTile = Mathf.Min(totalTiles, t.tileIndex + settings.transitionLength);

            // holesMap uses EffectiveLaneCount indices directly (prefabLane is already in effective space)
            int holeLane = prefabLane;
            for (int i = startTile; i < endTile; i++)
            {
                if (holeLane < 0 || holeLane >= effLanes) continue;
                holesMap[holeLane, i] = true;
            }
        }
    }

    private void BuildLaneSegments(int totalTiles, int effLanes)
    {
        // Profiles and topology use EFFECTIVE indices (0..effLanes-1)
        for (int lane = 0; lane < effLanes; lane++)
        {
            var segments = new List<Vector2Int>();
            bool inSegment = false;
            int segmentStart = 0;

            for (int tile = 0; tile < totalTiles; tile++)
            {
                bool hasHole = holesMap[lane, tile];
                bool isActive = lane >= leftEdgeProfile[tile]
                    && lane <= rightEdgeProfile[tile]
                    && !hasHole;

                if (isActive && !inSegment)
                {
                    segmentStart = tile;
                    inSegment = true;
                }
                else if (!isActive && inSegment)
                {
                    segments.Add(new Vector2Int(segmentStart, tile));
                    inSegment = false;
                }
            }

            if (inSegment)
            {
                segments.Add(new Vector2Int(segmentStart, totalTiles));
            }

            if (segments.Count > 0)
            {
                roadTopologyMap[lane] = segments;
            }
        }
    }

    private void DrawForkCreatorSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();
        
        GUILayout.Label("Fork Prefab Creator (Variant B)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Creates a fork prefab with: Parent (RoadSegment) + EntryView + N ChildViews. Each View has its own SplineComputer.", MessageType.Info);
        
        forkEntryPoint = (Transform)EditorGUILayout.ObjectField("Entry Point", forkEntryPoint, typeof(Transform), true);
        
        EditorGUILayout.LabelField("Exit Points", EditorStyles.boldLabel);
        int exitCount = EditorGUILayout.IntSlider("Exit Count", forkExitPoints?.Length ?? 2, 2, 4);
        if (forkExitPoints == null || forkExitPoints.Length != exitCount)
        {
            System.Array.Resize(ref forkExitPoints, exitCount);
            System.Array.Resize(ref forkExitLanes, exitCount);
        }
        
        for (int i = 0; i < exitCount; i++)
        {
            EditorGUILayout.BeginHorizontal();
            forkExitPoints[i] = (Transform)EditorGUILayout.ObjectField($"Exit {i}", forkExitPoints[i], typeof(Transform), true);
            forkExitLanes[i] = EditorGUILayout.IntField("Lanes", forkExitLanes[i], GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }
        
        if (GUILayout.Button("Generate Fork Prefab", GUILayout.Height(40)))
        {
            GenerateForkPrefab();
        }
    }

    private void GenerateForkPrefab()
    {
        if (forkEntryPoint == null || System.Array.Exists(forkExitPoints, p => p == null))
        {
            EditorUtility.DisplayDialog("Error", "Assign all entry/exit points", "OK");
            return;
        }
        
        // Calculate total lanes first
        int totalLanes = 0;
        for (int i = 0; i < forkExitLanes.Length; i++) totalLanes += forkExitLanes[i];
        
        // 1. Create parent GameObject
        var parent = new GameObject("RoadSegment_Fork");
        parent.transform.position = forkEntryPoint.position;
        parent.transform.rotation = forkEntryPoint.rotation;
        
        // 2. Create ENTRY SPLINE (Entry Point -> Fork Split Point)
        // Find split point: average of exit points projected onto entry direction
        Vector3 splitPoint = CalculateSplitPoint(forkEntryPoint, forkExitPoints);
        
        var entrySpline = CreateSpline(forkEntryPoint.position, splitPoint, forkEntryPoint.forward);
        var entryViewObj = new GameObject("EntryView");
        entryViewObj.transform.SetParent(parent.transform);
        var entryView = entryViewObj.AddComponent<RoadSegmentView>();
        entryView.spline = entrySpline;
        entryView.isForkEntry = true;
        entryView.laneCount = totalLanes; // SET LANE COUNT (sum of exit lanes)
        
        // 3. Create EXIT SPLINES (Split Point -> Each Exit Point)
        var exitViews = new System.Collections.Generic.List<RoadSegmentView>();
        for (int i = 0; i < forkExitPoints.Length; i++)
        {
            var exitSpline = CreateSpline(splitPoint, forkExitPoints[i].position, forkExitPoints[i].forward);
            var exitViewObj = new GameObject($"ChildView_{i}");
            exitViewObj.transform.SetParent(parent.transform);
            var exitView = exitViewObj.AddComponent<RoadSegmentView>();
            exitView.spline = exitSpline;
            exitView.isForkEntry = false;
            exitView.laneCount = forkExitLanes[i]; // SET LANE COUNT
            exitViews.Add(exitView);
        }
        
        // 4. Add RoadSegment component on parent
        var segment = parent.AddComponent<RoadSegment>();
        segment.roadView = entryView; // Main reference
        segment.segmentType = RoadSegment.SegmentType.Fork;
        
        segment.entryPoint = CreateConnectionPoint(forkEntryPoint, totalLanes);
        segment.exitPoints = new ConnectionPoint[forkExitPoints.Length];
        for (int i = 0; i < forkExitPoints.Length; i++)
        {
            segment.exitPoints[i] = CreateConnectionPoint(forkExitPoints[i], forkExitLanes[i]);
        }
        
        // 5. Bake topology for each view (using RoadSettingsSO.laneWidth)
        float laneWidth = settings?.laneWidth ?? 3.5f;
        entryView.laneWidth = laneWidth;
        foreach (var ev in exitViews) ev.laneWidth = laneWidth;
        
        // Generate constant edge profiles for each view (straight segments, no transitions)
        int entryTiles = Mathf.Max(1, Mathf.FloorToInt(entrySpline.CalculateLength() / (laneWidth * 2f)));
        entryView.leftEdgeProfile = new int[entryTiles];
        entryView.rightEdgeProfile = new int[entryTiles];
        for (int i = 0; i < entryTiles; i++)
        {
            entryView.leftEdgeProfile[i] = 0;
            entryView.rightEdgeProfile[i] = totalLanes - 1;
        }
        
        for (int i = 0; i < exitViews.Count; i++)
        {
            var ev = exitViews[i];
            int exitTiles = Mathf.Max(1, Mathf.FloorToInt(ev.spline.CalculateLength() / (laneWidth * 2f)));
            ev.leftEdgeProfile = new int[exitTiles];
            ev.rightEdgeProfile = new int[exitTiles];
            for (int t = 0; t < exitTiles; t++)
            {
                ev.leftEdgeProfile[t] = 0;
                ev.rightEdgeProfile[t] = forkExitLanes[i] - 1;
            }
        }
        
        // 6. Save as Prefab
        string folderPath = "Assets/Game/Level/Prefabs/RoadSegments/Forks/";
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
        {
            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];
            for (int i = 1; i < parts.Length - 1; i++)
            {
                string nextPath = currentPath + "/" + parts[i];
                if (!UnityEditor.AssetDatabase.IsValidFolder(nextPath))
                {
                    UnityEditor.AssetDatabase.CreateFolder(currentPath, parts[i]);
                }
                currentPath = nextPath;
            }
        }
        
        string path = folderPath + "Fork_" + System.DateTime.Now.Ticks + ".prefab";
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(parent, path);
        UnityEngine.Object.DestroyImmediate(parent);
        
        Debug.Log($"[ForkCreator] Created fork prefab at {path}");
        UnityEditor.EditorUtility.RevealInFinder(path);
        
        ShowForkCreator = false;
    }
    
    private Vector3 CalculateSplitPoint(Transform entry, Transform[] exits)
    {
        // Project exits onto entry forward plane, average
        Vector3 entryPos = entry.position;
        Vector3 entryFwd = entry.forward;
        Vector3 sum = Vector3.zero;
        foreach (var ex in exits)
        {
            Vector3 toExit = ex.position - entryPos;
            float proj = Vector3.Dot(toExit, entryFwd);
            sum += entryPos + entryFwd * proj;
        }
        return sum / exits.Length;
    }
    
    private Dreamteck.Splines.SplineComputer CreateSpline(Vector3 start, Vector3 end, Vector3 forward)
    {
        var go = new GameObject("Spline");
        var spline = go.AddComponent<Dreamteck.Splines.SplineComputer>();
        spline.type = Dreamteck.Splines.Spline.Type.Bezier;
        
        Vector3 dir = (end - start).normalized;
        float dist = Vector3.Distance(start, end);
        
        Dreamteck.Splines.SplinePoint p0 = new Dreamteck.Splines.SplinePoint();
        p0.position = start;
        p0.normal = forward;
        p0.size = 1f;
        p0.SetTangent2Position(start + dir * dist * 0.33f);
        
        Dreamteck.Splines.SplinePoint p1 = new Dreamteck.Splines.SplinePoint();
        p1.position = end;
        p1.normal = forward;
        p1.size = 1f;
        p1.SetTangentPosition(end - dir * dist * 0.33f);
        
        spline.SetPoints(new[] { p0, p1 });
        return spline;
    }
    
    private ConnectionPoint CreateConnectionPoint(Transform t, int lanes)
    {
        var go = new GameObject($"ConnectionPoint_{lanes}L");
        go.transform.SetPositionAndRotation(t.position, t.rotation);
        var cp = go.AddComponent<ConnectionPoint>();
        // ConnectionPoint has private field numLanes with public property NumLanes
        // We need to set via reflection or use the serialized field
        var field = typeof(ConnectionPoint).GetField("numLanes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(cp, lanes);
        return cp;
    }
}