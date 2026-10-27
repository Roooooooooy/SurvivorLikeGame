using UnityEngine;

// W5 波次事件调度器：运行时自动创建（不改场景），按 triggerTime 顺序触发
// Resources/WaveEvents 下配置的事件。每个事件只触发一次，场景重载后重置。
public class WaveEventScheduler : MonoBehaviour
{
    // 普通敌人类型数量：索引 0~5（NormalA/NormalB/Fast/Tank/Exploder/Ranged）。
    // 6 以后是 W5 追加的精英与 Boss 占位，包围潮只从普通池取，避免开局就刷精英。
    const int NormalTypeCount = 6;

    static WaveEventScheduler instance;

    WaveEventData[] events;   // 已按 triggerTime 升序排列
    bool[] triggered;         // 每个事件是否已触发（防御性判重）
    int nextIndex;            // 下一个待触发事件：到点前不扫描后面的条目

    // TimedElite 击杀窗口（同一时刻只保留一个）
    bool windowActive;
    float windowEndTime;      // 以 gameTime 计时
    int windowKills;
    int windowRequiredKills;
    int windowBonusExp;

    // 关闭 Domain Reload 进入 Play 时静态字段不会自动清空，这里显式重置
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    // 每个场景加载后自动建一个调度器；对象不跨场景，重载即重置
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (instance != null) return;
        new GameObject("[WaveEventScheduler]").AddComponent<WaveEventScheduler>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        events = Resources.LoadAll<WaveEventData>("WaveEvents");
        System.Array.Sort(events, (a, b) => a.triggerTime.CompareTo(b.triggerTime));
        triggered = new bool[events.Length];
        nextIndex = 0;
    }

    void OnEnable()
    {
        CombatEvents.Kill += OnKill;
    }

    void OnDisable()
    {
        CombatEvents.Kill -= OnKill;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // 击杀事件只用于限时窗口计数；窗口未开启时直接返回
    void OnKill(Vector2 pos, int expValue)
    {
        if (windowActive) windowKills++;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // 首页与死亡结算期间不触发新事件；残留的限时窗口直接作废
        if (gm.isGameOver || IsTitleScreen(gm))
        {
            if (windowActive) CancelWindow();
            return;
        }

        float now = gm.gameTime;

        // 指针推进：只处理已经到点的事件，全触发后不再每帧扫描
        while (nextIndex < events.Length && now >= events[nextIndex].triggerTime)
        {
            Trigger(nextIndex);
            nextIndex++;
        }

        if (windowActive && now >= windowEndTime) FinishWindow(false);

        // 没有待触发事件、也没有进行中的窗口：停掉 Update，彻底消除每帧开销
        if (nextIndex >= events.Length && !windowActive)
            enabled = false;
    }

    // 兼容没有首页的场景：titleScreen 未配置时视为已开局
    static bool IsTitleScreen(GameManager gm)
    {
        return gm.titleScreen != null && gm.titleScreen.activeInHierarchy;
    }

    void Trigger(int index)
    {
        if (triggered[index]) return;
        triggered[index] = true;

        var e = events[index];
        if (e == null) return;

        var player = PlayerController.Instance;
        ShowBanner(e);

        switch (e.eventType)
        {
            case WaveEventType.Elite:
                SpawnRing(player, e.enemyTypeIndex, e.count, e.radius);
                break;

            case WaveEventType.SurroundTide:
                SpawnTide(player, e);
                break;

            case WaveEventType.TimedElite:
                SpawnRing(player, e.enemyTypeIndex, e.count, e.radius);
                StartWindow(e);
                break;

            case WaveEventType.Boss:
                // TODO(W5+): 多阶段 Boss 做好后替换这里的单只超大精英占位
                SpawnRing(player, e.enemyTypeIndex, 1, e.radius);
                break;
        }

        Debug.Log($"[W5] 触发 {e.eventType} @ {e.triggerTime}s（{e.count} 只，类型 {e.enemyTypeIndex}）");
    }

    static void ShowBanner(WaveEventData e)
    {
        WaveBanner.Show(e.bannerTitle, e.bannerSubtitle, e.duration);
    }

    // 环形生成：均匀铺开再加少量角度/半径抖动，避免每次落点完全重合
    static void SpawnRing(PlayerController player, int typeIndex, int count, float radius)
    {
        if (player == null || count <= 0) return;
        var sim = EnemySimulation.Instance;
        if (sim == null || sim.TypeCount <= 0) return;

        Vector2 center = player.transform.position;
        float baseAngle = Random.value * Mathf.PI * 2f;
        float step = Mathf.PI * 2f / count;
        float r = Mathf.Max(1f, radius);
        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + step * i + Random.Range(-0.12f, 0.12f);
            Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (r + Random.Range(-0.4f, 0.4f));
            sim.Spawn(typeIndex, pos);
        }
    }

    // 包围潮：环形均匀铺满普通敌人，类型在普通池里随机
    static void SpawnTide(PlayerController player, WaveEventData e)
    {
        if (player == null || e.count <= 0) return;
        var sim = EnemySimulation.Instance;
        if (sim == null || sim.TypeCount <= 0) return;

        int pool = Mathf.Clamp(NormalTypeCount, 1, sim.TypeCount);
        Vector2 center = player.transform.position;
        float baseAngle = Random.value * Mathf.PI * 2f;
        float step = Mathf.PI * 2f / e.count;
        float r = Mathf.Max(1f, e.radius);
        for (int i = 0; i < e.count; i++)
        {
            float angle = baseAngle + step * i + Random.Range(-0.1f, 0.1f);
            Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (r + Random.Range(-0.6f, 0.6f));
            sim.Spawn(Random.Range(0, pool), pos);
        }
    }

    void StartWindow(WaveEventData e)
    {
        // 同一时刻只保留一个窗口：先结算旧的（阈值按旧事件）
        if (windowActive) FinishWindow(false);

        windowActive = true;
        windowKills = 0;
        windowRequiredKills = Mathf.Max(0, e.requiredKills);
        windowBonusExp = Mathf.Max(0, e.bonusExp);
        windowEndTime = GameManager.Instance.gameTime + Mathf.Max(0.1f, e.duration);
    }

    // cancelled=true 表示玩家死亡/回首页，不结算奖励
    void FinishWindow(bool cancelled)
    {
        if (!windowActive) return;

        bool success = windowKills >= windowRequiredKills;
        int kills = windowKills;
        int required = windowRequiredKills;
        int bonus = windowBonusExp;

        windowActive = false;
        windowKills = 0;

        if (cancelled) return;

        if (success && bonus > 0)
        {
            var player = PlayerController.Instance;
            if (player != null) player.GainExp(bonus);
            WaveBanner.Show("限时精英达成", "+" + bonus + " 经验", 2f);
            Debug.Log($"[W5] 限时精英达标 {kills}/{required}，奖励 {bonus} 经验");
        }
        else
        {
            Debug.Log($"[W5] 限时精英未达标 {kills}/{required}，不发奖励");
        }
    }

    void CancelWindow()
    {
        windowActive = false;
        windowKills = 0;
    }
}
