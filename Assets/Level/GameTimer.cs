using System;
using UnityEngine;
using VContainer.Unity;

namespace Level.Runtime
{
    public class GameTimer : IInitializable, IDisposable, ITickable
    {
        public float ElapsedSeconds { get; private set; }
        public bool  IsRunning      { get; private set; }

        public string FormattedTime
        {
            get
            {
                int minutes = (int)(ElapsedSeconds / 60f);
                int seconds = (int)(ElapsedSeconds % 60f);
                int millis  = (int)((ElapsedSeconds * 100f) % 100f);
                return $"{minutes:00}:{seconds:00}.{millis:00}";
            }
        }

        public event Action<float> Tick;

        public void Initialize()
        {
            ElapsedSeconds = 0f;
            IsRunning      = true;
            Debug.Log("[GameTimer] Запущен");
        }

        public void Dispose()
        {
            IsRunning = false;
            Debug.Log($"[GameTimer] Остановлен | Итого: {FormattedTime}");
        }

        void ITickable.Tick()
        {
            if (!IsRunning) return;
            ElapsedSeconds += Time.deltaTime;
            Tick?.Invoke(ElapsedSeconds);
        }

        public void Pause()  => IsRunning = false;
        public void Resume() => IsRunning = true;
        public void Reset()  => ElapsedSeconds = 0f;
    }
}
