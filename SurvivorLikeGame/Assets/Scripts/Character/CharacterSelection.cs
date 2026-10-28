using UnityEngine;

// 角色选择与属性应用。
// 选中角色存 PlayerPrefs（跨局保留），新局开始时由 PlayerController 调用 ApplyTo。
public static class CharacterSelection
{
    const string CharactersPath = "Characters";
    // 选择结果的持久化键；与 UI 高亮共用同一个键
    const string PrefsKey = "survivor.character.selected";

    // 角色解锁键前缀：完整键为 "character_<id>"，与 MetaProgress 的解锁契约一致
    const string UnlockKeyPrefix = "character_";

    static CharacterData[] cache;

    // 全部可选角色（来自 Resources/Characters），按 id 排序保证首项稳定
    public static CharacterData[] All
    {
        get
        {
            if (cache == null)
            {
                cache = Resources.LoadAll<CharacterData>(CharactersPath);
                System.Array.Sort(cache, (a, b) => string.CompareOrdinal(a != null ? a.id : "", b != null ? b.id : ""));
            }
            return cache;
        }
    }

    // 角色是否已解锁：默认角色直接可选，其余需要 MetaProgress 中对应解锁键
    public static bool IsUnlocked(CharacterData data)
    {
        if (data == null || string.IsNullOrEmpty(data.id)) return false;
        if (data.unlockedByDefault) return true;
        return MetaProgress.IsUnlocked(UnlockKey(data.id));
    }

    // 角色解锁键："character_<id>"；空 id 返回空串，交由 MetaProgress 侧忽略
    public static string UnlockKey(string id)
        => string.IsNullOrEmpty(id) ? "" : UnlockKeyPrefix + id;

    // 当前选中的角色；未选择、存档 id 已失效或对应角色仍锁定时，
    // 回退到第一个已解锁角色（全部锁定时返回 null）。
    public static CharacterData Selected
    {
        get
        {
            var all = All;
            if (all.Length == 0) return null;

            string saved = PlayerPrefs.GetString(PrefsKey, "");
            CharacterData fallback = null;
            foreach (var c in all)
            {
                if (c == null) continue;
                bool unlocked = IsUnlocked(c);
                if (fallback == null && unlocked) fallback = c;
                if (unlocked && c.id == saved) return c;
            }

            return fallback;
        }
    }

    // 当前选中角色的 id（用于 UI 高亮）
    public static string SelectedId
    {
        get
        {
            var selected = Selected;
            return selected != null ? selected.id : "";
        }
    }

    // 选择角色并持久化；空 id 忽略，避免误清空选择。
    // 锁定角色（或 id 不存在）拒绝选择：不写存档，保持原选择不变。
    public static void Select(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        CharacterData target = null;
        foreach (var c in All)
        {
            if (c != null && c.id == id)
            {
                target = c;
                break;
            }
        }
        if (!IsUnlocked(target)) return;

        PlayerPrefs.SetString(PrefsKey, id);
        PlayerPrefs.Save();
    }

    // 新局开始时应用角色的初始属性加成。
    // 调用时机在 MetaProgress.ApplyPermanentStats 之后，两类加成乘算叠加。
    public static void ApplyTo(PlayerController player, PlayerStats stats, ExpPickupRadius pickup)
    {
        var data = Selected;
        if (data == null) return;

        // 生命上限为固定值加成，由 PlayerController 负责最终下限校正
        if (player != null)
            player.maxHealth += data.maxHealthFlat;

        if (stats != null)
        {
            stats.damageMultiplier *= 1f + data.damagePct;
            stats.moveSpeedMultiplier *= 1f + data.moveSpeedPct;
            stats.expMultiplier *= 1f + data.expGainPct;
            stats.armor += Mathf.RoundToInt(data.armorFlat);
            stats.critChance = Mathf.Min(1f, stats.critChance + data.critChanceFlat);
            stats.cooldownReduction = Mathf.Min(0.8f, stats.cooldownReduction + data.cooldownReductionFlat);
        }

        if (pickup != null)
            pickup.radius *= 1f + data.pickupRadiusPct;
    }
}
