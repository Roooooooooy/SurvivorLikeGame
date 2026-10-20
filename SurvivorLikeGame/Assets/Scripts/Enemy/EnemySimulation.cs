using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// 敌人仿真器：敌人是纯数据（NativeList），移动由 Burst Job 并行计算，
// 命中/接触检测走空间哈希，渲染用 DrawMeshInstanced 合批。
// 现有 MonoBehaviour 玩法代码通过 Spawn / DamageInCircle / FindNearest 与之交互。
public partial class EnemySimulation : MonoBehaviour
{
    public struct EnemyData
    {
        public float2 position;
        public float2 knockback;   // 击退速度，MoveJob 中衰减
        public float health;
        public int typeIndex;
        public float flashTimer;   // 受击闪白剩余时间
        public float fuseTimer;    // <0 未引燃；>=0 引燃倒计时
        public float lastTickHit;  // 持续伤害（法球）上次命中时间
        public float animPhase;    // 动画相位（出生随机，避免万怪同帧）
        public float deathTimer;   // 死亡动画倒计时（仅 FLAG_DYING 后有效）
        public byte flags;         // bit0: flipX  bit1: explodePending  bit2: dying
        public byte alive;
    }

    const byte FLAG_FLIPX = 1;
    const byte FLAG_EXPLODE_PENDING = 2;
    const byte FLAG_DYING = 4;

    [Header("打击感")]
    public float knockbackStrength = 4f; // 受击击退初速度

