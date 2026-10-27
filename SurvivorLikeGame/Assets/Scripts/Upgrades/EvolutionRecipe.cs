using UnityEngine;

// 武器进化配方：基础武器满级 + 持有指定升级后，在升级界面作为进化选项出现
[CreateAssetMenu(fileName = "EvolutionRecipe", menuName = "Survivor/Evolution Recipe")]
public class EvolutionRecipe : ScriptableObject
{
    public WeaponTarget baseWeapon;      // 需要满级的基础武器
    public string requiredUpgradeId;     // 需要拥有过的升级 id（充当"被动"条件）
    public int requiredBossMaterials = 0; // 额外需要消耗的 Boss 进化材料（0 = 旧配方，无材料门槛）
    public string displayName;           // 进化后名称
    [TextArea] public string description;

    [Header("进化加成")]
    public float damageMultiplier = 2f;
    public float areaMultiplier = 1.5f;
    public float speedMultiplier = 1.5f;
    public int bonusCount = 1;
}
