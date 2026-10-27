using UnityEngine;

// 回旋镖：向最近敌人方向掷出，飞出一段距离后返回玩家；往返期间持续造成伤害。
public class BoomerangWeapon : AdvancedWeaponBase
{
    public GameObject boomerangPrefab;
    public int baseBoomerangCount = 1;
    public float spreadAngle = 20f;
    public float outSpeed = 11f;
    public float outTime = 0.5f;
    public float returnSpeed = 15f;
    public float maxLifetime = 3f;

    protected override WeaponTarget WeaponType => WeaponTarget.Boomerang;

    int BoomerangCount => Mathf.Max(1, baseBoomerangCount + countUpgrades);

    protected override void Attack()
    {
        attackCount++;
        if (boomerangPrefab == null) return;

        Vector2 baseDir = Vector2.right;
        var sim = EnemySimulation.Instance;
        if (sim != null && sim.FindNearest(transform.position, out Vector2 targetPos))
            baseDir = (targetPos - (Vector2)transform.position).normalized;

        float area = Stats != null ? Stats.areaMultiplier : 1f;
        int count = BoomerangCount;

        for (int i = 0; i < count; i++)
        {
            float spread = count > 1
                ? spreadAngle * ((float)i / (count - 1) - 0.5f)
                : 0f;
            Vector2 dir = Quaternion.Euler(0f, 0f, spread) * baseDir;

            var go = SmallObjectPool.Spawn(boomerangPrefab, transform.position, Quaternion.identity);
            var proj = go.GetComponent<BoomerangProjectile>();
            if (proj == null) proj = go.AddComponent<BoomerangProjectile>();

            proj.Init(dir, outSpeed, outTime, returnSpeed, maxLifetime, damage, Stats, area);
        }
    }
}