    public static EnemySimulation Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<EnemySimulation>();
                if (instance == null)
                    new GameObject("EnemySimulation").AddComponent<EnemySimulation>(); // Awake 同步执行
            }
            return instance;
        }
    }
    static EnemySimulation instance;

    [Header("仿真参数")]
    public float cellSize = 2f;
    public float tickDamageInterval = 0.3f; // 持续伤害对同一敌人的命中间隔
    public int maxEnemies = 10000;

    EnemyTypeTable table;
    System.Collections.Generic.List<EnemyTypeData> types;

    NativeList<EnemyData> enemies;
    NativeParallelMultiHashMap<int2, int> grid;
    NativeArray<float2> playerPosBuf;

    // 类型数据的 job 副本
    NativeArray<float> typeSpeed;
    NativeArray<float> typeScale;
    NativeArray<float> typeTriggerRange;
    NativeArray<float> typeFuseTime;
    NativeArray<int> typeBehavior;

    float maxEnemyRadius;
    bool initialized;

    public int AliveCount => initialized ? enemies.Length : 0;
    public int TypeCount => types != null ? types.Count : 0;

    void Awake()
    {
        instance = this;

        table = Resources.Load<EnemyTypeTable>("EnemyTypes");
        if (table == null)
        {
            Debug.LogError("EnemySimulation: Resources/EnemyTypes.asset 不存在");
            return;
        }
        types = table.types;
        if (types.Count == 0)
        {
            Debug.LogError("EnemySimulation: EnemyTypes 资产中没有敌人类型");
            return;
        }

        enemies = new NativeList<EnemyData>(1024, Allocator.Persistent);
        grid = new NativeParallelMultiHashMap<int2, int>(2048, Allocator.Persistent);
        playerPosBuf = new NativeArray<float2>(1, Allocator.Persistent);

        int n = types.Count;
        typeSpeed = new NativeArray<float>(n, Allocator.Persistent);
        typeScale = new NativeArray<float>(n, Allocator.Persistent);
        typeTriggerRange = new NativeArray<float>(n, Allocator.Persistent);
        typeFuseTime = new NativeArray<float>(n, Allocator.Persistent);
        typeBehavior = new NativeArray<int>(n, Allocator.Persistent);
        for (int i = 0; i < n; i++)
        {
            var t = types[i];
            typeSpeed[i] = t.moveSpeed;
            typeScale[i] = t.scale;
            typeTriggerRange[i] = t.explodeTriggerRange;
            typeFuseTime[i] = t.fuseTime;
            typeBehavior[i] = (int)t.behavior;
            maxEnemyRadius = Mathf.Max(maxEnemyRadius, 0.5f * t.scale);
        }

        InitRenderer();
        initialized = true;
    }

    void OnDestroy()
    {
        if (!initialized) return;
        enemies.Dispose();
        grid.Dispose();
        playerPosBuf.Dispose();
        typeSpeed.Dispose();
        typeScale.Dispose();
        typeTriggerRange.Dispose();
        typeFuseTime.Dispose();
        typeBehavior.Dispose();
        DisposeRenderer();
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (!initialized) return;
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        var player = PlayerController.Instance;
        if (player == null) return;
        playerPosBuf[0] = (Vector2)player.transform.position;

        // 顺序保证一致性地：先回收上一帧的死敌（压缩数组），再移动，再重建网格。
        // 武器 Update 无论何时运行，拿到的网格和数组都是同一时刻的快照。
        ProcessDeaths();
        RunSimJobs(Time.deltaTime);
        ProcessContacts(player);
    }

    // ---------- 对外 API ----------

    public void Spawn(int typeIndex, Vector2 pos)
    {
        if (!initialized) return;
        if ((uint)typeIndex >= (uint)types.Count) return;
        if (enemies.Length >= maxEnemies) return;

        enemies.Add(new EnemyData
        {
            position = pos,
            health = types[typeIndex].maxHealth,
            typeIndex = typeIndex,
            flashTimer = 0f,
            fuseTimer = -1f,
            lastTickHit = -999f,
            animPhase = UnityEngine.Random.value,
            deathTimer = -1f,
            flags = 0,
            alive = 1,
        });
    }

    // 圆形范围伤害。tick=true 表示持续伤害（同一敌人有命中间隔）。
    // 返回命中数量。伤害立即生效，死亡回收延迟到下一帧统一处理。
    public int DamageInCircle(Vector2 center, float radius, int damage, bool tick)
    {
        if (!initialized || enemies.Length == 0) return 0;

        int hits = 0;
        int2 minCell = (int2)math.floor(((float2)center - radius) / cellSize);
        int2 maxCell = (int2)math.floor(((float2)center + radius) / cellSize);

        for (int x = minCell.x; x <= maxCell.x; x++)
        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            foreach (int idx in grid.GetValuesForKey(new int2(x, y)))
            {
                var e = enemies[idx];
                if (e.alive == 0) continue;

                float rr = radius + 0.5f * typeScale[e.typeIndex];
                if (math.distancesq(e.position, (float2)center) > rr * rr) continue;

                if (tick)
                {
                    if (Time.time - e.lastTickHit < tickDamageInterval) continue;
                    e.lastTickHit = Time.time;
                }

                // 击退：沿伤害源反方向冲量，在 MoveJob 中衰减
                float2 kbDir = e.position - (float2)center;
                e.knockback += math.normalizesafe(kbDir) * knockbackStrength;

                e.health -= damage;
                e.flashTimer = 0.2f;
                if (e.health <= 0) e.alive = 0; // 死亡逻辑在 ProcessDeaths 统一结算
                enemies[idx] = e;
                hits++;
            }
        }
        return hits;
    }

    // 找最近敌人（瞄准用）。敌人数量级几千时线性扫描开销可忽略。
    public bool FindNearest(Vector2 pos, out Vector2 nearest, float maxDist = 25f)
    {
        nearest = default;
        if (!initialized) return false;

        float best = maxDist * maxDist;
        bool found = false;
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e.alive == 0) continue;
            float d = math.distancesq(e.position, (float2)pos);
            if (d < best) { best = d; nearest = e.position; found = true; }
        }
        return found;
    }

    // ---------- 内部处理 ----------

    // 敌人撞玩家：一次圆形查询，护甲/无敌帧仍在 PlayerController.TakeDamage 里
    void ProcessContacts(PlayerController player)
    {
        float2 pp = (Vector2)player.transform.position;
        float queryRadius = 0.5f + maxEnemyRadius;

        int2 minCell = (int2)math.floor((pp - queryRadius) / cellSize);
        int2 maxCell = (int2)math.floor((pp + queryRadius) / cellSize);

        for (int x = minCell.x; x <= maxCell.x; x++)
        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            foreach (int idx in grid.GetValuesForKey(new int2(x, y)))
            {
                var e = enemies[idx];
                if (e.alive == 0) continue;

                float rr = 0.5f + 0.5f * typeScale[e.typeIndex];
                if (math.distancesq(e.position, pp) > rr * rr) continue;

                var t = types[e.typeIndex];
                if (t.behavior == EnemyBehavior.Exploder) continue; // 自爆怪伤害走爆炸
                player.TakeDamage(t.damage);
            }
        }
    }

    // 死亡两阶段处理：
    // 1. 逻辑结算（掉宝石/计数/自爆伤害）→ 标记 DYING，进入死亡动画期
    // 2. 死亡动画播完（deathTimer 归零）→ swap-remove 真正回收
    void ProcessDeaths()
    {
        var player = PlayerController.Instance;

        for (int i = 0; i < enemies.Length;)
        {
            var e = enemies[i];
            if (e.alive != 0) { i++; continue; }

            if ((e.flags & FLAG_DYING) == 0)
            {
                // 阶段 1：逻辑结算
                var t = types[e.typeIndex];

                if ((e.flags & FLAG_EXPLODE_PENDING) != 0 && player != null)
                {
                    if (Vector2.Distance((Vector2)e.position, player.transform.position) <= t.explodeRadius)
                        player.TakeDamage(t.damage);
                }

                if (table.expGemPrefab != null)
                {
                    var gemObj = Instantiate(table.expGemPrefab, (Vector2)e.position, Quaternion.identity);
                    var gem = gemObj.GetComponent<ExpGem>();
                    if (gem != null) gem.expAmount = t.expValue;
                }

                if (GameManager.Instance) GameManager.Instance.AddKill();

                e.flags |= FLAG_DYING;
                e.deathTimer = t.hasDeathAnim ? t.animFrames / t.animFps : 0.15f;
                enemies[i] = e;
                i++;
            }
            else if (e.deathTimer < 0f)
            {
                // 阶段 2：动画播完，回收
                enemies.RemoveAtSwapBack(i); // 不递增 i，检查换过来的元素
            }
            else i++;
        }
    }
}
