using Common.Runtime;
using UnityEngine;
using VContainer.Unity;

namespace Game.Enemies.Audio
{
    /// <summary>
    /// VContainer ITickable: каждый кадр включает FMOD только для N ближайших к игроку врагов.
    /// Ноль аллокаций (insertion sort по фиксированному буферу).
    /// </summary>
    public sealed class EnemyAudioLodManager : ITickable
    {
        private readonly PlayerTracker _playerTracker;
        private readonly int _maxActiveVoices;
        private readonly float _maxAudibleDistanceSqr;

        private readonly EnemyAudioFmodAdapter[] _adapters = new EnemyAudioFmodAdapter[128];
        private int _count;

        public EnemyAudioLodManager(
            PlayerTracker playerTracker,
            int maxActiveVoices = 8,
            float maxAudibleDistance = 120f)
        {
            _playerTracker = playerTracker;
            _maxActiveVoices = Mathf.Max(1, maxActiveVoices);
            _maxAudibleDistanceSqr = maxAudibleDistance * maxAudibleDistance;
        }

        public void Register(EnemyAudioFmodAdapter adapter)
        {
            if (adapter == null || _count >= _adapters.Length) return;
            _adapters[_count++] = adapter;
        }

        public void Unregister(EnemyAudioFmodAdapter adapter)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_adapters[i] == adapter)
                {
                    _adapters[i] = _adapters[--_count];
                    _adapters[_count] = null;
                    return;
                }
            }
        }

        public void Tick()
        {
            if (_count == 0 || _playerTracker == null || _playerTracker.PlayerTransform == null)
                return;

            Vector3 listenerPos = _playerTracker.PlayerTransform.position;

            // Insertion sort по квадрату дистанции — быстро на почти отсортированных массивах, 0 аллокаций
            for (int i = 1; i < _count; i++)
            {
                var current = _adapters[i];
                float currentD2 = (current.transform.position - listenerPos).sqrMagnitude;
                int j = i - 1;
                while (j >= 0 && (_adapters[j].transform.position - listenerPos).sqrMagnitude > currentD2)
                {
                    _adapters[j + 1] = _adapters[j];
                    j--;
                }
                _adapters[j + 1] = current;
            }

            for (int i = 0; i < _count; i++)
            {
                var adapter = _adapters[i];
                float d2 = (adapter.transform.position - listenerPos).sqrMagnitude;
                bool shouldPlay = i < _maxActiveVoices && d2 <= _maxAudibleDistanceSqr;
                adapter.SetPaused(!shouldPlay);
            }
        }
    }
}