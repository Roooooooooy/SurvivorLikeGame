using UnityEngine;

// Boss 状态总线：DOTS 仿真层负责写入，血条 UI / 奖励系统负责读取，
// 避免 UI 直接依赖 EnemySimulation 的内部数组。
// 场景重载或重新进入 Play 时重置，防止残留 Active 状态。
public static class BossStatus
{
    public static bool Active { get; private set; }
    public static string BossName { get; private set; }
    public static int CurrentHealth { get; private set; }
    public static int MaxHealth { get; private set; }
    public static int Phase { get; private set; }        // 1..PhaseCount
    public static int PhaseCount { get; private set; }   // 总阶段数

    // 击杀事件：奖励/进化材料系统后续订阅（当前无人订阅也不影响）
    public static event System.Action BossKilled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = false;
        BossName = null;
        CurrentHealth = 0;
        MaxHealth = 0;
        Phase = 0;
        PhaseCount = 0;
        BossKilled = null; // 关闭 Domain Reload 时清掉旧订阅
    }

    // Boss 出生：由仿真层在检测到 Boss 敌人时调用
    public static void Spawn(string bossName, int maxHealth, int phaseCount)
    {
        BossName = bossName;
        MaxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = MaxHealth;
        PhaseCount = Mathf.Max(1, phaseCount);
        Phase = 1;
        Active = true;
    }

    // 每帧/受击后同步当前血量与阶段
    public static void UpdateHealth(int currentHealth, int phase)
    {
        if (!Active) return;
        CurrentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
        Phase = Mathf.Clamp(phase, 1, Mathf.Max(1, PhaseCount));
    }

    // Boss 被击杀：关闭状态并通知订阅者（经验/材料奖励）
    public static void Kill()
    {
        if (!Active) return;
        CurrentHealth = 0;
        Active = false;
        BossKilled?.Invoke();
    }

    // 玩家死亡/回首页/场景重载：静默关闭，不触发奖励
    public static void Despawn()
    {
        Active = false;
        CurrentHealth = 0;
        MaxHealth = 0;
        Phase = 0;
        PhaseCount = 0;
        BossName = null;
    }
}
