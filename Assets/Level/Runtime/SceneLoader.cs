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

            var scene = SceneManager.LoadSceneAsync("LevelScene", LoadSceneMode.Additive);
            await UniTask.WaitUntil(() => scene.isDone);
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