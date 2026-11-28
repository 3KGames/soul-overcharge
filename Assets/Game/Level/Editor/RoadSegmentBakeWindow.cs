#if UNITY_EDITOR

using Game.Level.Runtime;
using Game.Level.Runtime.Config;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditorInternal;

public class RoadSegmentBakeWindow : EditorWindow
{
    [SerializeField] private RoadSettingsSO bakeSettings;
    [SerializeField] private RoadGridSettings gridSettings;
    [SerializeField] private bool forceRebake = false;
    [SerializeField] private List<GameObject> targetPrefabs = new List<GameObject>();
    
    private ReorderableList reorderableList;
    private Vector2 scrollPos;
    
    [MenuItem("Tools/Road/Bake Road Segments (Multi-Select)")]
    private static void ShowWindow()
    {
        var window = GetWindow<RoadSegmentBakeWindow>("Bake Road Segments");
        window.minSize = new Vector2(400, 300);
    }
    
    void OnEnable()
    {
        reorderableList = new ReorderableList(targetPrefabs, typeof(GameObject), true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Target Prefabs"),
            drawElementCallback = (rect, index, active, focused) =>
            {
                if (index >= 0 && index < targetPrefabs.Count)
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    rect.y += 1;
                    targetPrefabs[index] = (GameObject)EditorGUI.ObjectField(
                        rect, targetPrefabs[index], typeof(GameObject), false);
                }
            },
            onAddCallback = list => targetPrefabs.Add(null),
            onRemoveCallback = list =>
            {
                if (list.index >= 0 && list.index < targetPrefabs.Count)
                    targetPrefabs.RemoveAt(list.index);
            },
            elementHeight = EditorGUIUtility.singleLineHeight + 4
        };
    }
    
    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        EditorGUILayout.LabelField("Bake Road Segments", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Settings
        bakeSettings = (RoadSettingsSO)EditorGUILayout.ObjectField(
            "Road Settings SO", bakeSettings, typeof(RoadSettingsSO), false);
        
        gridSettings = (RoadGridSettings)EditorGUILayout.ObjectField(
            "Road Grid Settings", gridSettings, typeof(RoadGridSettings), false);
        
        EditorGUILayout.Space();
        
        forceRebake = EditorGUILayout.ToggleLeft("Force Re-bake (overwrite serializedTopology & fix firstEntryLane)", forceRebake);
        if (forceRebake)
            EditorGUILayout.HelpBox("Полностью пересчитает топологию, исправит firstEntryLane на выходе форков, обновит laneCount на всех View.", MessageType.Warning);
        
        EditorGUILayout.Space();
        
        reorderableList.DoLayoutList();
        
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Current Open Prefab"))
        {
            var prefabAsset = GetOpenPrefabAssetWithRoadSegment();
            if (prefabAsset != null)
            {
                if (!targetPrefabs.Contains(prefabAsset))
                    targetPrefabs.Add(prefabAsset);
            }
            else
            {
                EditorUtility.DisplayDialog("No Prefab", "Open a prefab with RoadSegment in Prefab Mode first.", "OK");
            }
        }
        if (GUILayout.Button("Clear All"))
        {
            targetPrefabs.Clear();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        bool canBake = bakeSettings != null && gridSettings != null && targetPrefabs.Count > 0 && targetPrefabs.All(p => p != null);
        EditorGUI.BeginDisabledGroup(!canBake);
        
        if (GUILayout.Button($"Bake {targetPrefabs.Count} Prefab(s)", GUILayout.Height(35)))
        {
            BakeMultiple(targetPrefabs.ToArray(), bakeSettings, gridSettings, forceRebake);
        }
        
        EditorGUI.EndDisabledGroup();
        
        if (!canBake)
        {
            EditorGUILayout.HelpBox("Назначьте Settings и выберите хотя бы один префаб с RoadSegment.", MessageType.Warning);
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private static GameObject GetOpenPrefabAssetWithRoadSegment()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.prefabContentsRoot != null)
        {
            var root = stage.prefabContentsRoot;
            if (root.GetComponent<RoadSegment>() != null)
            {
                // Get the actual prefab asset, not the scene instance
                string assetPath = stage.assetPath;
                return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
        }
        return null;
    }
    
    private static void BakeMultiple(GameObject[] prefabs, RoadSettingsSO settings, RoadGridSettings gridSettings, bool force)
    {
        if (settings == null || gridSettings == null)
        {
            EditorUtility.DisplayDialog("Error", "RoadSettingsSO and RoadGridSettings must be assigned!", "OK");
            return;
        }
        
        float defaultLaneWidth = settings.laneWidth;
        float defaultCellSize = gridSettings.cellSize;
        int baked = 0, failed = 0;
        
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            
            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[RoadSegmentBake] {prefab.name} is not an asset prefab, skipping.");
                failed++;
                continue;
            }
            
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var segment = root.GetComponent<RoadSegment>();
                if (segment == null)
                {
                    Debug.LogWarning($"[RoadSegmentBake] {prefab.name} has no RoadSegment, skipping.");
                    failed++;
                    continue;
                }
                
                // Always sync from settings on force-rebake; otherwise only if not set
                if (force || segment.laneWidth <= 0f) segment.laneWidth = defaultLaneWidth;
                if (!Mathf.Approximately(segment.cellSize, defaultCellSize)) segment.cellSize = defaultCellSize;
                
                if (segment.BakeGrid(force)) baked++;
                else failed++;
                
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        
        Debug.Log($"[RoadSegmentBake] Готово: {baked}, с ошибками: {failed}. laneWidth={defaultLaneWidth}, cellSize={defaultCellSize}, force={force}");
        EditorUtility.DisplayDialog("Bake Complete", $"Baked: {baked}\nFailed: {failed}", "OK");
    }
}

public static class RoadSegmentBakeMenu
{
    [MenuItem("Tools/Road/Bake all road segments (default 3.5)")]
    private static void BakeAllDefault()
    {
        const float defaultLaneWidth = 3.5f;
        const float defaultCellSize = 40f;
        int baked = 0, failed = 0;
        
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponent<RoadSegment>() == null) continue;
            
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var segment = root.GetComponent<RoadSegment>();
                if (segment.laneWidth <= 0f) segment.laneWidth = defaultLaneWidth;
                if (!Mathf.Approximately(segment.cellSize, defaultCellSize)) segment.cellSize = defaultCellSize;
                
                if (segment.BakeGrid()) baked++;
                else failed++;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log($"[RoadSegmentBake] Готово: {baked}, с ошибками: {failed}. Default laneWidth=3.5, cellSize=40");
    }
}
#endif