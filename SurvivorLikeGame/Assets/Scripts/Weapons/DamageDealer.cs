using UnityEngine;

// 伤害区域：每帧对敌人仿真器做一次圆形范围查询，替代原来的物理触发器。
// isContinuous=true（法球）：持续伤害，同一敌人有命中间隔（间隔在 EnemySimulation 上配置）
// destroyOnHit=true（飞弹）：命中即回收自身（池化对象归还对象池，非池化对象退回 Destroy）
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

        // 暴击标记随伤害一起带给表现层（伤害数字/顿帧/震屏），不改变伤害数值与概率
        bool crit = false;
        int dmg = sourceStats != null ? sourceStats.RollDamage(damage, out crit) : damage;
        int hits = sim.DamageInCircle(transform.position, radius, dmg, isContinuous);

        if (hits > 0)
        {
            // 命中事件交给表现层（音效/伤害数字/震屏）订阅
            CombatEvents.RaiseHit(transform.position, dmg, crit);

            // 池化飞弹归还对象池；未池化对象由 Release 内部退回 Destroy，保持旧行为
            if (destroyOnHit && !isContinuous) SmallObjectPool.Release(gameObject);
        }
    }
}
