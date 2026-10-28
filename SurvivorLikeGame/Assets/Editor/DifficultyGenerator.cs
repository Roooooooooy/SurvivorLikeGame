using UnityEditor;
using UnityEngine;

// 难度资产生成器：在 Resources/Difficulties 下生成 3 个难度档位。
// 只走 AssetDatabase，不手写 YAML；可重复执行（同名资产覆盖数值）。
public static class DifficultyGenerator
{
    const string DifficultyDir = "Assets/Resources/Difficulties";

    [MenuItem("Tools/Survivor/Generate Difficulties")]
    public static void Generate()
    {
        if (!EnsureFolder()) return;

        // id            显示名   描述                                                          血量  伤害  速度  金币  默认解锁 价格
        CreateDifficulty("normal", "普通", "标准难度：敌人属性与金币收益保持原始数值。",
            1f, 1f, 1f, 1f, true, 0);
        CreateDifficulty("hard", "困难", "困难：敌人血量 ×1.5、伤害 ×1.25、速度 ×1.1，金币 ×1.25。",
            1.5f, 1.25f, 1.1f, 1.25f, false, 30);
        CreateDifficulty("nightmare", "噩梦", "噩梦：敌人血量 ×2.2、伤害 ×1.6、速度 ×1.2，金币 ×1.6。",
            2.2f, 1.6f, 1.2f, 1.6f, false, 80);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
    }

    static bool EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(DifficultyDir)) return true;
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            Debug.LogError("[DIFFICULTY][GEN] 缺少 Assets/Resources 目录，生成中止");
            return false;
        }
        AssetDatabase.CreateFolder("Assets/Resources", "Difficulties");
        return AssetDatabase.IsValidFolder(DifficultyDir);
    }

    static void CreateDifficulty(string id, string displayName, string description,
        float healthMul, float damageMul, float speedMul, float coinMul,
        bool unlockedByDefault, int unlockCost)
    {
        string path = DifficultyDir + "/" + id + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<DifficultyData>(path);
        bool isNew = asset == null;
        if (isNew) asset = ScriptableObject.CreateInstance<DifficultyData>();

        asset.id = id;
        asset.displayName = displayName;
        asset.description = description;
        asset.enemyHealthMultiplier = healthMul;
        asset.enemyDamageMultiplier = damageMul;
        asset.enemySpeedMultiplier = speedMul;
        asset.coinMultiplier = coinMul;
        asset.unlockedByDefault = unlockedByDefault;
        asset.unlockCost = unlockCost;

        if (isNew) AssetDatabase.CreateAsset(asset, path);
        else EditorUtility.SetDirty(asset);
    }

    // 生成后自检：数量、id 唯一、倍率与解锁配置符合设计表
    static void Validate()
    {
        var assets = Resources.LoadAll<DifficultyData>("Difficulties");
        int pass = 0;
        var errors = new System.Collections.Generic.List<string>();

        void Check(bool ok, string msg)
        {
            if (ok) { pass++; Debug.Log("[DIFFICULTY][GEN][PASS] " + msg); }
            else { errors.Add(msg); Debug.LogError("[DIFFICULTY][GEN][FAIL] " + msg); }
        }

        Check(assets.Length == 3, "Resources/Difficulties 共 3 个资产，实际 " + assets.Length);

        var expected = new (string id, float hp, float dmg, float speed, float coin,
            bool unlocked, int cost)[]
        {
            ("normal", 1f, 1f, 1f, 1f, true, 0),
            ("hard", 1.5f, 1.25f, 1.1f, 1.25f, false, 30),
            ("nightmare", 2.2f, 1.6f, 1.2f, 1.6f, false, 80),
        };

        foreach (var e in expected)
        {
            var asset = System.Array.Find(assets, a => a != null && a.id == e.id);
            if (asset == null)
            {
                Check(false, "缺少资产 " + e.id);
                continue;
            }
            Check(Mathf.Approximately(asset.enemyHealthMultiplier, e.hp)
                && Mathf.Approximately(asset.enemyDamageMultiplier, e.dmg)
                && Mathf.Approximately(asset.enemySpeedMultiplier, e.speed)
                && Mathf.Approximately(asset.coinMultiplier, e.coin)
                && asset.unlockedByDefault == e.unlocked
                && asset.unlockCost == e.cost
                && !string.IsNullOrEmpty(asset.displayName),
                asset.id + " 数值正确（HP " + asset.enemyHealthMultiplier
                + " / 伤害 " + asset.enemyDamageMultiplier
                + " / 速度 " + asset.enemySpeedMultiplier
                + " / 金币 " + asset.coinMultiplier
                + " / 默认解锁 " + asset.unlockedByDefault
                + " / 价格 " + asset.unlockCost + "）");
        }

        if (errors.Count == 0)
            Debug.Log("[DIFFICULTY][GEN] 生成完成并通过校验：" + pass + " 项");
        else
            Debug.LogError("[DIFFICULTY][GEN] 生成结束，但校验失败 " + errors.Count + " 项："
                + string.Join("；", errors));
    }
}
