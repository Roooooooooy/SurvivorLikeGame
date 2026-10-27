using UnityEngine;

// 穿透箭：向最近敌人发射高速箭矢，命中不销毁，1.5 秒后自动回收。
// 伤害判定完全交给箭矢上的 DamageDealer（isContinuous=true），
// 同一敌人由 EnemySimulation.tickDamageInterval 限频，无需修改仿真器。
public class PierceWeapon : AdvancedWeaponBase
{
    public GameObject arrowPrefab;
    public float arrowSpeed = 18f;
    public int baseArrowCount = 1;
    public float spreadAngle = 12f;
    public float arrowLifetime = 1.5f;

    protected override WeaponTarget WeaponType => WeaponTarget.Pierce;

    // 数量升级：每级多发射一支箭
    int ArrowCount => Mathf.Max(1, baseArrowCount + countUpgrades);

    protected override void Attack()
    {
        attackCount++;
        if (arrowPrefab == null) return;

        var sim = EnemySimulation.Instance;
        if (sim == null) return;
        if (!sim.FindNearest(transform.position, out Vector2 targetPos)) return;

        Vector2 origin = transform.position;
        Vector2 baseDir = (targetPos - origin).normalized;
        float area = Stats != null ? Stats.areaMultiplier : 1f;

        int count = ArrowCount;
        for (int i = 0; i < count; i++)
        {
            // 多支箭按扇形铺开（单支时沿目标方向）
            float spread = count > 1
                ? spreadAngle * ((float)i / (count - 1) - 0.5f)
                : 0f;
            Vector2 dir = Quaternion.Euler(0f, 0f, spread) * baseDir;

            var go = SmallObjectPool.Spawn(arrowPrefab, origin,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));

            var arrow = go.GetComponent<PierceProjectile>();
            if (arrow == null) arrow = go.AddComponent<PierceProjectile>();

            arrow.Init(dir, arrowSpeed, arrowLifetime, damage, Stats, area);
        }
    }
}
