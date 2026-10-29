using System.Collections.Generic;
using UnityEngine;

// 无限程序化障碍场：按世界网格确定性生成，围绕玩家回收/复用。
// 障碍物对玩家是静态物理碰撞体；敌人通过 EnemySimulation 读取圆形数据绕行；
// 玩家武器通过 DamageInCircle 破坏障碍。
public class ObstacleField : MonoBehaviour
{
    public static ObstacleField Instance { get; private set; }

    [Header("生成")]
    public float cellSize = 7f;
    [Range(0f, 1f)] public float density = 0.7f;
    public int seed = 4242;
    public float activeRadius = 20f;
    public float startClearRadius = 5f;   // 原点附近永久留空，避免开局被卡
    public float playerClearRadius = 3.5f; // 生成时离玩家太近则跳过
    public int maxActive = 48;

    public struct Circle
    {
        public Vector2 center;
        public float radius;
    }

    class TypeConfig
    {
        public Sprite sprite;
        public float radius;
        public int hp;
        public float weight;
    }

    readonly List<TypeConfig> configs = new();
    readonly List<Obstacle> active = new();
    readonly List<Obstacle> hitBuffer = new();
    readonly List<Circle> circles = new();
    readonly Dictionary<Vector2Int, Obstacle> cells = new();
    readonly HashSet<Vector2Int> brokenCells = new();
    readonly Stack<Obstacle> pool = new();
    readonly List<Vector2Int> releaseBuffer = new();
    readonly List<Vector2Int> brokenBuffer = new();
    Transform poolRoot;
    float totalWeight;
    int created;
    bool ready;

    public int Version { get; private set; }
    public IReadOnlyList<Circle> Circles => circles;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("ObstacleField");
        go.AddComponent<ObstacleField>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LoadConfigs();
        var root = new GameObject("ObstaclePool");
        root.transform.SetParent(transform, false);
        poolRoot = root.transform;
        ready = configs.Count > 0;
        if (!ready)
            Debug.LogWarning("[ObstacleField] Resources/Obstacles 下没有可用障碍物精灵");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LoadConfigs()
    {
        var sprites = Resources.LoadAll<Sprite>("Obstacles");
        System.Array.Sort(sprites, (a, b) =>
            string.CompareOrdinal(a.name, b.name));
        foreach (var s in sprites)
        {
            var cfg = ConfigFor(s.name);
            cfg.sprite = s;
            configs.Add(cfg);
            totalWeight += cfg.weight;
        }
    }

