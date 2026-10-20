using UnityEngine;

public class ProjectileWeapon : WeaponBase
{
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    public int projectileCount = 1;
    public float spreadAngle = 15f;
    public Color evolvedColor = new Color(0.4f, 0.8f, 1f);

    public override void Evolve(EvolutionRecipe recipe)
    {
        base.Evolve(recipe);
        projectileCount += recipe.bonusCount;
        projectileSpeed *= recipe.speedMultiplier;
    }

    protected override void Attack()
    {
        if (projectilePrefab == null) return;

        // 从敌人仿真器查最近敌人（O(1) 网格/线性 NativeArray，不再全表扫描 GameObject）
        if (!EnemySimulation.Instance.FindNearest(transform.position, out Vector2 nearestPos)) return;

        Vector2 baseDir = (nearestPos - (Vector2)transform.position).normalized;
        float area = Stats != null ? Stats.areaMultiplier : 1f;

        for (int i = 0; i < projectileCount; i++)
        {
            float currentSpread = 0;
            if (projectileCount > 1)
            {
                currentSpread = spreadAngle * ((float)i / (projectileCount - 1) - 0.5f);
            }

            Vector2 dir = Quaternion.Euler(0, 0, currentSpread) * baseDir;

            GameObject proj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
            if (rb) rb.linearVelocity = dir * projectileSpeed;

            // Rotate projectile to face direction
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            proj.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 攻击范围加成：放大飞弹（碰撞体随 transform 一起缩放）
            proj.transform.localScale = new Vector3(area, area, 1f);

            if (evolved)
            {
                var sr = proj.GetComponent<SpriteRenderer>();
                if (sr) sr.color = evolvedColor;
            }

            var damageDealer = proj.GetComponent<DamageDealer>();
            if (damageDealer == null) damageDealer = proj.AddComponent<DamageDealer>();
            damageDealer.damage = damage;
            damageDealer.destroyOnHit = true;
            damageDealer.isContinuous = false;
            damageDealer.SetSourceStats(Stats);

            Destroy(proj, 3f); // Cleanup after 3 seconds
        }
    }
}
