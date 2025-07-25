using UnityEditor;
using UnityEditor.SceneManagement;

namespace Level.Editor
{
    [InitializeOnLoad]
    public static class PlayModeStartScene
    {
        // Путь к вашей Bootstrap-сцене относительно Assets
        const string BootstrapScenePath = "Assets/0_Scenes/Bootstrap.unity";

        static PlayModeStartScene()
        {
            // Unity при старте редактора присвоит эту сцену для Play Mode
            var bootstrapSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            EditorSceneManager.playModeStartScene = bootstrapSceneAsset;
        }
    }
}