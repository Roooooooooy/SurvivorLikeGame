using UnityEngine;
using UnityEngine.SceneManagement;

// 局内统计（击杀 / 金币 / 时长）。
// 契约：FinalizeRun 幂等——第一次调用时把本局金币存入 MetaProgress，并返回快照；
// 之后重复调用返回同一快照，避免死亡演出与结算界面重复结算。
public static class RunStats
{
    static int kills;
    static int coinsEarned;
    static bool finalized;
    static RunSummary finalizedSnapshot;

    // 当前局内统计：未结算时实时计算（结算界面/死亡演出之前也可读）
    public static RunSummary Current
    {
        get
        {
            if (finalized) return finalizedSnapshot;
            return new RunSummary
            {
                TimeSeconds = GameManager.Instance != null ? GameManager.Instance.gameTime : 0f,
                Kills = kills,
                CoinsEarned = coinsEarned,
                TotalCoins = MetaProgress.TotalCoins, // 本局尚未入账，先展示局外累计值
            };
        }
    }

    // 拾取金币：只记本局收入，FinalizeRun 时才统一入账
    public static void AddCoins(int amount)
    {
        if (finalized || amount <= 0) return;
        coinsEarned += amount;
    }

    // 结算本局：金币存入 MetaProgress 并回填 TotalCoins。重复调用返回同一快照。
    public static RunSummary FinalizeRun()
    {
        if (finalized) return finalizedSnapshot;

        finalizedSnapshot = new RunSummary
        {
            TimeSeconds = GameManager.Instance != null ? GameManager.Instance.gameTime : 0f,
            Kills = kills,
            CoinsEarned = coinsEarned,
            TotalCoins = MetaProgress.BankCoins(coinsEarned),
        };
        finalized = true;
        return finalizedSnapshot;
    }

    // 新一局开始：清空计数并允许再次结算
    public static void ResetRun()
    {
        kills = 0;
        coinsEarned = 0;
        finalized = false;
        finalizedSnapshot = default;
    }

    // 击杀计数由 tracker 订阅 CombatEvents.Kill 维护：与掉落/音效一样每只敌人只加一次
    internal static void AddKill()
    {
        if (!finalized) kills++;
    }
}

// 运行时自动创建的统计追踪器：
// 进入 Play 后在场景加载完成时生成，跨场景保留；重开场景时清空统计。
// 独立于场景中的任何对象，避免依赖 SampleScene 的层级结构。
internal class RunStatsTracker : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("[RunStatsTracker]");
        go.AddComponent<RunStatsTracker>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        RunStats.ResetRun();
        SceneManager.sceneLoaded += OnSceneLoaded; // RestartGame 重载场景后重新开始统计
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnEnable()
    {
        CombatEvents.Kill += OnKill;
    }

    void OnDisable()
    {
        CombatEvents.Kill -= OnKill;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RunStats.ResetRun();
    }

    static void OnKill(Vector2 pos, int expValue)
    {
        RunStats.AddKill();
    }
}
