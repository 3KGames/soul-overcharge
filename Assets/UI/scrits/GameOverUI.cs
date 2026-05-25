using Common.Runtime.StateMachine;
using Level.Runtime;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI
{
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button     _restartButton;
        [SerializeField] private Button     _quitButton;

        private GameOverService          _gameOverService;
        private IStateSwitcher<GameState> _fsm;

        [Inject]
        public void Construct(GameOverService gameOverService, IStateSwitcher<GameState> fsm)
        {
            _gameOverService = gameOverService;
            _fsm             = fsm;
        }

        private void Awake()
        {
            Time.timeScale = 1f;
            if (_panel != null) _panel.SetActive(false);
        }

        private void Start()
        {
            if (_gameOverService == null)
            {
                Debug.LogError("[GameOverUI] GameOverService = null!");
                return;
            }

            _gameOverService.OnGameOver += Show;
            if (_restartButton != null) _restartButton.onClick.AddListener(Restart);
            if (_quitButton    != null) _quitButton.onClick.AddListener(Quit);
        }

        private void Show()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            _gameOverService.Reset();
            _fsm.Switch(GameState.Loading);
        }

        private void Quit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (_gameOverService != null)
                _gameOverService.OnGameOver -= Show;
        }
    }
}