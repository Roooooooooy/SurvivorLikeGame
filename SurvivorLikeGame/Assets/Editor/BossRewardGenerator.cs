using UnityEditor;
using UnityEngine;

// 4b 演示配方生成器：Boss 材料进化（Orb）。
// 只走 AssetDatabase，不手写 YAML；可重复执行（同名资产覆盖数值）。
public static class BossRewardGenerator
{
    const string EvolutionsDir = "Assets/Resources/Evolutions";
    const string RecipePath = EvolutionsDir + "/evo_boss_core_orbs.asset";

    [MenuItem("Tools/Survivor/Generate Boss Evolution Recipe")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(EvolutionsDir))
        {
            Debug.LogError("[4b][GEN] 缺少 " + EvolutionsDir + " 目录，生成中止");
            return;
        }

        // requiredUpgradeId 必须是 Resources/Upgrades 里真实存在的 id
        const string requiredId = "orb_damage";
        var upgrades = Resources.LoadAll<UpgradeData>("Upgrades");
        bool idExists = false;
        foreach (var u in upgrades)
            if (u != null && u.id == requiredId) { idExists = true; break; }
        if (!idExists)
        {
            Debug.LogError("[4b][GEN] 升级池里找不到 id=" + requiredId + "，生成中止");
            return;
        }

        var recipe = AssetDatabase.LoadAssetAtPath<EvolutionRecipe>(RecipePath);
        bool isNew = recipe == null;
        if (isNew) recipe = ScriptableObject.CreateInstance<EvolutionRecipe>();

        recipe.baseWeapon = WeaponTarget.Orb;
        recipe.requiredUpgradeId = requiredId;
        recipe.requiredBossMaterials = 1;
        recipe.displayName = "Boss Core Orbs";
        recipe.description = "Orbs fused with a boss core: double damage, faster orbit, +2 orbs (costs 1 boss material)";
        recipe.damageMultiplier = 2f;
        recipe.areaMultiplier = 1.5f;
        recipe.speedMultiplier = 1.6f;
        recipe.bonusCount = 2;

        if (isNew) AssetDatabase.CreateAsset(recipe, RecipePath);
        else EditorUtility.SetDirty(recipe);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = recipe;
        Debug.Log("[4b][GEN] 已生成 " + RecipePath + "（" + recipe.displayName
            + "，材料 " + recipe.requiredBossMaterials + "，升级条件 " + requiredId + "）");
    }
}
