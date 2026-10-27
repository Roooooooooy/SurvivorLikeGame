using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public partial class EnemySimulation
{
    // 移动 + 远程走位/开火请求 + 自爆引线 + 朝向：全部并行，5000 只也是亚毫秒级
    [BurstCompile]
    struct MoveJob : IJobParallelFor
    {
        public NativeArray<EnemyData> enemies;
        [ReadOnly] public NativeArray<float> typeSpeed;
        [ReadOnly] public NativeArray<int> typeBehavior;
        [ReadOnly] public NativeArray<float> typeTriggerRange;
        [ReadOnly] public NativeArray<float> typeFuseTime;
        [ReadOnly] public NativeArray<float> typeAttackRange;
        [ReadOnly] public NativeArray<float> typeFireInterval;
        [ReadOnly] public NativeArray<float> typeBossChargeInterval;
        [ReadOnly] public NativeArray<float> typeBossChargeDuration;
        [ReadOnly] public NativeArray<float> typeBossChargeSpeedMultiplier;
        [ReadOnly] public NativeArray<float> typeBossBarrageInterval;
        public float2 playerPos;
        public float deltaTime;

        public void Execute(int i)
        {
            var e = enemies[i];

            // 死亡动画期：只衰减计时，不移动
            if (e.alive == 0)
            {
                if ((e.flags & FLAG_DYING) != 0)
                {
                    e.deathTimer -= deltaTime;
                    enemies[i] = e;
                }
                return;
            }

            // 击退：冲量位移 + 指数衰减
            if (math.lengthsq(e.knockback) > 0.0001f)
            {
                e.position += e.knockback * deltaTime;
                e.knockback *= math.max(0f, 1f - 8f * deltaTime);
            }

            int behavior = typeBehavior[e.typeIndex];
            float speed = typeSpeed[e.typeIndex];
            float2 toPlayer = playerPos - e.position;
            float dist = math.length(toPlayer);

            // Boss 阶段计时全部在 Job 内并行递减，主线程只按血量同步阶段。
            // 阶段 1 只逼近玩家；阶段 2/3 才会周期触发冲锋与弹幕。
            float bossSpeedMul = 1f;
            if (behavior == BEHAVIOR_BOSS)
            {
                if (e.bossPhase >= 2)
                {
                    if (e.chargeTimer > 0f)
                    {
                        // 冲锋中：剩余时间递减，移速乘倍率
                        e.chargeTimer -= deltaTime;
                        bossSpeedMul = typeBossChargeSpeedMultiplier[e.typeIndex];
                        if (e.chargeTimer <= 0f)
                            e.flags = (byte)(e.flags & ~FLAG_CHARGING);
                    }
                    else
                    {
                        if (e.chargeCooldown > 0f) e.chargeCooldown -= deltaTime;
                        if (e.chargeCooldown <= 0f)
                        {
                            e.chargeTimer = typeBossChargeDuration[e.typeIndex];
                            e.chargeCooldown = typeBossChargeInterval[e.typeIndex];
                            e.flags |= FLAG_CHARGING;
                            bossSpeedMul = typeBossChargeSpeedMultiplier[e.typeIndex];
                        }
                    }

                    // 弹幕：到点只写请求标记，Instantiate 交给主线程
                    if (e.barrageCooldown > 0f) e.barrageCooldown -= deltaTime;
                    if (e.barrageCooldown <= 0f)
                    {
                        e.flags |= FLAG_BARRAGE_REQUEST;
                        e.barrageCooldown = typeBossBarrageInterval[e.typeIndex];
                    }
                }
                else
                {
                    // 阶段 1 不积累冲锋/弹幕冷却
                    e.chargeTimer = 0f;
                    e.flags = (byte)(e.flags & ~FLAG_CHARGING);
                }
            }

            if (dist > 0.001f)
            {
                float2 dir = toPlayer / dist;

                if (behavior == BEHAVIOR_RANGED)
                {
                    // 远程：远于射程压上、近于 70% 射程后撤、射程带内站定，
                    // 目标是把距离稳在射程边缘（attackRange * 0.7 ~ attackRange）
                    float attackRange = typeAttackRange[e.typeIndex];
                    if (dist > attackRange)
                        e.position += dir * speed * deltaTime;
                    else if (dist < attackRange * 0.7f)
                        e.position -= dir * speed * deltaTime;
                }
                else if (behavior == BEHAVIOR_BOSS)
                {
                    // Boss：始终朝玩家移动，冲锋期间按倍率加速
                    e.position += dir * speed * bossSpeedMul * deltaTime;
                }
                else
                {
                    // Chase / Exploder：原行为，始终朝玩家移动
                    e.position += dir * speed * deltaTime;
                }

                if (dir.x != 0f)
                    e.flags = (byte)(dir.x < 0f ? e.flags | FLAG_FLIPX : e.flags & ~FLAG_FLIPX);
            }

            if (e.flashTimer > 0f) e.flashTimer -= deltaTime;

            if (behavior == BEHAVIOR_EXPLODER) // Exploder
            {
                if (e.fuseTimer >= 0f)
                {
                    e.fuseTimer -= deltaTime;
                    if (e.fuseTimer < 0f)
                    {
                        e.alive = 0;
                        e.flags |= FLAG_EXPLODE_PENDING; // 爆炸伤害由主线程结算
                    }
                }
                else if (dist <= typeTriggerRange[e.typeIndex])
                {
                    e.fuseTimer = typeFuseTime[e.typeIndex];
                }
            }
            else if (behavior == BEHAVIOR_RANGED)
            {
                // 冷却结束且玩家在射程内：只写请求标记，Instantiate 交给主线程
                if (e.fireCooldown > 0f) e.fireCooldown -= deltaTime;
                if (e.fireCooldown <= 0f && dist <= typeAttackRange[e.typeIndex])
                {
                    e.flags |= FLAG_FIRE_REQUEST;
                    e.fireCooldown = typeFireInterval[e.typeIndex];
                }
            }

            enemies[i] = e;
        }
    }

    // 重建空间哈希：格子 -> 敌人索引（multi hash map，一格多敌）
    [BurstCompile]
    struct BuildGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<EnemyData> enemies;
        public NativeParallelMultiHashMap<int2, int>.ParallelWriter grid;
        public float cellSize;

        public void Execute(int i)
        {
            if (enemies[i].alive == 0) return;
            int2 cell = (int2)math.floor(enemies[i].position / cellSize);
            grid.Add(cell, i);
        }
    }

    void RunSimJobs(float deltaTime)
    {
        // 注意：job 调度后、Complete 前不能触碰 enemies/grid（安全检查会抛异常），
        // 所以数量、数组视图、容量全部在调度前准备好
        int count = enemies.Length;
        if (count == 0) return;

        var enemyArray = enemies.AsArray();

        grid.Clear();
        int needed = count * 2;
        if (grid.Capacity < needed) grid.Capacity = needed;

        var moveJob = new MoveJob
        {
            enemies = enemyArray,
            typeSpeed = typeSpeed,
            typeBehavior = typeBehavior,
            typeTriggerRange = typeTriggerRange,
            typeFuseTime = typeFuseTime,
            typeAttackRange = typeAttackRange,
            typeFireInterval = typeFireInterval,
            typeBossChargeInterval = typeBossChargeInterval,
            typeBossChargeDuration = typeBossChargeDuration,
            typeBossChargeSpeedMultiplier = typeBossChargeSpeedMultiplier,
            typeBossBarrageInterval = typeBossBarrageInterval,
            playerPos = playerPosBuf[0],
            deltaTime = deltaTime,
        };
        var moveHandle = moveJob.Schedule(count, 64);

        var gridJob = new BuildGridJob
        {
            enemies = enemyArray,
            grid = grid.AsParallelWriter(),
            cellSize = cellSize,
        };
        gridJob.Schedule(count, 64, moveHandle).Complete();
    }
}
