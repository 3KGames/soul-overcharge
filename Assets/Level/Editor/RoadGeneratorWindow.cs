using System;
using System.Collections.Generic;
using Dreamteck.Splines;
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

    private RoadLayout CalculateLayout()
    {
        float totalSplineLength = targetSpline.CalculateLength();
        float physicalLength = settings.laneWidth * settings.textureRatio;
        int totalTiles = Mathf.Max(1, Mathf.FloorToInt(totalSplineLength / physicalLength));

        float totalWidth = laneCount * settings.laneWidth;
        float startOffset = (-totalWidth / 2f) + (settings.laneWidth / 2f);

        return new RoadLayout(totalSplineLength, physicalLength, totalTiles, startOffset);
    }

    private void BuildMeshChannels(SplineMesh splineMesh, RoadLayout layout)
    {
        for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            if (!roadTopologyMap.TryGetValue(laneIndex, out var segments)) continue;

            foreach (Vector2Int segment in segments)
            {
                CreateLaneChannel(splineMesh, layout, laneIndex, segment);
            }
        }
    }

    private void CreateLaneChannel(SplineMesh splineMesh, RoadLayout layout, int laneIndex, Vector2Int segment)
    {
        SplineMesh.Channel channel = splineMesh.AddChannel($"Lane_{laneIndex}_{segment.x}_{segment.y}");
        channel.AddMesh(settings.laneMesh);

        float currentOffset = layout.StartOffset + (laneIndex * settings.laneWidth);
        SetChannelLateralOffset(channel, currentOffset);

        channel.autoCount = true;
        var mesh = channel.GetMesh(0);
        mesh.scale = new Vector3(settings.laneWidth, settings.roadThickness, settings.laneWidth);

        ConfigureChannelUVs(channel, layout);
        ConfigureChannelClip(channel, layout, segment);

        channel.overrideMaterialID = true;
        ApplyLaneMaterial(channel, mesh, layout, laneIndex, segment, currentOffset);
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

    private void ApplyLaneMaterial(SplineMesh.Channel channel, SplineMesh.Channel.MeshDefinition mesh, RoadLayout layout, int laneIndex, Vector2Int segment, float currentOffset)
    {
        int midTile = Mathf.Clamp((segment.x + segment.y) / 2, 0, layout.TotalTiles - 1);
        bool isLeftEdge = laneIndex == leftEdgeProfile[midTile];
        bool isRightEdge = laneIndex == rightEdgeProfile[midTile];

        if (isLeftEdge && isRightEdge)
        {
            channel.targetMaterialID = 1;
            return;
        }

        if (isLeftEdge)
        {
            channel.targetMaterialID = 2;
            mesh.scale = new Vector3(settings.laneWidth * 0.5f, settings.roadThickness, settings.laneWidth);
            mesh.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.X;
            SetChannelLateralOffset(channel, currentOffset + 0.25f * settings.laneWidth);
            return;
        }

        if (isRightEdge)
        {
            channel.targetMaterialID = 2;
            mesh.scale = new Vector3(settings.laneWidth * 0.5f, settings.roadThickness, settings.laneWidth);
            mesh.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.None;
            SetChannelLateralOffset(channel, currentOffset - 0.25f * settings.laneWidth);
            return;
        }

        channel.targetMaterialID = ResolveCenterLaneMaterial(laneIndex);
    }

    private int ResolveCenterLaneMaterial(int laneIndex)
    {
        int centerLaneId = (laneCount - 1) / 2;
        int idFromCenter = centerLaneId - laneIndex;

        return (idFromCenter != 0 && laneIndex % 2 == 0) ? 0 : 1;
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
        if (laneCount < 2) return;

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

    private int CalculateInitialEdgeLane(List<TransitionData> sorted, bool isRightSide)
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

        return isRightSide ? (laneCount - 1) - extreme : -extreme;
    }

    private void SpawnSideTransitions(List<TransitionData> sorted, bool isRightSide, RoadLayout layout)
    {
        if (sorted.Count == 0) return;

        int spawnLane = CalculateInitialEdgeLane(sorted, isRightSide);

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
            int randomLane = Random.Range(0, laneCount);

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
        roadView.laneCount = laneCount;
        roadView.roadLength = layout.TotalLength;
        roadView.previousRoad = previousRoad;
        roadView.nextRoad = nextRoad;
        roadView.SetTopologyMap(roadTopologyMap);
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
        holesMap = new bool[laneCount, totalTiles];

        var sortedLeft = SortTransitions(leftTransitions);
        var sortedRight = SortTransitions(rightTransitions);

        int startLeftEdge = CalculateInitialEdgeLane(sortedLeft, false);
        int startRightEdge = CalculateInitialEdgeLane(sortedRight, true);

        for (int i = 0; i < totalTiles; i++)
        {
            leftEdgeProfile[i] = startLeftEdge;
            rightEdgeProfile[i] = startRightEdge;
        }

        ApplyEdgeTransitions(sortedLeft, startLeftEdge, false, totalTiles);
        ApplyEdgeTransitions(sortedRight, startRightEdge, true, totalTiles);

        BuildLaneSegments(totalTiles);
    }

    private void ApplyEdgeTransitions(List<TransitionData> sorted, int startLane, bool isRightSide, int totalTiles)
    {
        int[] edgeProfile = isRightSide ? rightEdgeProfile : leftEdgeProfile;
        int currentLane = startLane;

        foreach (TransitionData t in sorted)
        {
            int prefabLane = TransitionPrefabLane(t.type, currentLane, isRightSide);
            currentLane += TransitionDelta(t.type, isRightSide);

            int changeStartIndex = t.type == TransitionType.Narrowing
                ? t.tileIndex
                : t.tileIndex + settings.transitionLength;

            for (int i = Mathf.Max(0, changeStartIndex); i < totalTiles; i++)
            {
                edgeProfile[i] = currentLane;
            }

            int startTile = Mathf.Max(0, t.tileIndex);
            int endTile = Mathf.Min(totalTiles, t.tileIndex + settings.transitionLength);

            for (int i = startTile; i < endTile; i++)
            {
                if (prefabLane < 0 || prefabLane >= laneCount) continue;
                holesMap[prefabLane, i] = true;
            }
        }
    }

    private void BuildLaneSegments(int totalTiles)
    {
        for (int lane = 0; lane < laneCount; lane++)
        {
            var segments = new List<Vector2Int>();
            bool inSegment = false;
            int segmentStart = 0;

            for (int tile = 0; tile < totalTiles; tile++)
            {
                bool isActive = lane >= leftEdgeProfile[tile]
                    && lane <= rightEdgeProfile[tile]
                    && !holesMap[lane, tile];

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
}