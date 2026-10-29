using System.Collections.Generic;
using UnityEngine;

// 升级/武器图标的解析与缓存。
// 图标按 stat/武器类型解析，不依赖逐个资产手工填 icon 字段；
// 资产上显式指定 icon 时优先使用，方便后续单独覆盖。
public static class UpgradeIcons
{
    static readonly Dictionary<string, Sprite> cache = new();

    // 下标即 UpgradeStat 枚举值；null 表示该 stat 需要额外上下文（当前只有 WeaponGrant）
    static readonly string[] StatIcons =
    {
        "ic_movespeed",           // MoveSpeedPct
        "ic_health",              // MaxHealthFlat
        "ic_pickup",              // PickupRadiusPct
        "ic_armor",               // ArmorFlat
        "ic_crit",                // CritChanceFlat
        "ic_critdmg",             // CritDamageFlat
        "ic_exp",                 // ExpGainPct
        "ic_area",                // AreaPct
        "ic_cooldown",            // CooldownReductionFlat
        "ic_damage",              // DamagePct
        "ic_weapondmg",           // WeaponDamagePct
        "ic_weaponcount",         // WeaponCountFlat
        "ic_weaponspeed",         // WeaponSpeedPct
        "ic_interval",            // WeaponIntervalPct
        null,                     // WeaponGrant：用武器自身图标
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cache.Clear();
    }

    public static Sprite ForOption(UpgradeOption option)
    {
        if (option == null) return Load("ic_damage");

        if (option.IsEvolution)
            return Load("ic_evo_bosscore");

        var data = option.upgrade;
        if (data == null) return Load("ic_damage");
        if (data.icon != null) return data.icon;

        return ForData(data);
    }

    public static Sprite ForData(UpgradeData data)
    {
        if (data == null || data.effects == null || data.effects.Count == 0)
            return Load("ic_damage");

        // 一个升级可能带多个效果，取第一个有意义的
        for (int i = 0; i < data.effects.Count; i++)
        {
            var e = data.effects[i];
            if (e == null) continue;

            if (e.stat == UpgradeStat.WeaponGrant)
                return ForWeapon(e.weapon);

            string name = StatIconName(e.stat);
            if (name != null)
            {
                // 武器类数值升级优先用武器图标，辨识度更高
                if (IsWeaponStat(e.stat) && e.weapon != WeaponTarget.All)
                    return Load(WeaponIconName(e.weapon));
                return Load(name);
            }
        }
        return Load("ic_damage");
    }

    public static Sprite ForWeapon(WeaponTarget target)
    {
        return Load(WeaponIconName(target));
    }

    static bool IsWeaponStat(UpgradeStat stat)
    {
        return stat == UpgradeStat.WeaponDamagePct ||
               stat == UpgradeStat.WeaponCountFlat ||
               stat == UpgradeStat.WeaponSpeedPct ||
               stat == UpgradeStat.WeaponIntervalPct;
    }

    static string StatIconName(UpgradeStat stat)
    {
        int i = (int)stat;
        if (i < 0 || i >= StatIcons.Length) return null;
        return StatIcons[i];
    }

    static string WeaponIconName(WeaponTarget weapon)
    {
        switch (weapon)
        {
            case WeaponTarget.Orb: return "ic_w_orb";
            case WeaponTarget.Projectile: return "ic_w_projectile";
            case WeaponTarget.Pierce: return "ic_w_pierce";
            case WeaponTarget.Lightning: return "ic_w_lightning";
            case WeaponTarget.Nova: return "ic_w_nova";
            case WeaponTarget.Boomerang: return "ic_w_boomerang";
            default: return "ic_damage";
        }
    }

    public static Sprite Load(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (cache.TryGetValue(name, out var cached)) return cached;

        var sprite = Resources.Load<Sprite>("Icons/" + name);
        cache[name] = sprite; // 缺失也缓存 null，避免反复 IO
        return sprite;
    }
}