using System.Collections.Generic;
using UnityEngine;

// 闪电链：向最近敌人放电，随后依次跳向附近敌人（3~5 跳）。
// 每一跳在其落点做一次小半径 DamageInCircle，不修改 EnemySimulation。
public class LightningWeapon : AdvancedWeaponBase
{
    public int baseChainCount = 3;
    public int maxChainCount = 5;
    public float chainRange = 6f;      // 首次索敌距离
    public float jumpRange = 5f;       // 后续跳跃距离
    public float impactRadius = 0.9f;  // 每跳落点的伤害半径
    public GameObject sparkPrefab;     // 落点电火花（可选）

    private static Material arcMaterial;
    private static Material ArcMaterial
    {
        get
        {
            if (arcMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader != null) arcMaterial = new Material(shader);
            }
            return arcMaterial;
        }
    }

    protected override WeaponTarget WeaponType => WeaponTarget.Lightning;

    // 跳数随等级成长（数量升级会额外提升），上限 5
    int ChainCount => Mathf.Clamp(baseChainCount + countUpgrades, 1, maxChainCount);

    // 冷却随等级小幅缩短，再叠加全局冷却缩减
    protected override float EffectiveInterval
    {
        get
        {
            float levelFactor = Mathf.Max(0.4f, 1f - 0.04f * (level - 1));
            float cooldown = Stats != null ? 1f - Stats.cooldownReduction : 1f;
            return attackInterval * levelFactor * cooldown;
        }
    }

    protected override void Attack()
    {
        attackCount++;
        var sim = EnemySimulation.Instance;
        if (sim == null) return;

        Vector2 from = transform.position;
        if (!sim.FindNearest(from, out Vector2 pos, chainRange)) return;

        float area = Stats != null ? Stats.areaMultiplier : 1f;
        float radius = impactRadius * area;
        int hops = ChainCount;

        // 记录已命中落点，避免 FindNearest 反复返回同一只敌人
        var visited = new List<Vector2>(hops);

        for (int i = 0; i < hops; i++)
        {
            int hits = sim.DamageInCircle(pos, radius, damage, false);
            lastHitCount += hits;
            if (hits > 0) CombatEvents.RaiseHit(pos, damage); // 命中事件交给表现层
            SpawnSpark(pos, area);
            DrawArc(from, pos);
            visited.Add(pos);

            if (i == hops - 1) break;
            if (!TryFindNext(sim, pos, visited, jumpRange, out Vector2 next)) break;

            from = pos;
            pos = next;
        }
    }

    // FindNearest 会把查询点处的敌人排在第一位，因此把查询点随机偏移 1.5 单位再找，
    // 并跳过已经命中过的落点；找不到新目标就提前结束链。
    static bool TryFindNext(EnemySimulation sim, Vector2 current, List<Vector2> visited,
        float maxDist, out Vector2 next)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * 1.5f;
            if (offset.sqrMagnitude < 0.2f)
                offset = offset.normalized * 0.5f;

            if (!sim.FindNearest(current + offset, out Vector2 candidate, maxDist))
                continue;

            bool seen = false;
            foreach (var v in visited)
            {
                if ((candidate - v).sqrMagnitude < 0.04f) { seen = true; break; }
            }
            if (seen) continue;

            next = candidate;
            return true;
        }

        next = default;
        return false;
    }

    void SpawnSpark(Vector2 pos, float area)
    {
        if (sparkPrefab == null) return;

        var go = SmallObjectPool.Spawn(sparkPrefab, pos, Quaternion.identity);
        var burst = go.GetComponent<SpriteBurstEffect>();
        if (burst != null) burst.Configure(1.1f * area, 0.16f);
    }

    // 用 LineRenderer 画一段短暂的电弧
    void DrawArc(Vector2 a, Vector2 b)
    {
        var mat = ArcMaterial;
        if (mat == null) return;

        var go = new GameObject("LightningArc");
        var line = go.AddComponent<LineRenderer>();
        line.material = mat;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, new Vector3(a.x, a.y, 0f));
        line.SetPosition(1, new Vector3(b.x, b.y, 0f));

        float width = 0.09f * (Stats != null ? Stats.areaMultiplier : 1f);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = new Color(0.75f, 0.92f, 1f, 0.95f);
        line.endColor = new Color(0.45f, 0.6f, 1f, 0.25f);
        line.sortingOrder = 20;

        Destroy(go, 0.12f);
    }
}
