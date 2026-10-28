using UnityEngine;

// 打击反馈：订阅命中/击杀事件，负责震屏与极短顿帧（hit-stop）。
// 顿帧用 unscaledTime 恢复；升级面板打开、结算/标题暂停时跳过；
// 全局冷却避免连续命中反复触发，也不会把其他系统设置的 timeScale 顶掉。
public class CombatFeedback : MonoBehaviour
{
    // 只给“有分量”的命中/击杀做反馈：普通命中、普通怪击杀不震屏/顿帧，
    // 否则持续伤害与刷屏击杀会让画面一直抖、看起来像在漂。
    private const float CritShake = 0.08f;
    private const float BossKillShake = 0.10f;
    private const float CritShakeDuration = 0.12f;
    private const float BossKillShakeDuration = 0.14f;

    private const float HitStopScale = 0.06f;
    private const float HitStopDuration = 0.035f;
    private const float HitStopCooldown = 0.35f;

    private static CombatFeedback instance;

    public static CombatFeedback Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("CombatFeedback");
                instance = go.AddComponent<CombatFeedback>();
            }
            return instance;
        }
    }

    // 每次场景加载后立即重建实例并重新订阅（重开场景不会再次触发 RuntimeInitializeOnLoadMethod）
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        _ = Instance;
    }

    private bool hitStopActive;
    private float hitStopEndTime;
    private float restoreScale = 1f;
    private float lastHitStopTime = -999f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnEnable()
    {
        CombatEvents.HitDetailed += OnHit;
        CombatEvents.Kill += OnKill;
    }

    private void OnDisable()
    {
        CombatEvents.HitDetailed -= OnHit;
        CombatEvents.Kill -= OnKill;
        RestoreNow(); // 场景重载/销毁时不要把慢动作带到下一局
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnHit(Vector2 pos, int damage, bool crit)
    {
        // 普通命中不震屏；暴击只做轻微震屏，不顿帧，避免持续伤害把画面抖散
        if (crit) CameraShake.Add(CritShake, CritShakeDuration);
    }

    private void OnKill(Vector2 pos, int expValue)
    {
        // 普通怪（exp 1~2）不触发反馈；精英 20 / Boss 50 才有分量
        if (expValue < 15) return;

        CameraShake.Add(BossKillShake, BossKillShakeDuration);
        TryHitStop();
    }

    // 启动一次顿帧；升级面板/结算/已暂停时直接跳过
    private void TryHitStop()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            if (gm.isGameOver) return;
            if (gm.levelUpPanel != null && gm.levelUpPanel.activeInHierarchy) return;
        }

        if (Time.timeScale <= 0.001f) return;
        if (Time.unscaledTime - lastHitStopTime < HitStopCooldown) return;

        lastHitStopTime = Time.unscaledTime;
        if (!hitStopActive)
        {
            restoreScale = Time.timeScale; // 通常为 1；恢复时回到触发前的值
            Time.timeScale = HitStopScale;
            hitStopActive = true;
        }
        hitStopEndTime = Time.unscaledTime + HitStopDuration;
    }

    private void Update()
    {
        if (!hitStopActive) return;

        // 其他系统（升级暂停、结算、重开）改过 timeScale：放弃接管，避免把暂停顶掉
        if (!Mathf.Approximately(Time.timeScale, HitStopScale))
        {
            hitStopActive = false;
            return;
        }

        if (Time.unscaledTime < hitStopEndTime) return;
        RestoreNow();
    }

    private void RestoreNow()
    {
        if (!hitStopActive) return;
        hitStopActive = false;
        if (Mathf.Approximately(Time.timeScale, HitStopScale))
            Time.timeScale = restoreScale;
    }
}
