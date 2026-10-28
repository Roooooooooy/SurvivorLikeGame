using UnityEngine;

// 局外存档：累计金币、永久升级等级与解锁项。
// 全部通过 PlayerPrefs 读写，键名统一以 survivor.meta. 开头，方便日后迁移。
public static class MetaProgress
{
    public const string TotalCoinsKey = "survivor.meta.totalCoins";
    public const string VersionKey = "survivor.meta.version";
    public const int CurrentVersion = 1;

    // 永久升级等级与解锁项的键前缀，后面直接拼 id
    const string UpgradeKeyPrefix = "survivor.meta.upgrade.";
    const string UnlockKeyPrefix = "survivor.meta.unlock.";

    // 永久升级资产所在目录（Resources 相对路径）
    const string MetaUpgradesPath = "MetaUpgrades";

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
        // 只记录当前存档版本，不做任何迁移
        PlayerPrefs.SetInt(VersionKey, CurrentVersion);
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

    // ---------------- 永久升级 ----------------

    // 永久升级等级：按 id 存 PlayerPrefs，0 = 未购买。
    // 脏数据（负值）按未购买处理；具体上限由 MetaUpgradeData.maxLevel 决定。
    public static int GetUpgradeLevel(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        return Mathf.Max(0, PlayerPrefs.GetInt(UpgradeKeyPrefix + id, 0));
    }

    // 下一级价格：baseCost * costGrowth^level，四舍五入且至少 1。
    public static int GetUpgradeCost(MetaUpgradeData data)
    {
        if (data == null) return 0;
        float raw = data.baseCost * Mathf.Pow(Mathf.Max(0f, data.costGrowth), GetUpgradeLevel(data.id));
        // 用 +0.5 向下取整实现四舍五入，避免 Mathf.RoundToInt 的银行家舍入
        return Mathf.Max(1, Mathf.FloorToInt(raw + 0.5f));
    }

    // 购买下一级：未满级且金币足够时扣金币、等级 +1 并立即落盘。
    // 任何一项不满足都返回 false，且不产生副作用。
    public static bool TryPurchase(MetaUpgradeData data)
    {
        if (data == null || string.IsNullOrEmpty(data.id)) return false;

        int level = GetUpgradeLevel(data.id);
        if (level >= data.maxLevel) return false; // 已满级

        if (!loaded) Load();
        int cost = GetUpgradeCost(data);
        if (totalCoins < cost) return false; // 余额不足

        totalCoins -= cost;
        PlayerPrefs.SetInt(TotalCoinsKey, totalCoins);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + data.id, level + 1);
        PlayerPrefs.SetInt(VersionKey, CurrentVersion);
        PlayerPrefs.Save(); // 立即落盘，避免异常退出丢失购买结果
        return true;
    }

    // 新一局开始时把永久等级应用到玩家初始属性。
    // 读取 Resources/MetaUpgrades 下的全部升级资产，按 等级 × valuePerLevel 叠加。
    // Flat 类直接加，Pct 类按比例乘算；多个同类升级之间也按乘算叠加。
    public static void ApplyPermanentStats(PlayerController player, PlayerStats stats, ExpPickupRadius pickup)
    {
        var assets = Resources.LoadAll<MetaUpgradeData>(MetaUpgradesPath);
        foreach (var data in assets)
        {
            if (data == null || string.IsNullOrEmpty(data.id)) continue;

            int level = Mathf.Min(GetUpgradeLevel(data.id), data.maxLevel);
            if (level <= 0) continue;

            float amount = data.valuePerLevel * level;
            switch (data.stat)
            {
                case MetaStatType.MaxHealthFlat:
                    if (player != null) player.maxHealth += Mathf.RoundToInt(amount);
                    break;
                case MetaStatType.DamagePct:
                    if (stats != null) stats.damageMultiplier *= 1f + amount;
                    break;
                case MetaStatType.MoveSpeedPct:
                    if (stats != null) stats.moveSpeedMultiplier *= 1f + amount;
                    break;
                case MetaStatType.ArmorFlat:
                    if (stats != null) stats.armor += Mathf.RoundToInt(amount);
                    break;
                case MetaStatType.CritChanceFlat:
                    if (stats != null) stats.critChance = Mathf.Min(1f, stats.critChance + amount);
                    break;
                case MetaStatType.ExpGainPct:
                    if (stats != null) stats.expMultiplier *= 1f + amount;
                    break;
                case MetaStatType.PickupRadiusPct:
                    if (pickup != null) pickup.radius *= 1f + amount;
                    break;
            }
        }
    }

    // ---------------- 解锁项 ----------------
    // 解锁项（武器/角色/难度）的通用存储；具体接入由后续波次完成。

    public static bool IsUnlocked(string id)
        => !string.IsNullOrEmpty(id) && PlayerPrefs.GetInt(UnlockKeyPrefix + id, 0) != 0;

    public static void Unlock(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        PlayerPrefs.SetInt(UnlockKeyPrefix + id, 1);
        PlayerPrefs.SetInt(VersionKey, CurrentVersion);
        PlayerPrefs.Save();
    }

    // 解锁购买：扣金币；余额不足返回 false 且不产生副作用。
    // 解锁键约定：character_<id> / difficulty_<id> / weapon_<id>
    public static bool TrySpendCoins(int amount)
    {
        if (amount <= 0) return true;
        if (!loaded) Load();
        if (totalCoins < amount) return false;

        totalCoins -= amount;
        PlayerPrefs.SetInt(TotalCoinsKey, totalCoins);
        PlayerPrefs.SetInt(VersionKey, CurrentVersion);
        PlayerPrefs.Save();
        return true;
    }
}
