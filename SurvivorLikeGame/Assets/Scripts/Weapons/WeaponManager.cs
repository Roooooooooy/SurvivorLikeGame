using UnityEngine;
using System.Collections.Generic;

// 升级系统：从 Resources/Upgrades 加载 UpgradeData 资产，按权重抽 3 个供选择；
// 武器满级且满足进化条件时，进化选项优先出现。
public class WeaponManager : MonoBehaviour
{
    [Header("Weapons")]
    public List<WeaponBase> activeWeapons = new List<WeaponBase>();

    private List<UpgradeData> upgradePool;
    private List<EvolutionRecipe> recipes;
    private readonly Dictionary<string, int> stacks = new Dictionary<string, int>();
    private List<UpgradeOption> pendingOptions;

    void Start()
    {
        activeWeapons = new List<WeaponBase>(GetComponentsInChildren<WeaponBase>());
        FilterByCharacterWeapon();
        upgradePool = new List<UpgradeData>(Resources.LoadAll<UpgradeData>("Upgrades"));
        recipes = new List<EvolutionRecipe>(Resources.LoadAll<EvolutionRecipe>("Evolutions"));
        if (upgradePool.Count == 0)
            Debug.LogError("WeaponManager: Resources/Upgrades 下没有找到任何 UpgradeData 资产");
    }

    // 按角色初始武器过滤：只保留匹配项，其余隐藏并移出升级池。
    // 一个都不匹配时保留全部，避免角色配置错误导致玩家完全无武器。
    void FilterByCharacterWeapon()
    {
        var character = CharacterSelection.Selected;
        if (character == null) return;

        var matched = new List<WeaponBase>();
        foreach (var weapon in activeWeapons)
            if (weapon != null && TargetOf(weapon) == character.initialWeapon)
                matched.Add(weapon);

        if (matched.Count == 0) return;

        foreach (var weapon in activeWeapons)
        {
            if (weapon == null || matched.Contains(weapon)) continue;
            weapon.gameObject.SetActive(false);
        }

        activeWeapons = matched;
    }

    public void ShowUpgradeOptions()
    {
        pendingOptions = BuildOptions(3);
        if (GameManager.Instance) GameManager.Instance.ShowLevelUp(pendingOptions);
    }

    public void SelectUpgrade(int index)
    {
        if (pendingOptions == null || index >= pendingOptions.Count) return;

        var option = pendingOptions[index];
        if (option.IsEvolution)
        {
            // 进化时扣除 Boss 材料；0 材料门槛的旧配方行为不变
            if (!PlayerProgress.ConsumeBossMaterials(option.evolution.requiredBossMaterials)) return;
            option.evolveTarget.Evolve(option.evolution);
        }
        else
            ApplyUpgrade(option.upgrade);

        if (GameManager.Instance) GameManager.Instance.HideLevelUp();
    }

    // 构造升级选项：进化优先，其余按权重不重复抽取
    List<UpgradeOption> BuildOptions(int count)
    {
        var options = new List<UpgradeOption>();

        foreach (var weapon in activeWeapons)
        {
            if (weapon.IsMaxLevel && !weapon.evolved)
            {
                foreach (var recipe in recipes)
                {
                    if (recipe.baseWeapon != TargetOf(weapon)) continue;
                    if (!stacks.ContainsKey(recipe.requiredUpgradeId)) continue;
                    // 材料不足时，需要 Boss 材料的进化选项不出现
                    if (PlayerProgress.BossMaterials < recipe.requiredBossMaterials) continue;

                    string label = "EVOLVE: " + recipe.displayName;
                    // 需要材料时在名字后标出进度（升级 UI 只显示 displayName）
                    if (recipe.requiredBossMaterials > 0)
                        label += "（材料 " + PlayerProgress.BossMaterials + "/" + recipe.requiredBossMaterials + "）";

                    options.Add(new UpgradeOption
                    {
                        displayName = label,
                        description = recipe.description,
                        evolution = recipe,
                        evolveTarget = weapon,
                    });
                }
            }
        }

        var candidates = new List<UpgradeData>();
        foreach (var u in upgradePool)
            if (IsAvailable(u)) candidates.Add(u);

        while (options.Count < count && candidates.Count > 0)
        {
            var picked = WeightedPick(candidates);
            candidates.Remove(picked);
            options.Add(new UpgradeOption
            {
                displayName = picked.displayName,
                description = picked.description,
                upgrade = picked,
            });
        }

        return options;
    }

    static WeaponTarget TargetOf(WeaponBase weapon)
    {
        if (weapon is OrbWeapon) return WeaponTarget.Orb;
        if (weapon is ProjectileWeapon) return WeaponTarget.Projectile;
        return weapon.target;
    }

    // 是否已经持有某种武器（按类型判重，同类武器只允许一把）
    public bool HasWeaponType(System.Type type)
    {
        foreach (var w in activeWeapons)
            if (w != null && w.GetType() == type) return true;
        return false;
    }

