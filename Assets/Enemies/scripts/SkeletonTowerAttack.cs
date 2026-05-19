using UnityEngine;

public class SkeletonTowerAttack : MonoBehaviour
{
    [Header("Ссылки")]
    public Animator skeletonAnimator;
    public GameObject bombPrefab;

    [Header("Радиус атаки")]
    public float attackRadius = 30f;

    [Header("Настройки стрельбы")]
    public float predictionTime = 2f;
    public float randomOffsetX = 1.5f;
    public float randomOffsetZ = 1.5f;
    public float bombSpawnHeight = 20f;
    public float attackCooldown = 2.5f;

    [Header("Анимация")]
    private static readonly int AttackTrigger = Animator.StringToHash("IsAttacking");

    private Transform _car;
    private Rigidbody _carRb;
    private float _cooldownTimer;
    private bool _isInRange;

    void Start()
    {
        var carGo = GameObject.FindGameObjectWithTag("Player");
        if (carGo == null)
        {
            Debug.LogWarning("[SkeletonTower] Машина не найдена — нет объекта с тегом Player");
            return;
        }

        _car   = carGo.transform;
        _carRb = carGo.GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (_car == null) return;

        float dist = Vector3.Distance(transform.position, _car.position);
        _isInRange = dist <= attackRadius;

        if (skeletonAnimator != null)
            skeletonAnimator.SetBool(AttackTrigger, _isInRange);

        if (!_isInRange) return;

        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer <= 0f)
        {
            ThrowBomb();
            _cooldownTimer = attackCooldown;
        }
    }

    private void ThrowBomb()
    {
        if (bombPrefab == null || _car == null) return;

        Vector3 carVelocity = _carRb != null ? _carRb.linearVelocity : Vector3.zero;
        Vector3 predictedPos = _car.position + carVelocity * predictionTime;

        predictedPos += new Vector3(
            Random.Range(-randomOffsetX, randomOffsetX),
            0f,
            Random.Range(-randomOffsetZ, randomOffsetZ)
        );

        Vector3 spawnPos = predictedPos + Vector3.up * bombSpawnHeight;
        Instantiate(bombPrefab, spawnPos, Quaternion.identity);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
#endif
}