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
        public float fireCooldown; // Ranged 开火冷却，<=0 且玩家在射程内时 Job 写开火请求
        public byte bossPhase;     // Boss 当前阶段（1..phaseCount），非 Boss 恒为 0
        public float chargeTimer;  // Boss 冲锋剩余时间，>0 表示正在冲锋
        public float chargeCooldown; // Boss 下次冲锋冷却
        public float barrageCooldown; // Boss 下次弹幕冷却
        public byte flags;         // bit0: flipX  bit1: explodePending  bit2: dying  bit3: fireRequest
                                   // bit4: charging  bit5: barrageRequest
        public byte alive;
    }

    const byte FLAG_FLIPX = 1;
    const byte FLAG_EXPLODE_PENDING = 2;
    const byte FLAG_DYING = 4;
    const byte FLAG_FIRE_REQUEST = 8; // Burst Job 内不能 Instantiate，只写标记由主线程处理
    const byte FLAG_CHARGING = 16;    // Boss 冲锋中（供渲染/表现层读取）
    const byte FLAG_BARRAGE_REQUEST = 32; // Boss 弹幕请求，同样由主线程生成子弹

    const int BEHAVIOR_EXPLODER = 1;
    const int BEHAVIOR_RANGED = 2;
    const int BEHAVIOR_BOSS = 3;

    // 敌方子弹数量上限：远程怪很多时避免 Instantiate/Draw 无限增长
    const int MaxEnemyProjectiles = 200;

    // 金币掉落规则：Boss 必掉多枚，精英高概率掉多枚，普通怪低概率掉单枚
    const float EliteCoinHealthThreshold = 200f; // 生命值达到该值视为精英（Elite）
    const float NormalCoinDropChance = 0.2f;
    const float EliteCoinDropChance = 0.75f;

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
    NativeArray<float> typeAttackRange;
    NativeArray<float> typeFireInterval;
    NativeArray<float> typeProjectileSpeed;
    NativeArray<int> typeProjectileDamage;

    // Boss 参数的 job/主线程副本
    NativeArray<int> typeBossPhaseCount;
    NativeArray<float> typeBossPhase2Pct;
    NativeArray<float> typeBossPhase3Pct;
    NativeArray<float> typeBossChargeInterval;
    NativeArray<float> typeBossChargeDuration;
    NativeArray<float> typeBossChargeSpeedMultiplier;
    NativeArray<float> typeBossBarrageInterval;
    NativeArray<int> typeBossBarrageCount;
    NativeArray<float> typeBossBarrageSpeed;
    NativeArray<int> typeBossBarrageDamage;

    // 主线程维护的敌方子弹（只用于数量上限与清理，不进入敌人仿真数组）
    readonly System.Collections.Generic.List<EnemyProjectile> enemyProjectiles =
        new System.Collections.Generic.List<EnemyProjectile>();
    int rangedAliveCount; // 存活远程敌人数：为 0 时跳过每帧开火扫描，避免近战压测额外开销
    int bossAliveCount;   // 存活（含死亡动画期）Boss 数：为 0 时跳过每帧 Boss 扫描

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
        typeAttackRange = new NativeArray<float>(n, Allocator.Persistent);
        typeFireInterval = new NativeArray<float>(n, Allocator.Persistent);
        typeProjectileSpeed = new NativeArray<float>(n, Allocator.Persistent);
        typeProjectileDamage = new NativeArray<int>(n, Allocator.Persistent);
        typeBossPhaseCount = new NativeArray<int>(n, Allocator.Persistent);
        typeBossPhase2Pct = new NativeArray<float>(n, Allocator.Persistent);
        typeBossPhase3Pct = new NativeArray<float>(n, Allocator.Persistent);
        typeBossChargeInterval = new NativeArray<float>(n, Allocator.Persistent);
        typeBossChargeDuration = new NativeArray<float>(n, Allocator.Persistent);
        typeBossChargeSpeedMultiplier = new NativeArray<float>(n, Allocator.Persistent);
        typeBossBarrageInterval = new NativeArray<float>(n, Allocator.Persistent);
        typeBossBarrageCount = new NativeArray<int>(n, Allocator.Persistent);
        typeBossBarrageSpeed = new NativeArray<float>(n, Allocator.Persistent);
        typeBossBarrageDamage = new NativeArray<int>(n, Allocator.Persistent);
        for (int i = 0; i < n; i++)
        {
            var t = types[i];
            typeSpeed[i] = t.moveSpeed;
            typeScale[i] = t.scale;
            typeTriggerRange[i] = t.explodeTriggerRange;
            typeFuseTime[i] = t.fuseTime;
            typeBehavior[i] = (int)t.behavior;
            typeAttackRange[i] = Mathf.Max(0.1f, t.attackRange);
            typeFireInterval[i] = Mathf.Max(0.1f, t.fireInterval); // 防止 0 间隔导致每帧请求
            typeProjectileSpeed[i] = t.projectileSpeed;
            typeProjectileDamage[i] = t.projectileDamage;
            // Boss 参数做下限保护：0 间隔会导致每帧请求，0 阶段数会让 UI 阶段失效
            typeBossPhaseCount[i] = Mathf.Max(1, t.bossPhaseCount);
            typeBossPhase2Pct[i] = t.bossPhase2Pct;
            typeBossPhase3Pct[i] = t.bossPhase3Pct;
            typeBossChargeInterval[i] = Mathf.Max(0.1f, t.bossChargeInterval);
            typeBossChargeDuration[i] = Mathf.Max(0.05f, t.bossChargeDuration);
            typeBossChargeSpeedMultiplier[i] = Mathf.Max(1f, t.bossChargeSpeedMultiplier);
            typeBossBarrageInterval[i] = Mathf.Max(0.1f, t.bossBarrageInterval);
            typeBossBarrageCount[i] = Mathf.Max(1, t.bossBarrageCount);
            typeBossBarrageSpeed[i] = Mathf.Max(0.1f, t.bossBarrageSpeed);
            typeBossBarrageDamage[i] = Mathf.Max(1, t.bossBarrageDamage);
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
        typeAttackRange.Dispose();
        typeFireInterval.Dispose();
        typeProjectileSpeed.Dispose();
        typeProjectileDamage.Dispose();
        typeBossPhaseCount.Dispose();
        typeBossPhase2Pct.Dispose();
        typeBossPhase3Pct.Dispose();
        typeBossChargeInterval.Dispose();
        typeBossChargeDuration.Dispose();
        typeBossChargeSpeedMultiplier.Dispose();
        typeBossBarrageInterval.Dispose();
        typeBossBarrageCount.Dispose();
        typeBossBarrageSpeed.Dispose();
        typeBossBarrageDamage.Dispose();
        enemyProjectiles.Clear();
        DisposeRenderer();
        // 场景卸载/退出 Play：静默关闭 Boss 状态（不触发击杀奖励）
        BossStatus.Despawn();
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (!initialized) return;
        // 注意：死亡演出期间（isGameOver）仿真继续运行——怪群继续涌动，黑魂味更足

        var player = PlayerController.Instance;
        if (player == null) return;
        playerPosBuf[0] = (Vector2)player.transform.position;

        // 顺序保证一致性地：先回收上一帧的死敌（压缩数组），再移动，再重建网格。
        // 武器 Update 无论何时运行，拿到的网格和数组都是同一时刻的快照。
        ProcessDeaths();
        RunSimJobs(Time.deltaTime);
        ProcessRangedFire(player);
        ProcessBoss();

        // 玩家已死则不再结算接触伤害
        if (GameManager.Instance == null || !GameManager.Instance.isGameOver)
            ProcessContacts(player);
    }

    // ---------- 对外 API ----------

    public void Spawn(int typeIndex, Vector2 pos)
    {
        if (!initialized) return;
        if ((uint)typeIndex >= (uint)types.Count) return;
        if (enemies.Length >= maxEnemies) return;

        var type = types[typeIndex];
        bool isBoss = type.behavior == EnemyBehavior.Boss;

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
            // Ranged 出生冷却随机错开，避免整群同一帧齐射
            fireCooldown = type.behavior == EnemyBehavior.Ranged
                ? UnityEngine.Random.value * Mathf.Max(0.1f, type.fireInterval)
                : 0f,
            bossPhase = isBoss ? (byte)1 : (byte)0,
            chargeTimer = 0f,
            // 首次冲锋/弹幕都从完整间隔开始，进入阶段 2 后不会立刻触发
            chargeCooldown = isBoss ? Mathf.Max(0.1f, type.bossChargeInterval) : 0f,
            barrageCooldown = isBoss ? Mathf.Max(0.1f, type.bossBarrageInterval) : 0f,
            flags = 0,
            alive = 1,
        });

        if (type.behavior == EnemyBehavior.Ranged) rangedAliveCount++;
        if (isBoss)
        {
            bossAliveCount++;
            // 同步 Boss 状态总线，供血条 UI / 奖励系统读取
            BossStatus.Spawn(type.typeName, type.maxHealth, Mathf.Max(1, type.bossPhaseCount));
        }
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

    // Ranged 开火：MoveJob 只写 FLAG_FIRE_REQUEST（Burst 内禁止 Instantiate），
    // 主线程在这里生成子弹并清标记。即使因上限/缺预制体无法生成也要清标记，
    // 否则限制解除后会一次齐射所有积压请求。
    void ProcessRangedFire(PlayerController player)
    {
        // 清理已回收的子弹引用（命中/寿命到期由 EnemyProjectile 归还对象池，对象仍在但已失活）
        for (int i = enemyProjectiles.Count - 1; i >= 0; i--)
        {
            var p = enemyProjectiles[i];
            if (p == null || !p.gameObject.activeSelf) enemyProjectiles.RemoveAt(i);
        }

        if (rangedAliveCount == 0) return;

        var prefab = table.projectilePrefab;
        bool gameOver = GameManager.Instance != null && GameManager.Instance.isGameOver;
        float2 pp = (Vector2)player.transform.position;

        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if ((e.flags & FLAG_FIRE_REQUEST) == 0) continue;

            e.flags = (byte)(e.flags & ~FLAG_FIRE_REQUEST);
            enemies[i] = e;

            if (gameOver || prefab == null || enemyProjectiles.Count >= MaxEnemyProjectiles) continue;

            float2 toPlayer = pp - e.position;
            float dist = math.length(toPlayer);
            if (dist < 0.01f) continue;
            float2 dir = toPlayer / dist;

            // 从身体前方一点生成，避免子弹一出生就叠在敌人身上
            Vector3 spawnPos = new Vector3(e.position.x + dir.x * 0.5f, e.position.y + dir.y * 0.5f, 0f);
            var go = SmallObjectPool.Spawn(prefab, spawnPos, Quaternion.identity);
            var proj = go.GetComponent<EnemyProjectile>();
            if (proj == null) proj = go.AddComponent<EnemyProjectile>();
            proj.Launch(new Vector2(dir.x, dir.y), typeProjectileSpeed[e.typeIndex], typeProjectileDamage[e.typeIndex]);
            enemyProjectiles.Add(proj);
        }
    }

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

    // 金币掉落：按敌人档次决定数量/面值。生成位置沿用对象池，与经验宝石同一套回收路径。
    void DropCoins(Vector2 pos, EnemyTypeData t)
    {
        var prefab = table.coinPrefab;
        if (prefab == null) return;

        int count;
        int value;
        if (t.behavior == EnemyBehavior.Boss)
        {
            count = UnityEngine.Random.Range(6, 9); // Boss 必掉多枚
            value = 3;
        }
        else if (t.maxHealth >= EliteCoinHealthThreshold)
        {
            if (UnityEngine.Random.value > EliteCoinDropChance) return;
            count = UnityEngine.Random.Range(2, 5);
            value = 2;
        }
        else
        {
            if (UnityEngine.Random.value > NormalCoinDropChance) return;
            count = 1;
            value = 1;
        }

        for (int i = 0; i < count; i++)
        {
            // 多枚金币沿小圆环散开，避免完全重叠成一枚
            float angle = count > 1 ? i * Mathf.PI * 2f / count : 0f;
            Vector3 offset = count > 1
                ? new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.3f
                : Vector3.zero;
            var coinObj = SmallObjectPool.Spawn(prefab, (Vector3)pos + offset, Quaternion.identity);
            if (coinObj == null) continue;
            var coin = coinObj.GetComponent<CoinPickup>();
            if (coin != null) coin.value = value;
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

                // Boss 击杀：BossStatus.Kill 内部有 Active 守卫，只会触发一次 BossKilled
                if (t.behavior == EnemyBehavior.Boss) BossStatus.Kill();

                if ((e.flags & FLAG_EXPLODE_PENDING) != 0 && player != null)
                {
                    if (Vector2.Distance((Vector2)e.position, player.transform.position) <= t.explodeRadius)
                        player.TakeDamage(t.damage);
                }

                if (table.expGemPrefab != null)
                {
                    // 经验宝石走对象池：击杀密集时避免大量 Instantiate
                    var gemObj = SmallObjectPool.Spawn(table.expGemPrefab, (Vector3)(Vector2)e.position, Quaternion.identity);
                    var gem = gemObj.GetComponent<ExpGem>();
                    if (gem != null) gem.expAmount = t.expValue;
                }

                DropCoins((Vector2)e.position, t);

                // 击杀事件交给表现层（音效等）；与宝石掉落一样每只敌人只结算一次
                CombatEvents.RaiseKill((Vector2)e.position, t.expValue);

                if (GameManager.Instance) GameManager.Instance.AddKill();

                e.flags |= FLAG_DYING;
                e.deathTimer = t.hasDeathAnim ? t.animFrames / t.animFps : 0.15f;
                enemies[i] = e;
                i++;
            }
            else if (e.deathTimer < 0f)
            {
                // 阶段 2：动画播完，回收
                if (types[e.typeIndex].behavior == EnemyBehavior.Ranged) rangedAliveCount--;
                if (types[e.typeIndex].behavior == EnemyBehavior.Boss) bossAliveCount--;
                enemies.RemoveAtSwapBack(i); // 不递增 i，检查换过来的元素
            }
            else i++;
        }
    }
}
