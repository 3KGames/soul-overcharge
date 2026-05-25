using Common.Runtime;
using Level.Runtime;
using UnityEngine;
using VContainer;

namespace Enemies
{
    public class BossController : MonoBehaviour
    {
        [SerializeField] private float _speed           = 12f;
        [SerializeField] private float _catchDistance   = 5f;
        [SerializeField] private float _activationDelay = 5f;

        private PlayerTracker   _playerTracker;
        private GameOverService _gameOverService;

        private bool  _active;
        private float _timer;

        [Inject]
        public void Construct(PlayerTracker playerTracker, GameOverService gameOverService)
        {
            _playerTracker   = playerTracker;
            _gameOverService = gameOverService;
        }

        private void FixedUpdate()
        {
            if (_playerTracker?.PlayerTransform == null) return;

            if (!_active)
            {
                _timer += Time.fixedDeltaTime;
                if (_timer >= _activationDelay)
                {
                    _active = true;
                    Debug.Log("[BossController] Активирован!");
                }
                return;
            }

            MoveTowardPlayer();
            CheckCatch();
        }

        private void MoveTowardPlayer()
        {
            Vector3 playerPos = _playerTracker.PlayerTransform.position;
            Vector3 bossPos   = transform.position;

            // Двигаемся только по XZ
            Vector3 target    = new Vector3(playerPos.x, bossPos.y, playerPos.z);
            Vector3 direction = (target - bossPos).normalized;

            transform.position += direction * _speed * Time.fixedDeltaTime;

            // Поворачиваем к игроку
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        private void CheckCatch()
        {
            Vector3 bossPos   = transform.position;
            Vector3 playerPos = _playerTracker.PlayerTransform.position;

            float distXZ = Vector2.Distance(
                new Vector2(bossPos.x, bossPos.z),
                new Vector2(playerPos.x, playerPos.z));

            if (distXZ <= _catchDistance)
            {
                _active = false;
                Debug.Log($"[BossController] Догнал! Дистанция: {distXZ}");
                _gameOverService.TriggerGameOver();
            }
        }
    }
}