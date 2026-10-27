using UnityEngine;
using UnityEngine.SceneManagement;

// Boss 击杀奖励：运行时自动创建（不改场景），击杀 +1 进化材料并弹出横幅。
// 对象不跨场景——重开一局时旧实例销毁、新实例创建，材料随之清零。
public class BossReward : MonoBehaviour
{
    private static BossReward instance;

    // 关闭 Domain Reload 进入 Play 时静态字段/回调不会自动清空，这里显式重置
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 首场景加载后建一个奖励对象；之后每次 LoadScene 由 sceneLoaded 补建，
    // 保证死亡重开的一局也能拿到 Boss 材料。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
    }

    static void EnsureInstance()
    {
        if (instance != null) return;
        new GameObject("[BossReward]").AddComponent<BossReward>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject); // 同一时间只保留一个奖励对象
            return;
        }
        instance = this;
        PlayerProgress.Reset(); // 新场景：Boss 材料清零
    }

    void OnEnable()
    {
        BossStatus.BossKilled += OnBossKilled;
    }

    void OnDisable()
    {
        BossStatus.BossKilled -= OnBossKilled;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Boss 死亡时由 BossStatus.Kill 触发一次
    void OnBossKilled()
    {
        PlayerProgress.AddBossMaterial(1);
        WaveBanner.Show("BOSS 已击杀", "获得进化材料 ×1", 2f);
    }
}
