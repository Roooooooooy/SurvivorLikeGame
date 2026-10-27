using UnityEngine;

// Boss 主线程逻辑（仿真层）：MoveJob 只负责移动与写请求标记，
// 这里同步阶段/血量到 BossStatus，并把弹幕请求变成真正的子弹（Burst 内禁止 Instantiate）。
public partial class EnemySimulation
{
    void ProcessBoss()
    {
        // 没有 Boss 时保持零开销；压测/普通波次不会进入下面的扫描
        if (bossAliveCount == 0)
        {
            // 场景里已无 Boss 但状态总线仍开启（例如 Boss 被外部移除）：静默关闭。
            // 已击杀的情况 Active 早已为 false，这里的守卫保证不会覆盖 Kill 的结果。
            if (BossStatus.Active) BossStatus.Despawn();
            return;
        }

        var prefab = table.projectilePrefab;
        bool gameOver = GameManager.Instance != null && GameManager.Instance.isGameOver;

        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e.alive == 0) continue;

            int behavior = typeBehavior[e.typeIndex];
            if (behavior != BEHAVIOR_BOSS) continue;

            var t = types[e.typeIndex];

            // 血量比例 → 阶段：66% 以下进入阶段 2、33% 以下进入阶段 3
            float ratio = t.maxHealth > 0 ? e.health / t.maxHealth : 0f;
            byte phase = ratio > t.bossPhase2Pct ? (byte)1
                       : ratio > t.bossPhase3Pct ? (byte)2
                       : (byte)3;
            int maxPhase = Mathf.Max(1, t.bossPhaseCount);
            if (phase > maxPhase) phase = (byte)maxPhase; // 资产配置少于 3 阶段时钳制

            if (e.bossPhase != phase)
            {
                e.bossPhase = phase;
                enemies[i] = e;
            }

            BossStatus.UpdateHealth(Mathf.Max(0, Mathf.CeilToInt(e.health)), phase);

            // 弹幕请求：Job 写标记，主线程清标记并生成一圈子弹
            if ((e.flags & FLAG_BARRAGE_REQUEST) == 0) continue;

            e.flags = (byte)(e.flags & ~FLAG_BARRAGE_REQUEST);
            enemies[i] = e;

            if (gameOver || prefab == null) continue;

            int count = Mathf.Max(1, t.bossBarrageCount);
            float startAngle = Random.value * Mathf.PI * 2f; // 每轮起始角随机，避免弹幕完全重合
            for (int k = 0; k < count; k++)
            {
                if (enemyProjectiles.Count >= MaxEnemyProjectiles) break;

                float angle = startAngle + k * (Mathf.PI * 2f / count);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // 从身体边缘外侧生成，避免子弹一出生就叠在 Boss 身上
                float offset = 0.5f * t.scale;
                Vector3 spawnPos = new Vector3(
                    e.position.x + dir.x * offset,
                    e.position.y + dir.y * offset,
                    0f);

                var go = SmallObjectPool.Spawn(prefab, spawnPos, Quaternion.identity);
                var proj = go.GetComponent<EnemyProjectile>();
                if (proj == null) proj = go.AddComponent<EnemyProjectile>();
                proj.Launch(dir, t.bossBarrageSpeed, t.bossBarrageDamage);
                enemyProjectiles.Add(proj);
            }
        }
    }
}
