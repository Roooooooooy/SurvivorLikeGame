using System.Collections.Generic;
using UnityEngine;

// 轻量对象池：按 prefab 分池，覆盖飞弹、敌方子弹、经验宝石、武器特效这类高频短命对象，
// 避免 5000 敌人压测时反复 Instantiate/Destroy 带来的 GC 与物理体重建开销。
// 池根节点懒创建、随场景销毁；静态引用在场景重载（RestartGame 的 LoadScene）后用
// Unity 对象 == null 判空安全重建，不会指向已销毁对象。
public static class SmallObjectPool
{
    // 每个 prefab 一条空闲栈。键是预制体资产，场景重载后依然有效。
    static readonly Dictionary<GameObject, Stack<GameObject>> pools =
        new Dictionary<GameObject, Stack<GameObject>>();

    static GameObject poolRoot; // 失活池对象统一挂载的根节点

    // 每个 prefab 的生成/复用计数（仅诊断用，不参与池逻辑；跨场景重载累计保留）
    static readonly Dictionary<GameObject, int> createdCounts = new Dictionary<GameObject, int>();
    static readonly Dictionary<GameObject, int> reusedCounts = new Dictionary<GameObject, int>();

    // 运行期统计：Play 模式 Console 里用于确认复用生效
    public static int TotalCreated { get; private set; }
    public static int TotalReused { get; private set; }
    public static int TotalReleased { get; private set; }
    public static int TotalSpawned => TotalCreated + TotalReused;
    public static int TotalActive => TotalCreated + TotalReused - TotalReleased;

#if UNITY_EDITOR
    static float lastStatsLogTime;
    static int lastStatsLogSpawned;
#endif

    // 关闭域重载时静态字段会跨 Play 保留，这里统一回到干净状态
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pools.Clear();
        createdCounts.Clear();
        reusedCounts.Clear();
        poolRoot = null;
        TotalCreated = 0;
        TotalReused = 0;
        TotalReleased = 0;
    }

    static GameObject PoolRoot
    {
        get
        {
            // Unity 对象被销毁后 == null（C# 引用仍在）：说明场景已重载，重建池根并丢弃旧条目
            if (poolRoot == null)
            {
                pools.Clear(); // 旧池对象已随场景销毁，清空避免继续持有失效引用
                // created/reused 是运行期累计统计，跨场景重载保留，便于观察整局复用率
                poolRoot = new GameObject("[SmallObjectPool]");
            }
            return poolRoot;
        }
    }

    // 生成：优先复用同 prefab 的空闲对象，池空时才 Instantiate。
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        _ = PoolRoot; // 先确保池根有效（可能刚经历场景重载）

        if (!pools.TryGetValue(prefab, out var free))
        {
            free = new Stack<GameObject>();
            pools[prefab] = free;
        }

        GameObject go = null;
        while (free.Count > 0 && go == null)
        {
            var candidate = free.Pop();
            if (candidate != null) go = candidate; // 被外部销毁的池对象直接丢弃
        }

        if (go == null)
        {
            go = Object.Instantiate(prefab);
            TotalCreated++;
            Bump(createdCounts, prefab);
        }
        else
        {
            TotalReused++;
            Bump(reusedCounts, prefab);
        }

        var marker = go.GetComponent<PooledObject>();
        if (marker == null) marker = go.AddComponent<PooledObject>();
        marker.Bind(prefab);
        marker.InPool = false;

        // 先脱离池根再激活，避免在池根下做一帧物理/触发回调
        go.transform.SetParent(null, false);
        go.transform.SetPositionAndRotation(position, rotation);
        ResetPhysics(go);
        go.SetActive(true);

#if UNITY_EDITOR
        LogStatsIfDue();
#endif
        return go;
    }

    // 回收：失活并挂到池根下。非池化对象保持原来的 Destroy 语义。
    public static void Release(GameObject go)
    {
        if (go == null) return;

        var marker = go.GetComponent<PooledObject>();
        if (marker == null || marker.SourcePrefab == null)
        {
            Object.Destroy(go);
            return;
        }
        if (marker.InPool) return; // 重复归还保护

        marker.InPool = true;
        ResetPhysics(go);
        go.SetActive(false);
        go.transform.SetParent(PoolRoot.transform, false); // 池根失效时会先重建

        if (!pools.TryGetValue(marker.SourcePrefab, out var free))
        {
            free = new Stack<GameObject>();
            pools[marker.SourcePrefab] = free;
        }
        free.Push(go);
        TotalReleased++;
    }

    // 回收时清掉速度，避免下次复用从上一轮的速度继续漂移
    static void ResetPhysics(GameObject go)
    {
        if (go.TryGetComponent(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public static void LogStats()
    {
        Debug.Log(BuildStatsText());
    }

    // 生成统计文本：总量 + 每种 prefab 的 created/reused/free（仅诊断调用，不参与热路径）
    public static string BuildStatsText()
    {
        var sb = new System.Text.StringBuilder(256);
        sb.Append("[SmallObjectPool] spawned=").Append(TotalSpawned)
          .Append(" created=").Append(TotalCreated)
          .Append(" reused=").Append(TotalReused)
          .Append(" released=").Append(TotalReleased)
          .Append(" active=").Append(TotalActive)
          .Append(" prefabKinds=").Append(pools.Count);

        foreach (var pair in pools)
        {
            var prefab = pair.Key;
            createdCounts.TryGetValue(prefab, out int created);
            reusedCounts.TryGetValue(prefab, out int reused);
            sb.Append('\n').Append("  ").Append(prefab != null ? prefab.name : "<missing>")
              .Append(": created=").Append(created)
              .Append(" reused=").Append(reused)
              .Append(" free=").Append(pair.Value.Count);
        }
        return sb.ToString();
    }

    static void Bump(Dictionary<GameObject, int> counts, GameObject prefab)
    {
        counts.TryGetValue(prefab, out int n);
        counts[prefab] = n + 1;
    }

#if UNITY_EDITOR
    // 编辑器内每 10 秒输出一次统计，便于 Play 模式验证复用；构建版不产生日志开销
    static void LogStatsIfDue()
    {
        if (Time.unscaledTime - lastStatsLogTime < 10f) return;
        if (TotalSpawned == lastStatsLogSpawned) return;
        lastStatsLogTime = Time.unscaledTime;
        lastStatsLogSpawned = TotalSpawned;
        LogStats();
    }
#endif
}
