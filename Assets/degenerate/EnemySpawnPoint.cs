using NaughtyAttributes;
using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [Tooltip("Какие типы врагов могут спавниться здесь. Пусто = любые.")]
    public EnemyTag[] allowedEnemyTags;

	public bool spawnOnTrigger = false;

	private bool _isArmed = false;
	private GameObject _armedPrefab;
	private TrackEnemySpawner _parentSpawner;
	
	private void Reset()
	{
		var col = GetComponent<BoxCollider>();
		col.isTrigger = true;
	}

    public bool AllowsEnemy(EnemyTag enemyTag)
    {
        if (allowedEnemyTags == null || allowedEnemyTags.Length == 0) return true;
        return System.Array.IndexOf(allowedEnemyTags, enemyTag) >= 0;
    }
	
	public void ArmForAmbush(GameObject prefab, TrackEnemySpawner spawner)
	{
		_armedPrefab = prefab;
		_parentSpawner = spawner;
		_isArmed = true;
	}

	public void Disarm()
	{
		_isArmed = false;
		_armedPrefab = null;
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!_isArmed || !other.CompareTag("Player")) return;

		_isArmed = false;

		_parentSpawner.SpawnFromTrigger(this, _armedPrefab);
	}

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = (allowedEnemyTags != null && allowedEnemyTags.Length > 0)
            ? Color.yellow
            : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawIcon(transform.position, "sv_icon_dot4_pix16_gizmo", true);
    }
#endif
}