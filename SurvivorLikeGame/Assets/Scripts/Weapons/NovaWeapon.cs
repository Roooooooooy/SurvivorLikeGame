using UnityEngine;

// 新星爆发：以玩家为中心周期性造成大范围圆形伤害，半径受 PlayerStats.areaMultiplier 影响。
// 数量升级会额外爆发一次（围绕玩家轻微错位），伤害按次数叠加。
public class NovaWeapon : AdvancedWeaponBase
{
    public float radius = 3.5f;
    public int baseNovaCount = 1;
    public GameObject ringPrefab; // 扩散光环特效（可选）

    protected override WeaponTarget WeaponType => WeaponTarget.Nova;

    int NovaCount => Mathf.Max(1, baseNovaCount + countUpgrades);

    protected override void Attack()
    {
        attackCount++;

        var sim = EnemySimulation.Instance;
        if (sim == null) return;

        float area = Stats != null ? Stats.areaMultiplier : 1f;
        float finalRadius = radius * area;
        Vector2 center = transform.position;

        int count = NovaCount;
        for (int i = 0; i < count; i++)
        {
            // 多次爆发围绕玩家错位展开，避免完全重叠
            Vector2 burstCenter = center;
            if (i > 0)
            {
                float angle = i * Mathf.PI * 2f / count + 0.4f;
                burstCenter += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (finalRadius * 0.18f);
            }

            int hits = sim.DamageInCircle(burstCenter, finalRadius, damage, false);
            lastHitCount += hits;
            if (hits > 0) CombatEvents.RaiseHit(burstCenter, damage); // 命中事件交给表现层
            SpawnRing(burstCenter, finalRadius);
        }
    }

    void SpawnRing(Vector2 pos, float worldRadius)
    {
        if (ringPrefab == null) return;

        var go = SmallObjectPool.Spawn(ringPrefab, pos, Quaternion.identity);
        var burst = go.GetComponent<SpriteBurstEffect>();
        if (burst != null) burst.Configure(worldRadius * 2f, 0.35f);
    }
}
