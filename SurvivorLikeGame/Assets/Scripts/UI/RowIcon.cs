using UnityEngine;
using UnityEngine.UI;

// 列表行左侧图标的统一构建与解析。
// 升级/武器图标在 UpgradeIcons 里；这里补充商店、选角、难度与结算的映射。
public static class RowIcon
{
    public const float Size = 46f;

    public static Image Add(Transform parent, string name, Sprite sprite, Vector2 anchoredPos, float size = Size)
    {
        if (sprite == null) return null;

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    // ---------------- 永久商店 ----------------
    public static Sprite ForMetaStat(MetaStatType stat)
    {
        switch (stat)
        {
            case MetaStatType.MaxHealthFlat: return UpgradeIcons.Load("ic_health");
            case MetaStatType.DamagePct: return UpgradeIcons.Load("ic_damage");
            case MetaStatType.MoveSpeedPct: return UpgradeIcons.Load("ic_movespeed");
            case MetaStatType.ArmorFlat: return UpgradeIcons.Load("ic_armor");
            case MetaStatType.CritChanceFlat: return UpgradeIcons.Load("ic_crit");
            case MetaStatType.ExpGainPct: return UpgradeIcons.Load("ic_exp");
            case MetaStatType.PickupRadiusPct: return UpgradeIcons.Load("ic_pickup");
            default: return UpgradeIcons.Load("ic_damage");
        }
    }

    public static Sprite ForMetaUpgrade(MetaUpgradeData data)
    {
        return data != null ? ForMetaStat(data.stat) : UpgradeIcons.Load("ic_damage");
    }

    // ---------------- 角色 ----------------
    // 角色没有立绘，先用初始武器图标做身份标识，保证每个角色可区分
    public static Sprite ForCharacter(CharacterData data)
    {
        if (data == null) return UpgradeIcons.Load("ic_health");
        return UpgradeIcons.ForWeapon(data.initialWeapon);
    }

    // ---------------- 难度 ----------------
    public static Sprite ForDifficulty(DifficultyData data)
    {
        if (data == null) return UpgradeIcons.Load("ic_diff_normal");
        string id = (data.id ?? string.Empty).ToLowerInvariant();
        if (id.Contains("nightmare") || id.Contains("噩梦")) return UpgradeIcons.Load("ic_diff_nightmare");
        if (id.Contains("hard") || id.Contains("困难")) return UpgradeIcons.Load("ic_diff_hard");
        return UpgradeIcons.Load("ic_diff_normal");
    }

    // ---------------- 结算 ----------------
    public static Sprite ForSettlement(string label)
    {
        if (string.IsNullOrEmpty(label)) return null;
        if (label.Contains("时长")) return UpgradeIcons.Load("ic_time");
        if (label.Contains("击杀")) return UpgradeIcons.Load("ic_skull");
        if (label.Contains("金币")) return UpgradeIcons.Load("ic_coin");
        return null;
    }
}