using UnityEditor;
using UnityEngine;

// 角色资产生成器：在 Resources/Characters 下生成 3 个可选角色。
// 只走 AssetDatabase，不手写 YAML；可重复执行（同名资产覆盖数值）。
public static class CharacterGenerator
{
    const string CharactersDir = "Assets/Resources/Characters";

    [MenuItem("Tools/Survivor/Generate Characters")]
    public static void Generate()
    {
        if (!EnsureFolder()) return;

        CreateCharacter("balanced", "均衡者",
            "攻守兼备的新手角色，适合所有武器流派。",
            "初始携带环绕法球，生命与伤害小幅提升。",
            WeaponTarget.Orb, maxHealthFlat: 10, damagePct: 0.05f, moveSpeedPct: 0f,
            armorFlat: 0f, critChanceFlat: 0f, pickupRadiusPct: 0f, expGainPct: 0f,
            cooldownReductionFlat: 0f);

        CreateCharacter("glass_cannon", "玻璃大炮",
            "以生命换取爆发伤害，容错率极低。",
            "初始携带飞弹，伤害大幅提升、暴击率提高，但生命上限降低。",
            WeaponTarget.Projectile, maxHealthFlat: -20, damagePct: 0.35f, moveSpeedPct: 0.05f,
            armorFlat: 0f, critChanceFlat: 0.1f, pickupRadiusPct: 0f, expGainPct: 0f,
            cooldownReductionFlat: 0f);

        CreateCharacter("tank", "重装者",
            "高生命高护甲，移动稍慢但生存能力极强。",
            "初始携带环绕法球，生命上限与护甲提升，拾取范围扩大。",
            WeaponTarget.Orb, maxHealthFlat: 50, damagePct: 0f, moveSpeedPct: -0.1f,
            armorFlat: 3f, critChanceFlat: 0f, pickupRadiusPct: 0.2f, expGainPct: 0f,
            cooldownReductionFlat: 0f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
    }

    static bool EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(CharactersDir)) return true;
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            Debug.LogError("[CHARACTER][GEN] 缺少 Assets/Resources 目录，生成中止");
            return false;
        }
        AssetDatabase.CreateFolder("Assets/Resources", "Characters");
        return AssetDatabase.IsValidFolder(CharactersDir);
    }

    static void CreateCharacter(string id, string displayName, string description, string passiveDescription,
        WeaponTarget initialWeapon, int maxHealthFlat, float damagePct, float moveSpeedPct,
        float armorFlat, float critChanceFlat, float pickupRadiusPct, float expGainPct,
        float cooldownReductionFlat)
    {
        string path = CharactersDir + "/" + id + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        bool isNew = asset == null;
        if (isNew) asset = ScriptableObject.CreateInstance<CharacterData>();

        asset.id = id;
        asset.displayName = displayName;
        asset.description = description;
        asset.passiveDescription = passiveDescription;
        asset.initialWeapon = initialWeapon;
        asset.maxHealthFlat = maxHealthFlat;
        asset.damagePct = damagePct;
        asset.moveSpeedPct = moveSpeedPct;
        asset.armorFlat = armorFlat;
        asset.critChanceFlat = critChanceFlat;
        asset.pickupRadiusPct = pickupRadiusPct;
        asset.expGainPct = expGainPct;
        asset.cooldownReductionFlat = cooldownReductionFlat;

        if (isNew) AssetDatabase.CreateAsset(asset, path);
        else EditorUtility.SetDirty(asset);
    }

    // 生成后自检：数量、id 唯一、关键数值符合设计表
    static void Validate()
    {
        var assets = Resources.LoadAll<CharacterData>("Characters");
        int pass = 0;
        var errors = new System.Collections.Generic.List<string>();

        void Check(bool ok, string msg)
        {
            if (ok) { pass++; Debug.Log("[CHARACTER][GEN][PASS] " + msg); }
            else { errors.Add(msg); Debug.LogError("[CHARACTER][GEN][FAIL] " + msg); }
        }

        Check(assets.Length == 3, "Resources/Characters 共 3 个资产，实际 " + assets.Length);

        var expected = new (string id, WeaponTarget weapon, int hp, float dmg, float move,
            float armor, float crit, float pickup)[]
        {
            ("balanced", WeaponTarget.Orb, 10, 0.05f, 0f, 0f, 0f, 0f),
            ("glass_cannon", WeaponTarget.Projectile, -20, 0.35f, 0.05f, 0f, 0.1f, 0f),
            ("tank", WeaponTarget.Orb, 50, 0f, -0.1f, 3f, 0f, 0.2f),
        };

        foreach (var e in expected)
        {
            var asset = System.Array.Find(assets, a => a != null && a.id == e.id);
            if (asset == null)
            {
                Check(false, "缺少资产 " + e.id);
                continue;
            }
            Check(string.IsNullOrEmpty(asset.displayName) == false
                && asset.initialWeapon == e.weapon
                && asset.maxHealthFlat == e.hp
                && Mathf.Approximately(asset.damagePct, e.dmg)
                && Mathf.Approximately(asset.moveSpeedPct, e.move)
                && Mathf.Approximately(asset.armorFlat, e.armor)
                && Mathf.Approximately(asset.critChanceFlat, e.crit)
                && Mathf.Approximately(asset.pickupRadiusPct, e.pickup),
                asset.id + " 数值正确（" + asset.initialWeapon + " / HP " + asset.maxHealthFlat
                + " / 伤害 " + asset.damagePct + " / 移速 " + asset.moveSpeedPct
                + " / 护甲 " + asset.armorFlat + " / 暴击 " + asset.critChanceFlat
                + " / 拾取 " + asset.pickupRadiusPct + "）");
        }

        if (errors.Count == 0)
            Debug.Log("[CHARACTER][GEN] 生成完成并通过校验：" + pass + " 项");
        else
            Debug.LogError("[CHARACTER][GEN] 生成结束，但校验失败 " + errors.Count + " 项："
                + string.Join("；", errors));
    }
}