    static TypeConfig ConfigFor(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("pillar")) return new TypeConfig { radius = 0.72f, hp = 90, weight = 22f };
        if (n.Contains("rubble")) return new TypeConfig { radius = 0.62f, hp = 30, weight = 28f };
        if (n.Contains("brazier")) return new TypeConfig { radius = 0.48f, hp = 25, weight = 12f };
        if (n.Contains("statue")) return new TypeConfig { radius = 0.70f, hp = 140, weight = 12f };
        if (n.Contains("sarcophagus")) return new TypeConfig { radius = 0.72f, hp = 180, weight = 10f };
        if (n.Contains("crate")) return new TypeConfig { radius = 0.52f, hp = 25, weight = 10f };
        return new TypeConfig { radius = 0.6f, hp = 40, weight = 5f };
    }

    void LateUpdate()
    {
        if (!ready) return;
        var player = PlayerController.Instance;
        if (player == null) return;

        Vector2 p = player.transform.position;
        int minX = Mathf.FloorToInt((p.x - activeRadius) / cellSize);
        int maxX = Mathf.FloorToInt((p.x + activeRadius) / cellSize);
        int minY = Mathf.FloorToInt((p.y - activeRadius) / cellSize);
        int maxY = Mathf.FloorToInt((p.y + activeRadius) / cellSize);

        // 回收离开范围的格子
        releaseBuffer.Clear();
        foreach (var kv in cells)
        {
            if (kv.Key.x < minX || kv.Key.x > maxX || kv.Key.y < minY || kv.Key.y > maxY)
                releaseBuffer.Add(kv.Key);
        }
        foreach (var key in releaseBuffer) Release(key);

        // 离开范围的“已破坏”记录可以遗忘，回来时会重新生成
        brokenBuffer.Clear();
        foreach (var key in brokenCells)
        {
            if (key.x < minX || key.x > maxX || key.y < minY || key.y > maxY)
                brokenBuffer.Add(key);
        }
        foreach (var key in brokenBuffer) brokenCells.Remove(key);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var key = new Vector2Int(x, y);
                if (cells.ContainsKey(key) || brokenCells.Contains(key)) continue;
                TrySpawnCell(key, p);
            }
        }
    }

    void TrySpawnCell(Vector2Int key, Vector2 playerPos)
    {
        uint h = Hash(key.x, key.y, seed);
        if (h / 4294967295f >= density) return;

        const float margin = 1.2f;
        float ox = margin + (Hash(key.x, key.y, seed + 11) & 0xFFFF) / 65535f * (cellSize - margin * 2f);
        float oy = margin + (Hash(key.x, key.y, seed + 22) & 0xFFFF) / 65535f * (cellSize - margin * 2f);
        Vector2 pos = new Vector2(key.x * cellSize + ox, key.y * cellSize + oy);

        if (pos.sqrMagnitude < startClearRadius * startClearRadius) return;
        if ((pos - playerPos).sqrMagnitude < playerClearRadius * playerClearRadius) return;

        int typeIndex = PickType(Hash(key.x, key.y, seed + 33));
        var cfg = configs[typeIndex];

        Obstacle ob = GetPooled();
        if (ob == null) return;

        ob.transform.position = new Vector3(pos.x, pos.y, 1.5f);
        ob.gameObject.SetActive(true);
        ob.Init(this, cfg.sprite, cfg.radius, cfg.hp, key);

        cells[key] = ob;
        active.Add(ob);
        RebuildCircles();
    }

    void Release(Vector2Int key)
    {
        if (!cells.TryGetValue(key, out var ob)) return;
        cells.Remove(key);
        active.Remove(ob);
        if (ob != null)
        {
            ob.gameObject.SetActive(false);
            pool.Push(ob);
        }
        RebuildCircles();
    }

    // 由 Obstacle 在被破坏时调用
    public void NotifyBroken(Obstacle ob)
    {
        if (ob == null) return;
        Vector2Int key = ob.cell;
        cells.Remove(key);
        active.Remove(ob);
        brokenCells.Add(key);
        ob.gameObject.SetActive(false);
        pool.Push(ob);
        RebuildCircles();
    }

    void RebuildCircles()
    {
        circles.Clear();
        for (int i = 0; i < active.Count; i++)
        {
            var ob = active[i];
            if (ob == null || !ob.gameObject.activeSelf) continue;
            circles.Add(new Circle
            {
                center = ob.transform.position,
                radius = ob.radius,
            });
        }
        Version++;
    }

    // 玩家武器命中障碍：返回有效命中数量
    public int DamageInCircle(Vector2 center, float radius, int damage, bool continuous)
    {
        hitBuffer.Clear();
        for (int i = 0; i < active.Count; i++)
        {
            var ob = active[i];
            if (ob == null || !ob.gameObject.activeSelf) continue;
            float rr = radius + ob.radius;
            if (((Vector2)ob.transform.position - center).sqrMagnitude <= rr * rr)
                hitBuffer.Add(ob);
        }
        if (hitBuffer.Count == 0) return 0;

        int hits = 0;
        for (int i = 0; i < hitBuffer.Count; i++)
        {
            var ob = hitBuffer[i];
            if (ob == null || !ob.gameObject.activeSelf) continue;
            if (ob.TakeDamage(damage, continuous)) hits++;
        }
        return hits;
    }

    // 敌方子弹是否被障碍挡住
    public bool OverlapsCircle(Vector2 center, float radius)
    {
        float rr;
        for (int i = 0; i < circles.Count; i++)
        {
            rr = circles[i].radius + radius;
            if ((circles[i].center - center).sqrMagnitude <= rr * rr) return true;
        }
        return false;
    }

    int PickType(uint h)
    {
        float t = (h % 100000) / 100000f * totalWeight;
        for (int i = 0; i < configs.Count; i++)
        {
            t -= configs[i].weight;
            if (t < 0f) return i;
        }
        return configs.Count - 1;
    }

    Obstacle GetPooled()
    {
        if (pool.Count > 0) return pool.Pop();
        if (created >= maxActive) return null;
        created++;

        var go = new GameObject("Obstacle");
        go.transform.SetParent(poolRoot, false);
        go.SetActive(false);
        go.AddComponent<SpriteRenderer>();
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = false;
        go.AddComponent<Obstacle>();
        return go.GetComponent<Obstacle>();
    }

    static uint Hash(int x, int y, int s)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u +
                     (uint)s * 2246822519u + 0x9E3779B9u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return h;
        }
    }
}