using UnityEngine;
using System.Collections.Generic;

public class DamageDealer : MonoBehaviour
{
    public int damage = 10;
    public bool isContinuous = false;
    public float damageInterval = 0.5f;
    public bool destroyOnHit = false;

    private PlayerStats sourceStats;
    private Dictionary<Collider2D, float> hitTimers = new Dictionary<Collider2D, float>();

    // 由生成它的武器注入，命中时做全局加成与暴击判定
    public void SetSourceStats(PlayerStats stats) => sourceStats = stats;

    void Update()
    {
        if (!isContinuous) return;

        // Clean up or update timers
        List<Collider2D> keys = new List<Collider2D>(hitTimers.Keys);
        foreach (var key in keys)
        {
            if (key == null) hitTimers.Remove(key);
            else if (hitTimers[key] > 0) hitTimers[key] -= Time.deltaTime;
        }
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        TryDealDamage(col);
    }

    void OnTriggerStay2D(Collider2D col)
    {
        if (isContinuous) TryDealDamage(col);
    }

    void TryDealDamage(Collider2D col)
    {
        if (!col.CompareTag("Enemy")) return;

        if (isContinuous)
        {
            if (hitTimers.ContainsKey(col) && hitTimers[col] > 0) return;
            hitTimers[col] = damageInterval;
        }

        var enemy = col.GetComponent<EnemyController>();
        if (enemy != null)
        {
            int finalDamage = sourceStats != null ? sourceStats.RollDamage(damage) : damage;
            enemy.TakeDamage(finalDamage);
            if (destroyOnHit && !isContinuous) Destroy(gameObject);
        }
    }
}
