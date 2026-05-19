using UnityEngine;
using Car.Health;

public class BombProjectile : MonoBehaviour
{
    [Header("Настройки")]
    public float fallSpeed       = 15f;
    public float damage          = 20f;
    public float explosionRadius = 2f;
    public float autoDestroyTime = 5f;
    public LayerMask carLayer;
    public LayerMask groundLayer;

    [Header("Индикатор на земле")]
    public GameObject indicatorPrefab;
    public float indicatorOffsetY = 0.05f;


    private bool       _exploded;
    private GameObject _indicator;
    private float      _timer;
    private Vector3    _groundHitPoint;
    private bool       _hasGroundPoint;

    void Start()
    {
        _timer = autoDestroyTime;

        if (indicatorPrefab != null)
            _indicator = Instantiate(indicatorPrefab);

        UpdateIndicator();
    }

    void Update()
    {
        if (_exploded) return;

        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        UpdateIndicator();

        if (_hasGroundPoint)
        {
            float distToGround = transform.position.y - _groundHitPoint.y;
            if (distToGround <= 0.2f)
                Explode();
        }

        _timer -= Time.deltaTime;
        if (_timer <= 0f) SelfDestruct();
    }

    private void UpdateIndicator()
    {
        if (_indicator == null) return;

        Vector3 origin = transform.position + Vector3.down * 0.3f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, groundLayer))
        {
            _groundHitPoint = hit.point;
            _hasGroundPoint = true;

            _indicator.transform.position   = hit.point + Vector3.up * indicatorOffsetY;
            _indicator.transform.rotation   = Quaternion.FromToRotation(Vector3.up, hit.normal);
            float scale                     = explosionRadius * 2f;
            _indicator.transform.localScale = new Vector3(scale, scale, scale);
        }
    }

    private void Explode()
    {
        _exploded = true;
        Vector3 explosionCenter = _hasGroundPoint ? _groundHitPoint : transform.position;
        Debug.Log($"[Bomb] Explode в точке {explosionCenter}");

        Collider[] allHits = Physics.OverlapSphere(explosionCenter, explosionRadius);
        Debug.Log($"[Bomb] OverlapSphere нашёл {allHits.Length} коллайдеров без маски");
        foreach (var h in allHits)
            Debug.Log($"[Bomb]   → {h.gameObject.name} | Layer: {LayerMask.LayerToName(h.gameObject.layer)}");

        Collider[] hits = Physics.OverlapSphere(explosionCenter, explosionRadius, carLayer);
        Debug.Log($"[Bomb] OverlapSphere с carLayer нашёл {hits.Length} коллайдеров");
        foreach (var hit in hits)
        {
            var bridge = hit.GetComponent<CarHealthBridge>();
            Debug.Log($"[Bomb]   → {hit.gameObject.name} | CarHealthBridge: {bridge != null}");
            bridge?.TakeDamage(damage);
        }

        DestroyIndicator();
        Destroy(gameObject);
    }

    private void SelfDestruct()
    {
        _exploded = true;
        DestroyIndicator();
        Destroy(gameObject);
    }

    private void DestroyIndicator()
    {
        if (_indicator != null)
            Destroy(_indicator);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        if (_hasGroundPoint)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(_groundHitPoint, explosionRadius);
        }
    }
#endif
}