    // 获得新武器：实例化到玩家身上并注册。新武器从 1 级开始，不追溯已选的通用加成。
    public WeaponBase GrantWeapon(GameObject prefab)
    {
        if (prefab == null) return null;
        var source = prefab.GetComponent<WeaponBase>();
        if (source == null)
        {
            Debug.LogError("WeaponManager: WeaponGrant 的 prefab 上没有 WeaponBase: " + prefab.name);
            return null;
        }
        if (HasWeaponType(source.GetType())) return null;

        var go = Instantiate(prefab, transform);
        go.name = prefab.name;
        var granted = go.GetComponent<WeaponBase>();
        activeWeapons.Add(granted);
        return granted;
    }

    List<WeaponBase> TargetWeapons(WeaponTarget target)
    {
        var result = new List<WeaponBase>();
        foreach (var w in activeWeapons)
            if (target == WeaponTarget.All || TargetOf(w) == target)
                result.Add(w);
        return result;
    }

    // 达到堆叠上限、或武器已满级/已进化的对应升级不再出现
    bool IsAvailable(UpgradeData data)
    {
        if (stacks.TryGetValue(data.id, out int n) && n >= data.maxStack) return false;

        foreach (var e in data.effects)
        {
            if (e.stat == UpgradeStat.WeaponGrant)
            {
                if (e.weaponPrefab == null) return false;
                var granted = e.weaponPrefab.GetComponent<WeaponBase>();
                if (granted != null && HasWeaponType(granted.GetType())) return false;
                continue;
            }
            if (e.stat < UpgradeStat.WeaponDamagePct) continue; // 非武器类效果
            var targets = TargetWeapons(e.weapon);
            if (targets.Count == 0) return false;
            bool allMaxed = true;
            foreach (var w in targets)
                if (!w.IsMaxLevel && !w.evolved) { allMaxed = false; break; }
            if (allMaxed) return false;
        }
        return true;
    }

    static UpgradeData WeightedPick(List<UpgradeData> candidates)
    {
        int total = 0;
        foreach (var c in candidates) total += c.weight;
        int roll = Random.Range(0, total);
        foreach (var c in candidates)
        {
            roll -= c.weight;
            if (roll < 0) return c;
        }
        return candidates[candidates.Count - 1];
    }

    void ApplyUpgrade(UpgradeData data)
    {
        stacks[data.id] = stacks.TryGetValue(data.id, out int n) ? n + 1 : 1;
        foreach (var e in data.effects) ApplyEffect(e);
    }

    void ApplyEffect(UpgradeEffect e)
    {
        var player = GetComponent<PlayerController>();
        var stats = GetComponent<PlayerStats>();
        var pickup = GetComponent<ExpPickupRadius>();

        switch (e.stat)
        {
            case UpgradeStat.MoveSpeedPct:
                if (stats) stats.moveSpeedMultiplier *= 1f + e.value;
                break;
            case UpgradeStat.MaxHealthFlat:
                if (player) { player.maxHealth += (int)e.value; player.currentHealth += (int)e.value; }
                break;
            case UpgradeStat.PickupRadiusPct:
                if (pickup) pickup.radius *= 1f + e.value;
                break;
            case UpgradeStat.ArmorFlat:
                if (stats) stats.armor += (int)e.value;
                break;
            case UpgradeStat.CritChanceFlat:
                if (stats) stats.critChance = Mathf.Min(1f, stats.critChance + e.value);
                break;
            case UpgradeStat.CritDamageFlat:
                if (stats) stats.critMultiplier += e.value;
                break;
            case UpgradeStat.ExpGainPct:
                if (stats) stats.expMultiplier *= 1f + e.value;
                break;
            case UpgradeStat.AreaPct:
                if (stats) stats.areaMultiplier *= 1f + e.value;
                break;
            case UpgradeStat.CooldownReductionFlat:
                if (stats) stats.cooldownReduction = Mathf.Min(0.8f, stats.cooldownReduction + e.value);
                break;
            case UpgradeStat.DamagePct:
                if (stats) stats.damageMultiplier *= 1f + e.value;
                break;
            case UpgradeStat.WeaponDamagePct:
                foreach (var w in TargetWeapons(e.weapon))
                {
                    w.damage = Mathf.RoundToInt(w.damage * (1f + e.value));
                    w.LevelUp(e.stat);
                }
                break;
            case UpgradeStat.WeaponCountFlat:
                foreach (var w in TargetWeapons(e.weapon))
                {
                    if (w is OrbWeapon orb) orb.AddOrb();
                    else if (w is ProjectileWeapon proj) proj.projectileCount += (int)e.value;
                    w.LevelUp(e.stat);
                }
                break;
            case UpgradeStat.WeaponSpeedPct:
                foreach (var w in TargetWeapons(e.weapon))
                {
                    if (w is OrbWeapon orb) orb.rotationSpeed *= 1f + e.value;
                    else if (w is ProjectileWeapon proj) proj.projectileSpeed *= 1f + e.value;
                    w.LevelUp(e.stat);
                }
                break;
            case UpgradeStat.WeaponIntervalPct:
                foreach (var w in TargetWeapons(e.weapon))
                {
                    w.attackInterval *= 1f - e.value;
                    w.LevelUp(e.stat);
                }
                break;
            case UpgradeStat.WeaponGrant:
                GrantWeapon(e.weaponPrefab);
                break;
        }
    }
}
