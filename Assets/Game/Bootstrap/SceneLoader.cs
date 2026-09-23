using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Level.Runtime
{
    public sealed class SceneLoader
    {
        private const string LevelSceneName = "LevelScene";

        public async UniTask LoadLevel(string sceneName = LevelSceneName)
        {
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
    }
}