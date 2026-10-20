using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public partial class EnemySimulation
{
    // 移动 + 自爆引线 + 朝向：全部并行，5000 只也是亚毫秒级
    [BurstCompile]
    struct MoveJob : IJobParallelFor
    {
        public NativeArray<EnemyData> enemies;
        [ReadOnly] public NativeArray<float> typeSpeed;
        [ReadOnly] public NativeArray<int> typeBehavior;
        [ReadOnly] public NativeArray<float> typeTriggerRange;
        [ReadOnly] public NativeArray<float> typeFuseTime;
        public float2 playerPos;
        public float deltaTime;

        public void Execute(int i)
        {
            var e = enemies[i];
            if (e.alive == 0) return;

            float2 toPlayer = playerPos - e.position;
            float dist = math.length(toPlayer);
            if (dist > 0.001f)
            {
                float2 dir = toPlayer / dist;
                e.position += dir * typeSpeed[e.typeIndex] * deltaTime;
                if (dir.x != 0f)
                    e.flags = (byte)(dir.x < 0f ? e.flags | FLAG_FLIPX : e.flags & ~FLAG_FLIPX);
            }

            if (e.flashTimer > 0f) e.flashTimer -= deltaTime;

            if (typeBehavior[e.typeIndex] == 1) // Exploder
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
