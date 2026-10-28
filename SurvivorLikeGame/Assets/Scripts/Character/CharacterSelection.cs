using UnityEngine;

// 角色选择与属性应用。
// 选中角色存 PlayerPrefs（跨局保留），新局开始时由 PlayerController 调用 ApplyTo。
public static class CharacterSelection
{
    const string CharactersPath = "Characters";
    // 选择结果的持久化键；与 UI 高亮共用同一个键
    const string PrefsKey = "survivor.character.selected";

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

    // 当前选中的角色；未选择或存档 id 已失效时返回第一个角色（或 null）
    public static CharacterData Selected
    {
        get
        {
            var all = All;
            if (all.Length == 0) return null;

            string saved = PlayerPrefs.GetString(PrefsKey, "");
            foreach (var c in all)
                if (c != null && c.id == saved) return c;

            return all[0];
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

    // 选择角色并持久化；空 id 忽略，避免误清空选择
    public static void Select(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
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
