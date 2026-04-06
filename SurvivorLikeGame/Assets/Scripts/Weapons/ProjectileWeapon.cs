using UnityEngine;

public class ProjectileWeapon : WeaponBase
{
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    public int projectileCount = 1;
    public float fireInterval { get { return attackInterval; } set { attackInterval = value; } }
    public float spreadAngle = 15f;

    protected override void Attack()
    {
        if (projectilePrefab == null) return;
        
        // Find nearest enemy
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length == 0) return;

        Transform nearest = null;
        float minDist = float.MaxValue;
        foreach (var enemy in enemies)
        {
            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy.transform;
            }
        }

        if (nearest == null) return;

        Vector2 baseDir = (nearest.position - transform.position).normalized;

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
            if (rb) rb.velocity = dir * projectileSpeed;

            // Rotate projectile to face direction
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            proj.transform.rotation = Quaternion.Euler(0, 0, angle);

            var damageDealer = proj.GetComponent<DamageDealer>();
            if (damageDealer == null) damageDealer = proj.AddComponent<DamageDealer>();
            damageDealer.damage = damage;
            damageDealer.destroyOnHit = true;
            damageDealer.isContinuous = false;

            Destroy(proj, 3f); // Cleanup after 3 seconds
        }
    }
}