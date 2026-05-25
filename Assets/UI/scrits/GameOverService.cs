using System;
using UnityEngine;

namespace Level.Runtime
{
    public class GameOverService
    {
        public event Action OnGameOver;

        private readonly AudioVolumeService _audioVolumeService;
        private bool _triggered;

        public GameOverService(AudioVolumeService audioVolumeService)
        {
            _audioVolumeService = audioVolumeService;
        }

        public void TriggerGameOver()
        {
            if (_triggered) return;
            _triggered = true;

            _audioVolumeService.MuteAllExceptMusic();

            Time.timeScale = 0f;
            Debug.Log("[GameOverService] TriggerGameOver вызван!");
            OnGameOver?.Invoke();
        }

        public void Reset()
        {
            _triggered = false;
            _audioVolumeService.RestoreAll();
        }
    }
}