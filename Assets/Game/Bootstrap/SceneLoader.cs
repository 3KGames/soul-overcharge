using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Level.Runtime
{
    public sealed class SceneLoader
    {
        private const string LevelSceneName = "LevelScene";

        public async UniTask LoadLevel(string sceneName = LevelSceneName)
        {
#if UNITY_EDITOR
            if (sceneName == LevelSceneName && OpenedSceneTracker.TryGetOpenedScene(out string openedPath))
            {
                await LoadByPathInEditor(openedPath);
                return;
            }
#endif
            var existing = SceneManager.GetSceneByName(sceneName);
            if (existing.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(sceneName);
                await UniTask.WaitUntil(() => unload.isDone);
            }

            var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            await UniTask.WaitUntil(() => load.isDone);

            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
        }

#if UNITY_EDITOR
        private static async UniTask LoadByPathInEditor(string path)
        {
            Debug.Log($"[SceneLoader] Загружается сцена, открытая в редакторе: {path}");

            var existing = SceneManager.GetSceneByPath(path);
            if (existing.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(existing);
                await UniTask.WaitUntil(() => unload.isDone);
            }

            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            await UniTask.WaitUntil(() => load.isDone);

            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(path));
        }
#endif
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
    internal static class OpenedSceneTracker
    {
        private const string Key = "Level.SceneLoader.OpenedScene";

        static OpenedSceneTracker()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                SessionState.SetString(Key, EditorSceneManager.GetActiveScene().path);
        }

        public static bool TryGetOpenedScene(out string path)
        {
            path = SessionState.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(path)) return false;

            var start = EditorSceneManager.playModeStartScene;
            if (start != null && AssetDatabase.GetAssetPath(start) == path) return false;

            return true;
        }
    }
#endif
}