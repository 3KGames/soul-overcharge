#if UNITY_EDITOR

using Game.Level.Runtime;
using Game.Level.Runtime.Config;
using UnityEditor;
using UnityEngine;

public class RoadSegmentBakeWindow : EditorWindow
{
    [SerializeField] private RoadSettingsSO bakeSettings;
    [SerializeField] private RoadGridSettings gridSettings;
    [SerializeField] private bool forceRebake = false;
	
    [MenuItem("Tools/Road/Bake all road segments (with settings)")]
    private static void ShowWindow()
    {
        var window = GetWindow<RoadSegmentBakeWindow>("Bake Road Segments");
        window.minSize = new Vector2(350, 220);
    }
    
    void OnGUI()
    {
        EditorGUILayout.LabelField("Bake Road Segments", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        bakeSettings = (RoadSettingsSO)EditorGUILayout.ObjectField(
            "Road Settings SO", bakeSettings, typeof(RoadSettingsSO), false);
        
        gridSettings = (RoadGridSettings)EditorGUILayout.ObjectField(
            "Road Grid Settings", gridSettings, typeof(RoadGridSettings), false);
        
        EditorGUILayout.Space();
        forceRebake = EditorGUILayout.ToggleLeft("Force Re-bake (overwrite serializedTopology & fix firstEntryLane)", forceRebake);
        if (forceRebake)
            EditorGUILayout.HelpBox("Полностью пересчитает топологию, исправит firstEntryLane на выходе форков, обновит laneCount на всех View.", MessageType.Warning);
        
        EditorGUILayout.Space();
        bool canBake = bakeSettings != null && gridSettings != null;
        EditorGUI.BeginDisabledGroup(!canBake);
        
        if (GUILayout.Button("Bake All RoadSegment Prefabs", GUILayout.Height(30)))
        {
            BakeAll(bakeSettings, gridSettings, forceRebake);
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Current Open Prefab", EditorStyles.boldLabel);
        var openPrefab = GetOpenPrefabWithRoadSegment();
        if (openPrefab != null)
        {
            EditorGUILayout.ObjectField(openPrefab, typeof(GameObject), false);
            if (GUILayout.Button("Bake This Prefab Only", GUILayout.Height(30)))
            {
                BakeSingle(openPrefab, bakeSettings, gridSettings, forceRebake);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Откройте префаб с RoadSegment в Prefab Mode.", MessageType.Info);
        }
        
        EditorGUI.EndDisabledGroup();
        
        if (!canBake)
        {
            EditorGUILayout.HelpBox("Назначьте оба Settings для активации кнопки.", MessageType.Warning);
        }
    }
    
    private static GameObject GetOpenPrefabWithRoadSegment()
    {
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.prefabContentsRoot != null)
        {
            return stage.prefabContentsRoot.GetComponent<RoadSegment>() != null ? stage.prefabContentsRoot : null;
        }
        return null;
    }
    
    private static void BakeAll(RoadSettingsSO settings, RoadGridSettings gridSettings, bool force)
    {
        if (settings == null || gridSettings == null)
        {
            EditorUtility.DisplayDialog("Error", "RoadSettingsSO and RoadGridSettings must be assigned!", "OK");
            return;
        }
        
        float defaultLaneWidth = settings.laneWidth;
        float defaultCellSize = gridSettings.cellSize;
        int baked = 0, failed = 0;
        
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponent<RoadSegment>() == null) continue;
            
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var segment = root.GetComponent<RoadSegment>();
                
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
    }
    
    private static void BakeSingle(GameObject root, RoadSettingsSO settings, RoadGridSettings gridSettings, bool force)
    {
        var segment = root.GetComponent<RoadSegment>();
        if (segment == null) return;
        
        // Always sync from settings on force-rebake; otherwise only if not set
        if (force || segment.laneWidth <= 0f) segment.laneWidth = settings.laneWidth;
        if (!Mathf.Approximately(segment.cellSize, gridSettings.cellSize)) segment.cellSize = gridSettings.cellSize;
        
        bool ok = segment.BakeGrid(force);
        Debug.Log($"[RoadSegmentBake] Single: {(ok ? "OK" : "FAILED")}. force={force}");
        
        // Save if in prefab stage
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
        }
        else
        {
            // Regular prefab asset
            string path = AssetDatabase.GetAssetPath(root);
            if (!string.IsNullOrEmpty(path))
                PrefabUtility.SaveAsPrefabAsset(root, path);
        }
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