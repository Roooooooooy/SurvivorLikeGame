using UnityEngine;

// 伤害区域：每帧对敌人仿真器做一次圆形范围查询，替代原来的物理触发器。
// isContinuous=true（法球）：持续伤害，同一敌人有命中间隔（间隔在 EnemySimulation 上配置）
// destroyOnHit=true（飞弹）：命中即销毁自身
public class DamageDealer : MonoBehaviour
{
    public int damage = 10;
    public bool isContinuous = false;
    public float damageInterval = 0.5f; // 已废弃：持续伤害间隔由 EnemySimulation.tickDamageInterval 统一控制
    public bool destroyOnHit = false;

    private CircleCollider2D col;
    private PlayerStats sourceStats;

    // 由生成它的武器注入，命中时做全局加成与暴击判定
    public void SetSourceStats(PlayerStats stats) => sourceStats = stats;

    void OnEnable()
    {
        col = GetComponent<CircleCollider2D>();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;
        var sim = EnemySimulation.Instance;
        if (sim == null) return;

        float radius = col != null
            ? col.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y)
            : 0.5f;

        int dmg = sourceStats != null ? sourceStats.RollDamage(damage) : damage;
        int hits = sim.DamageInCircle(transform.position, radius, dmg, isContinuous);

        if (hits > 0 && destroyOnHit && !isContinuous) Destroy(gameObject);
    }
}
