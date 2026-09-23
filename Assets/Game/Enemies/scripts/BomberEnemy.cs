using System;
using System.Collections;
using Car.Health;
using Common.Runtime;
using UnityEngine;
using VContainer;

namespace Enemies
{
    [RequireComponent(typeof(Rigidbody))]
    public class BomberEnemy : MonoBehaviour
    {
        private static readonly int ExplodeTrigger  = Animator.StringToHash("Explode");
        private static readonly int ExplodeStateHash = Animator.StringToHash("Boomber_explode");

        [SerializeField] private float activationRadius = 25f;
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float explosionDamage = 30f;
        [SerializeField] private Collider physicsCollider;
        [SerializeField] private Collider detectionTrigger;
		[SerializeField] private Animator  _animator;

        private Rigidbody _rb;
	
        private Transform _player;
        private bool      _activated;
        private bool      _exploded;
        private Vector3   _moveDirection;

        private PlayerTracker _playerTracker;
		
		public Action OnExplode;

        [Inject]
        public void Construct(PlayerTracker playerTracker)
        {
            _playerTracker = playerTracker;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            _rb.isKinematic = false;
            _rb.useGravity  = true;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void Start()
        {
            if (_playerTracker != null && _playerTracker.PlayerTransform != null)
                _player = _playerTracker.PlayerTransform;
            else
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) _player = go.transform;
            }

            if (_player == null)
            {
                Debug.LogWarning("[BomberEnemy] Игрок не найден!", this);
                return;
            }

            var playerColliders = _player.GetComponents<Collider>();
            foreach (var col in playerColliders)
                Physics.IgnoreCollision(physicsCollider, col, true);
        }

        private void FixedUpdate()
        {
            if (_exploded) return;

            if (!_activated)
            {
                TryActivate();
                return;
            }

            MoveForward();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_exploded) return;
            if (other.CompareTag("Player"))
                StartCoroutine(ExplodeRoutine(other.gameObject));
        }

        public void ForceDestroy()
        {
            if (_exploded) return;
            StopAllCoroutines();
            Destroy(gameObject);
        }

        private void TryActivate()
        {
            if (_player == null) return;
            if (Vector3.Distance(transform.position, _player.position) > activationRadius) return;

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y     = 0f;
            _moveDirection = toPlayer.normalized;

            _activated = true;
        }

        private void MoveForward()
        {
            _rb.linearVelocity = new Vector3(
                _moveDirection.x * moveSpeed,
                _rb.linearVelocity.y,
                _moveDirection.z * moveSpeed
            );
        }

        private IEnumerator ExplodeRoutine(GameObject playerGO)
        {
			OnExplode?.Invoke();
            _exploded = true;

            _rb.linearVelocity = Vector3.zero;
            _rb.isKinematic    = true;

            _animator.SetTrigger(ExplodeTrigger);

            playerGO.GetComponent<CarHealthBridge>()?.TakeDamage(explosionDamage);

            yield return null;

            while (true)
            {
                var state = _animator.GetCurrentAnimatorStateInfo(0);
                if (state.shortNameHash == ExplodeStateHash && state.normalizedTime >= 1f)
                    break;
                yield return null;
            }

            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.92f, 0.02f, 0.15f);
            Gizmos.DrawSphere(transform.position, activationRadius);
            Gizmos.color = new Color(1f, 0.92f, 0.02f, 1f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);

            if (Application.isPlaying && _activated)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position, _moveDirection * 4f);
            }
        }
#endif
    }
}