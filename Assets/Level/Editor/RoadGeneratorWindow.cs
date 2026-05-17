using System;
using UnityEngine;
using UnityEditor;
using Dreamteck.Splines;
using System.Collections.Generic;
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

    [Header("Основные ссылки")]
    private SplineComputer targetSpline;
    private RoadSettingsSO settings; 

    [Header("Связи дороги")]
    private RoadSegmentView previousRoad;
    private RoadSegmentView nextRoad;

    private int laneCount = 3;
    private int minDecals = 3;
    private int maxDecals = 7;
    private float minSpacing = 5f;

    private Dictionary<int, List<Vector2Int>> roadTopologyMap = new Dictionary<int, List<Vector2Int>>();

    private int[] leftEdgeProfile;
    private int[] rightEdgeProfile;
    private bool[,] holesMap;

    [SerializeField] private List<TransitionData> leftTransitions = new List<TransitionData>();
    [SerializeField] private List<TransitionData> rightTransitions = new List<TransitionData>();

    private Vector2 scrollPos;

    [MenuItem("Tools/Road Generator")]
    public static void ShowWindow()
    {
        GetWindow<RoadGeneratorWindow>("Генератор Дорог");
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Базовые настройки", EditorStyles.boldLabel);
        
        targetSpline = (SplineComputer)EditorGUILayout.ObjectField("Целевой Сплайн", targetSpline, typeof(SplineComputer), true);
        settings = (RoadSettingsSO)EditorGUILayout.ObjectField("Настройки (SO)", settings, typeof(RoadSettingsSO), false);
        laneCount = EditorGUILayout.IntSlider("Количество полос", laneCount, 1, 11);

        EditorGUILayout.Space();
        GUILayout.Label("Соединения графа дорог", EditorStyles.boldLabel);
        previousRoad = (RoadSegmentView)EditorGUILayout.ObjectField("Предыдущая дорога", previousRoad, typeof(RoadSegmentView), true);
        nextRoad = (RoadSegmentView)EditorGUILayout.ObjectField("Следующая дорога", nextRoad, typeof(RoadSegmentView), true);

        EditorGUILayout.Space();
        
        DrawTransitionsList("Переходы: Левая сторона", leftTransitions);
        DrawTransitionsList("Переходы: Правая сторона", rightTransitions);

        EditorGUILayout.Space();
        GUILayout.Label("Настройки ям (Декалей)", EditorStyles.boldLabel);
        minDecals = EditorGUILayout.IntField("Мин. количество ям", minDecals);
        maxDecals = EditorGUILayout.IntField("Макс. количество ям", maxDecals);
        minSpacing = EditorGUILayout.FloatField("Мин. расстояние (метры)", minSpacing);

        EditorGUILayout.Space();

        if (GUILayout.Button("Сгенерировать дорогу", GUILayout.Height(40)))
        {
            GenerateEverything();
        }

        EditorGUILayout.EndScrollView();
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

    private void GenerateEverything()
    {
        if (targetSpline == null || settings == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Пожалуйста, выберите Spline Computer и файл настроек RoadSettingsSO!", "OK");
            return;
        }
    
        if (!targetSpline.TryGetComponent<RoadSegmentView>(out var roadView))
            roadView = targetSpline.gameObject.AddComponent<RoadSegmentView>();
    
        if (targetSpline.TryGetComponent<MeshRenderer>(out var meshRenderer))
            meshRenderer.materials = new [] { settings.cleanEmptyRoadMaterial, settings.cleanCenterLaneMaterial, settings.cleanSideLaneMaterial };
        else
            Debug.LogError("Spline Computer must have MeshRenderer!");
    
        SplineMesh splineMesh = targetSpline.GetComponent<SplineMesh>();
        if (splineMesh == null) splineMesh = targetSpline.gameObject.AddComponent<SplineMesh>();
    
        while (splineMesh.GetChannelCount() > 0) splineMesh.RemoveChannel(0);
    
        float totalSplineLength = targetSpline.CalculateLength();
        float physicalLength = settings.laneWidth * settings.textureRatio;
        int totalTiles = Mathf.Max(1, Mathf.FloorToInt(totalSplineLength / physicalLength));
    
        float totalWidth = laneCount * settings.laneWidth;
        float startOffset = (-totalWidth / 2f) + (settings.laneWidth / 2f);
    
        GenerateTopologyMap(totalTiles);
    
        for (int i = 0; i < laneCount; i++)
        {
            if (!roadTopologyMap.ContainsKey(i)) continue;
            List<Vector2Int> segments = roadTopologyMap[i];
    
            foreach (var seg in segments)
            {
                SplineMesh.Channel channel = splineMesh.AddChannel($"Lane_{i}_{seg.x}_{seg.y}");
                channel.AddMesh(settings.laneMesh);
                
                float currentOffset = startOffset + (i * settings.laneWidth);
                channel.minOffset = new Vector3(currentOffset, 0, 0);
                channel.maxOffset = new Vector3(currentOffset, 0, 0);
                
                channel.autoCount = true;
                var mesh = channel.GetMesh(0);

                mesh.scale = new Vector3(settings.laneWidth, settings.roadThickness, settings.laneWidth);
                
                channel.overrideUVs = SplineMesh.Channel.UVOverride.UniformV;
                float correctUvScale = 1f / physicalLength;
                channel.uvScale = new Vector2(1f, correctUvScale);
    
                channel.clipFrom = targetSpline.Travel(0.0, seg.x * physicalLength, Spline.Direction.Forward);
                channel.clipTo = targetSpline.Travel(0.0, seg.y * physicalLength, Spline.Direction.Forward);
    
                channel.overrideMaterialID = true;
    
                int midTile = (seg.x + seg.y) / 2; 
                midTile = Mathf.Clamp(midTile, 0, totalTiles - 1);
                
                bool isLeftEdge = (i == leftEdgeProfile[midTile]);
                bool isRightEdge = (i == rightEdgeProfile[midTile]);
    
                if (isLeftEdge && isRightEdge)
                {
                    channel.targetMaterialID = 1;
                }
                else if (isLeftEdge) 
                {
                    channel.targetMaterialID = 2;
                    mesh.scale = new Vector3(settings.laneWidth * 0.5f, settings.roadThickness, settings.laneWidth);
                    mesh.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.X;
                    channel.minOffset = new Vector3(currentOffset + 0.25f * settings.laneWidth, 0, 0);
                    channel.maxOffset = new Vector3(currentOffset + 0.25f * settings.laneWidth, 0, 0);
                }
                else if (isRightEdge)
                {
                    channel.targetMaterialID = 2;
                    mesh.scale = new Vector3(settings.laneWidth * 0.5f, settings.roadThickness, settings.laneWidth);
                    mesh.mirror = SplineMesh.Channel.MeshDefinition.MirrorMethod.None;
                    channel.minOffset = new Vector3(currentOffset - 0.25f * settings.laneWidth, 0, 0);
                    channel.maxOffset = new Vector3(currentOffset - 0.25f * settings.laneWidth, 0, 0);
                }
                else
                {
                    int centerLaneId = (laneCount - 1) / 2;
                    int idFromCenter = centerLaneId - i;
                    
                    if (idFromCenter != 0 && i % 2 == 0) channel.targetMaterialID = 0;
                    else channel.targetMaterialID = 1;
                }
            }
        }
    
        splineMesh.Rebuild();
    
        List<GameObject> childrenToRemove = new List<GameObject>();
        foreach (Transform child in targetSpline.transform)
        {
            if (child.name.StartsWith("Generated_Decal_") || child.name.StartsWith("Generated_Transition_"))
            {
                childrenToRemove.Add(child.gameObject);
            }
        }
        childrenToRemove.ForEach(DestroyImmediate);
    
        if (laneCount >= 2)
        {
            var sortedLeft = new List<TransitionData>(leftTransitions);
            sortedLeft.Sort((a, b) => a.tileIndex.CompareTo(b.tileIndex));
            
            int minL = 0; int curl = 0;
            foreach (var t in sortedLeft) { curl += (t.type == TransitionType.Narrowing ? 1 : -1); if (curl < minL) minL = curl; }
            int currentLeftForSpawn = -minL;

            for (int i = 0; i < sortedLeft.Count; i++)
            {
                var t = sortedLeft[i];
                int prefabLane = (t.type == TransitionType.Narrowing) ? currentLeftForSpawn + 1 : currentLeftForSpawn;
                
                SpawnTransitionPrefab(t.tileIndex, prefabLane, t.type, false, settings.leftTransitionPrefab, $"Generated_Transition_Left_{i}", physicalLength, startOffset);
                
                currentLeftForSpawn = (t.type == TransitionType.Narrowing) ? currentLeftForSpawn + 1 : currentLeftForSpawn - 1;
            }

            var sortedRight = new List<TransitionData>(rightTransitions);
            sortedRight.Sort((a, b) => a.tileIndex.CompareTo(b.tileIndex));

            int maxR = 0; int curr = 0;
            foreach (var t in sortedRight) { curr += (t.type == TransitionType.Narrowing ? -1 : 1); if (curr > maxR) maxR = curr; }
            int currentRightForSpawn = (laneCount - 1) - maxR;

            for (int i = 0; i < sortedRight.Count; i++)
            {
                var t = sortedRight[i];
                int prefabLane = (t.type == TransitionType.Narrowing) ? currentRightForSpawn - 1 : currentRightForSpawn;
                
                SpawnTransitionPrefab(t.tileIndex, prefabLane, t.type, true, settings.leftTransitionPrefab, $"Generated_Transition_Right_{i}", physicalLength, startOffset);
                
                currentRightForSpawn = (t.type == TransitionType.Narrowing) ? currentRightForSpawn - 1 : currentRightForSpawn + 1;
            }
        }
    
        // НОВОЕ: Динамический спавн декалей (ям) из 3-х префабов только на внутренних полосах
        bool hasPotholePrefabs = settings.smallPotholePrefab != null || settings.mediumPotholePrefab != null || settings.largePotholePrefab != null;
        if (hasPotholePrefabs)
        {
            // Формируем список доступных префабов ям
            List<GameObject> potholePool = new List<GameObject>();
            if (settings.smallPotholePrefab != null) potholePool.Add(settings.smallPotholePrefab);
            if (settings.mediumPotholePrefab != null) potholePool.Add(settings.mediumPotholePrefab);
            if (settings.largePotholePrefab != null) potholePool.Add(settings.largePotholePrefab);

            int countToSpawn = Random.Range(minDecals, maxDecals + 1);
            int minTilesSpacing = Mathf.Max(1, Mathf.CeilToInt(minSpacing / physicalLength));
            int currentTileIndex = 0;
    
            for (int i = 0; i < countToSpawn; i++)
            {
                int tilesToSkip = Random.Range(minTilesSpacing, (totalTiles / countToSpawn) + 1);
                currentTileIndex += tilesToSkip;
    
                if (currentTileIndex >= totalTiles) break;
    
                int randomLane = -1;
                int attempts = 0;
                bool foundValidInnerLane = false;
                
                // Ищем случайную внутреннюю полосу
                while (attempts < 20)
                {
                    randomLane = Random.Range(0, laneCount);
                    
                    bool isLaneValid = IsLaneValidAtTile(randomLane, currentTileIndex);
                    bool isLeftEdge = (randomLane == leftEdgeProfile[currentTileIndex]);
                    bool isRightEdge = (randomLane == rightEdgeProfile[currentTileIndex]);

                    // Проверяем: полоса должна существовать И не быть краем
                    if (isLaneValid && !isLeftEdge && !isRightEdge)
                    {
                        foundValidInnerLane = true;
                        break;
                    }
                    attempts++;
                }
                
                // Если дорога узкая (1-2 полосы) или подходящая полоса не найдена - пропускаем спавн
                if (!foundValidInnerLane) continue;
    
                float currentDistance = (currentTileIndex * physicalLength) + (physicalLength / 2f);
                double percent = targetSpline.Travel(0.0, currentDistance, Spline.Direction.Forward);
                SplineSample sample = targetSpline.Evaluate(percent);
    
                float laneOffsetValue = startOffset + (randomLane * settings.laneWidth);
    
                Vector3 spawnPosition = sample.position + (sample.right * laneOffsetValue);
                spawnPosition.y += Mathf.Max(0.5f, (settings.roadThickness / 2f) + 0.1f);
    
                // Случайный выбор префаба ямы из пула
                GameObject selectedPrefab = potholePool[Random.Range(0, potholePool.Count)];
    
                GameObject spawnedDecal = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
                spawnedDecal.transform.position = spawnPosition;
                
                // Добавляем случайное вращение по оси Y для разнообразия, ось X остается 90 для проектора
                float randomRotationY = Random.Range(0f, 360f); 
                spawnedDecal.transform.rotation = Quaternion.LookRotation(sample.forward, sample.up) * Quaternion.Euler(90f, randomRotationY, 0f);
                spawnedDecal.transform.SetParent(targetSpline.transform);
                spawnedDecal.name = $"Generated_Decal_Lane_{randomLane}_{i}";
    
                if (spawnedDecal.TryGetComponent<DecalProjector>(out var projectorComponent))
                {
                    projectorComponent.size = new Vector3(settings.laneWidth, settings.laneWidth, 1f);
                }
    
                Undo.RegisterCreatedObjectUndo(spawnedDecal, "Spawn Decal Road");
            }
        }
    
        roadView.laneCount = laneCount;
        roadView.roadLength = totalSplineLength;
        roadView.previousRoad = previousRoad;
        roadView.nextRoad = nextRoad;
        roadView.SetTopologyMap(roadTopologyMap);
        
        EditorUtility.SetDirty(roadView);
        Debug.Log($"[RoadGenerator] Успешно создана дорога с динамической матричной топологией.");
    }

    private void GenerateTopologyMap(int totalTiles)
    {
        roadTopologyMap.Clear();
        leftEdgeProfile = new int[totalTiles];
        rightEdgeProfile = new int[totalTiles];
        holesMap = new bool[laneCount, totalTiles];

        var sortedLeft = new List<TransitionData>(leftTransitions);
        sortedLeft.Sort((a, b) => a.tileIndex.CompareTo(b.tileIndex));

        var sortedRight = new List<TransitionData>(rightTransitions);
        sortedRight.Sort((a, b) => a.tileIndex.CompareTo(b.tileIndex));

        int minLeftDelta = 0; int leftDelta = 0;
        foreach (var t in sortedLeft)
        {
            leftDelta += (t.type == TransitionType.Narrowing) ? 1 : -1;
            if (leftDelta < minLeftDelta) minLeftDelta = leftDelta;
        }
        int startLeftEdge = -minLeftDelta;

        int maxRightDelta = 0; int rightDelta = 0;
        foreach (var t in sortedRight)
        {
            rightDelta += (t.type == TransitionType.Narrowing) ? -1 : 1;
            if (rightDelta > maxRightDelta) maxRightDelta = rightDelta;
        }
        int startRightEdge = (laneCount - 1) - maxRightDelta;

        for (int i = 0; i < totalTiles; i++)
        {
            leftEdgeProfile[i] = startLeftEdge;
            rightEdgeProfile[i] = startRightEdge;
        }

        int curLeft = startLeftEdge;
        foreach (var t in sortedLeft)
        {
            int prefabLane = (t.type == TransitionType.Narrowing) ? curLeft + 1 : curLeft;
            curLeft += (t.type == TransitionType.Narrowing) ? 1 : -1;
            
            int changeStartIndex = (t.type == TransitionType.Narrowing) ? t.tileIndex : t.tileIndex + settings.transitionLength;
            for (int i = changeStartIndex; i < totalTiles; i++)
            {
                if (i >= 0 && i < totalTiles) leftEdgeProfile[i] = curLeft;
            }

            for (int i = t.tileIndex; i < t.tileIndex + settings.transitionLength; i++)
            {
                if (prefabLane >= 0 && prefabLane < laneCount && i >= 0 && i < totalTiles) holesMap[prefabLane, i] = true;
            }
        }

        int curRight = startRightEdge;
        foreach (var t in sortedRight)
        {
            int prefabLane = (t.type == TransitionType.Narrowing) ? curRight - 1 : curRight;
            curRight += (t.type == TransitionType.Narrowing) ? -1 : 1;
            
            int changeStartIndex = (t.type == TransitionType.Narrowing) ? t.tileIndex : t.tileIndex + settings.transitionLength;
            for (int i = changeStartIndex; i < totalTiles; i++)
            {
                if (i >= 0 && i < totalTiles) rightEdgeProfile[i] = curRight;
            }

            for (int i = t.tileIndex; i < t.tileIndex + settings.transitionLength; i++)
            {
                if (prefabLane >= 0 && prefabLane < laneCount && i >= 0 && i < totalTiles) holesMap[prefabLane, i] = true;
            }
        }

        for (int l = 0; l < laneCount; l++)
        {
            List<Vector2Int> segs = new List<Vector2Int>();
            bool inSegment = false;
            int segStart = 0;

            for (int t = 0; t < totalTiles; t++)
            {
                bool isActive = (l >= leftEdgeProfile[t]) && (l <= rightEdgeProfile[t]) && !holesMap[l, t];

                if (isActive && !inSegment)
                {
                    segStart = t;
                    inSegment = true;
                }
                else if (!isActive && inSegment)
                {
                    segs.Add(new Vector2Int(segStart, t));
                    inSegment = false;
                }
            }

            if (inSegment) segs.Add(new Vector2Int(segStart, totalTiles));
            if (segs.Count > 0) roadTopologyMap[l] = segs;
        }
    }

    private void SpawnTransitionPrefab(int tileIndex, int prefabLane, TransitionType type, bool isRightSide, GameObject prefab, string objName, float physicalLength, float startOffset)
    {
        if (prefab == null) return;

        float distanceOffset = type == TransitionType.Narrowing ? 0 : settings.transitionLength - 1;
        float currentDistance = ((tileIndex + distanceOffset) * physicalLength) + (physicalLength / 2f);
        
        double percent = targetSpline.Travel(0.0, currentDistance, Spline.Direction.Forward);
        SplineSample sample = targetSpline.Evaluate(percent);

        float offsetB = startOffset + (prefabLane * settings.laneWidth);
        Vector3 spawnPosition = sample.position + (sample.right * offsetB);

        GameObject spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        spawned.transform.position = spawnPosition;
        spawned.transform.rotation = Quaternion.LookRotation(sample.forward, sample.up);
        
        float scaleX = isRightSide ? -settings.laneWidth : settings.laneWidth;
        float scaleZ = type == TransitionType.Widening ? settings.laneWidth : -settings.laneWidth;
        
        spawned.transform.localScale = new Vector3(scaleX, settings.roadThickness, scaleZ);
        
        spawned.transform.SetParent(targetSpline.transform);
        spawned.name = objName;

        Undo.RegisterCreatedObjectUndo(spawned, "Spawn Transition");
    }

    private bool IsLaneValidAtTile(int laneIndex, int tileIndex)
    {
        if (!roadTopologyMap.ContainsKey(laneIndex)) return false;

        List<Vector2Int> existingSegments = roadTopologyMap[laneIndex];
        foreach (Vector2Int seg in existingSegments)
        {
            if (tileIndex >= seg.x && tileIndex < seg.y) return true;
        }
        return false; 
    }
}