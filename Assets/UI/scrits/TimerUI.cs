using Level.Runtime;
using TMPro;
using UnityEngine;
using VContainer;

namespace UI
{
    public class TimerUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        private GameTimer _timer;

        [Inject]
        public void Construct(GameTimer timer) => _timer = timer;

        private void Update()
        {
            if (_timer == null) return;
            _label.text = _timer.FormattedTime;
        }
    }
}