using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace Level.Runtime
{
    public sealed class SceneLoader
    {
        //public LevelData DataToPass { get; private set; }

        public async UniTask LoadLevel(string levelName = "LevelData") // LevelData - ����������� ������� ��� ������������. ���� �� ������ ����, �� ����� ��������� ������ ��� ������
        {
            /*DataToPass = Resources.Load<LevelData>(levelName);
            Debug.Log($"Loaded {DataToPass}");*/

			string sceneName = "LevelScene";
			
            var scene = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            await UniTask.WaitUntil(() => scene.isDone);
			
			Scene loadedScene = SceneManager.GetSceneByName(sceneName);
			SceneManager.SetActiveScene(loadedScene);
        }
        
        /*public LevelData ConsumeAndClear()
        {
            var data = DataToPass;
            DataToPass = null;
            Debug.Log($"Consume {data} and clear");
            return data;
        }*/
    }
}