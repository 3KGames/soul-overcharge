using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [Tooltip("Какие типы врагов могут спавниться здесь. Пусто = любые.")]
    public string[] allowedEnemyTags;

    public bool AllowsEnemy(string enemyTag)
    {
        if (allowedEnemyTags == null || allowedEnemyTags.Length == 0) return true;
        return System.Array.IndexOf(allowedEnemyTags, enemyTag) >= 0;
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