using UnityEngine;

// 局外存档：目前只保存累计金币。
// PlayerPrefs 键名固定为 survivor.meta.totalCoins，后续解锁/升级系统沿用同一入口读写。
public static class MetaProgress
{
    public const string TotalCoinsKey = "survivor.meta.totalCoins";

    static bool loaded;
    static int totalCoins;

    // 累计金币：首次访问时从 PlayerPrefs 懒加载
    public static int TotalCoins
    {
        get
        {
            if (!loaded) Load();
            return totalCoins;
        }
    }

    // 从 PlayerPrefs 读取存档。金币是累加量，负值视为脏数据按 0 处理。
    public static void Load()
    {
        totalCoins = Mathf.Max(0, PlayerPrefs.GetInt(TotalCoinsKey, 0));
        loaded = true;
    }

    // 把本局金币写入存档并立即落盘，返回累加后的总数。
    public static int BankCoins(int amount)
    {
        if (!loaded) Load();
        if (amount > 0)
        {
            totalCoins += amount;
            PlayerPrefs.SetInt(TotalCoinsKey, totalCoins);
            PlayerPrefs.Save(); // 立即落盘，避免异常退出丢失本局收益
        }
        return totalCoins;
    }

    // 关闭域重载时静态字段会跨 Play 保留，这里回到未加载状态，下次访问重新读盘
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        loaded = false;
        totalCoins = 0;
    }
}
