using System.Collections;
using Car.Souls.Services;
using Common.Runtime;
using UnityEngine;
using VContainer;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Enemies
{
    [RequireComponent(typeof(Rigidbody))]
    public class SoulVOZ : MonoBehaviour, IEnemy
    {
        [Header("Movement")]
        [SerializeField] private float minMoveSpeed = 3f;
        [SerializeField] private float maxMoveSpeed = 7f;

        [Header("Soul Drain")]
        [SerializeField] private float soulsPerTick  = 5f;
        [SerializeField] private float drainInterval = 1f;
        [SerializeField] private Collider rearTrigger;

        [Header("Safety")]
        [SerializeField] private float destroyBelowY = -10f;

        private Rigidbody _rb;
        private Vector3   _moveDirection;
        private float     _moveSpeed;
        private float     _spawnY;

        private SoulService   _soulService;
        private PlayerTracker _playerTracker;

        private bool      _isDead;
        private bool      _playerInRear;
        private Coroutine _drainCoroutine;

        private bool  _isColliding;
        private float _collisionTimer;
        private const float CollisionDuration = 0.4f;

        private Collider[] _ownColliders;

        [Inject]
        public void Construct(PlayerTracker playerTracker, SoulService soulService)
        {
            _playerTracker = playerTracker;
            _soulService   = soulService;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic    = false;
            _rb.useGravity     = true;
            _rb.constraints    = RigidbodyConstraints.FreezeRotation;
            _rb.sleepThreshold = 0f;

            _moveSpeed    = Random.Range(minMoveSpeed, maxMoveSpeed);
            _ownColliders = GetComponentsInChildren<Collider>();

            int roadLayer = LayerMask.NameToLayer("Road");
            if (roadLayer >= 0)
                Physics.IgnoreLayerCollision(gameObject.layer, roadLayer, true);

            foreach (var other in FindObjectsByType<SoulVOZ>(FindObjectsSortMode.None))
            {
                if (other == this) continue;
                foreach (var ownCol in _ownColliders)
                    foreach (var otherCol in other.GetComponentsInChildren<Collider>())
                        if (!ownCol.isTrigger && !otherCol.isTrigger)
                            Physics.IgnoreCollision(ownCol, otherCol, true);
            }
        }

        private void Start()
        {
            _spawnY = transform.position.y;

            var flat = new Vector3(transform.forward.x, 0f, transform.forward.z);
            _moveDirection = flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward;
        }

        private void FixedUpdate()
        {
            if (_isDead) return;

            _rb.WakeUp();

            if (transform.position.y < _spawnY + destroyBelowY)
            {
                ForceDestroy();
                return;
            }

            if (_isColliding)
            {
                _collisionTimer -= Time.fixedDeltaTime;
                if (_collisionTimer <= 0f)
                    _isColliding = false;
                return;
            }

            if (_moveDirection.sqrMagnitude < 0.01f)
                _moveDirection = Vector3.forward;

            _rb.linearVelocity = new Vector3(
                _moveDirection.x * _moveSpeed,
                _rb.linearVelocity.y,
                _moveDirection.z * _moveSpeed
            );
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isDead) return;

            foreach (var contact in collision.contacts)
            {
                if (contact.normal.y < 0.7f)
                {
                    _isColliding    = true;
                    _collisionTimer = CollisionDuration;
                    return;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isDead) return;
            if (!other.CompareTag("Player")) return;

            _playerInRear   = true;
            _drainCoroutine ??= StartCoroutine(DrainSoulsLoop());
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRear = false;
        }

        private IEnumerator DrainSoulsLoop()
        {
            while (_playerInRear && !_isDead)
            {
                _soulService?.Spend(soulsPerTick, SoulSpendReason.AbilityCost);
                yield return new WaitForSeconds(drainInterval);
            }
            _drainCoroutine = null;
        }

        public void ForceDestroy()
        {
            if (_isDead) return;
            _isDead = true;
            StopAllCoroutines();
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (rearTrigger == null) return;
            Handles.color = new Color(0f, 0.5f, 1f, 0.3f);
            Handles.DrawWireCube(rearTrigger.bounds.center, rearTrigger.bounds.size);
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.25f);
            Gizmos.DrawCube(rearTrigger.bounds.center, rearTrigger.bounds.size);
        }
#endif
    }
}