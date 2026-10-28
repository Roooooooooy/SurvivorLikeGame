using UnityEditor;
using UnityEngine;

// Meta-B 永久升级资产生成器：在 Resources/MetaUpgrades 下生成 7 个升级资产。
// 只走 AssetDatabase，不手写 YAML；可重复执行（同名资产覆盖数值）。
public static class MetaUpgradeGenerator
{
    const string MetaUpgradesDir = "Assets/Resources/MetaUpgrades";

    [MenuItem("Tools/Survivor/Generate Meta Upgrades")]
    public static void Generate()
    {
        if (!EnsureFolder()) return;

        // id                 显示名             描述                                      属性                      每级      基础价 增长  上限
        CreateUpgrade("meta_max_health", "生命上限 +10", "每级永久提升 10 点生命上限。",
            MetaStatType.MaxHealthFlat, 10f, 15, 1.5f, 5);
        CreateUpgrade("meta_damage", "伤害 +5%", "每级永久提升 5% 全武器伤害。",
            MetaStatType.DamagePct, 0.05f, 20, 1.6f, 5);
        CreateUpgrade("meta_move", "移速 +4%", "每级永久提升 4% 移动速度。",
            MetaStatType.MoveSpeedPct, 0.04f, 15, 1.5f, 5);
        CreateUpgrade("meta_armor", "护甲 +1", "每级永久提升 1 点护甲，减少受到的伤害。",
            MetaStatType.ArmorFlat, 1f, 20, 1.6f, 5);
        CreateUpgrade("meta_crit", "暴击率 +2%", "每级永久提升 2% 暴击率。",
            MetaStatType.CritChanceFlat, 0.02f, 25, 1.7f, 5);
        CreateUpgrade("meta_exp", "经验 +5%", "每级永久提升 5% 经验获取。",
            MetaStatType.ExpGainPct, 0.05f, 15, 1.5f, 5);
        CreateUpgrade("meta_pickup", "拾取范围 +8%", "每级永久提升 8% 经验与金币拾取半径。",
            MetaStatType.PickupRadiusPct, 0.08f, 10, 1.4f, 5);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
    }

    static bool EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(MetaUpgradesDir)) return true;
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            Debug.LogError("[META][GEN] 缺少 Assets/Resources 目录，生成中止");
            return false;
        }
        AssetDatabase.CreateFolder("Assets/Resources", "MetaUpgrades");
        return AssetDatabase.IsValidFolder(MetaUpgradesDir);
    }

    static void CreateUpgrade(string id, string displayName, string description,
        MetaStatType stat, float valuePerLevel, int baseCost, float costGrowth, int maxLevel)
    {
        string path = MetaUpgradesDir + "/" + id + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(path);
        bool isNew = asset == null;
        if (isNew) asset = ScriptableObject.CreateInstance<MetaUpgradeData>();

        asset.id = id;
        asset.displayName = displayName;
        asset.description = description;
        asset.stat = stat;
        asset.valuePerLevel = valuePerLevel;
        asset.baseCost = baseCost;
        asset.costGrowth = costGrowth;
        asset.maxLevel = maxLevel;

        if (isNew) AssetDatabase.CreateAsset(asset, path);
        else EditorUtility.SetDirty(asset);
    }

    // 生成后自检：数量、id 唯一、关键数值符合设计表
    static void Validate()
    {
        var assets = Resources.LoadAll<MetaUpgradeData>("MetaUpgrades");
        int pass = 0;
        var errors = new System.Collections.Generic.List<string>();

        void Check(bool ok, string msg)
        {
            if (ok) { pass++; Debug.Log("[META][GEN][PASS] " + msg); }
            else { errors.Add(msg); Debug.LogError("[META][GEN][FAIL] " + msg); }
        }

        Check(assets.Length == 7, "Resources/MetaUpgrades 共 7 个资产，实际 " + assets.Length);

        var expected = new (string id, MetaStatType stat, float perLevel, int baseCost, float growth)[]
        {
            ("meta_max_health", MetaStatType.MaxHealthFlat, 10f, 15, 1.5f),
            ("meta_damage", MetaStatType.DamagePct, 0.05f, 20, 1.6f),
            ("meta_move", MetaStatType.MoveSpeedPct, 0.04f, 15, 1.5f),
            ("meta_armor", MetaStatType.ArmorFlat, 1f, 20, 1.6f),
            ("meta_crit", MetaStatType.CritChanceFlat, 0.02f, 25, 1.7f),
            ("meta_exp", MetaStatType.ExpGainPct, 0.05f, 15, 1.5f),
            ("meta_pickup", MetaStatType.PickupRadiusPct, 0.08f, 10, 1.4f),
        };

        foreach (var e in expected)
        {
            var asset = System.Array.Find(assets, a => a != null && a.id == e.id);
            if (asset == null)
            {
                Check(false, "缺少资产 " + e.id);
                continue;
            }
            Check(asset.stat == e.stat && Mathf.Approximately(asset.valuePerLevel, e.perLevel)
                && asset.baseCost == e.baseCost && Mathf.Approximately(asset.costGrowth, e.growth)
                && asset.maxLevel == 5,
                asset.id + " 数值正确（" + asset.stat + " / 每级 " + asset.valuePerLevel
                + " / 基础 " + asset.baseCost + " / 增长 " + asset.costGrowth + " / 上限 " + asset.maxLevel + "）");
        }

        if (errors.Count == 0)
            Debug.Log("[META][GEN] 生成完成并通过校验：" + pass + " 项");
        else
            Debug.LogError("[META][GEN] 生成结束，但校验失败 " + errors.Count + " 项："
                + string.Join("；", errors));
    }
}
