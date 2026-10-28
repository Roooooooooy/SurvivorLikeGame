using UnityEngine;

// 玩家属性中枢：所有加成类升级统一作用在这里，武器/玩家在运行时读取。
public class PlayerStats : MonoBehaviour
{
    [Header("进攻")]
    [Range(0f, 1f)] public float critChance = 0.05f;
    public float critMultiplier = 2f;
    [Range(0f, 0.8f)] public float cooldownReduction = 0f;
    public float damageMultiplier = 1f;
    public float areaMultiplier = 1f;

    [Header("防御")]
    public int armor = 0;

    [Header("功能")]
    public float expMultiplier = 1f;
    public float moveSpeedMultiplier = 1f;

    // 兼容旧调用：不关心暴击标记时直接返回伤害
    public int RollDamage(int baseDamage) => RollDamage(baseDamage, out _);

    // 计算一次伤害：全局加成 + 暴击判定，并输出是否暴击供表现层使用。
    // 只做一次随机判定，数值与概率和旧实现完全一致。
    public int RollDamage(int baseDamage, out bool crit)
    {
        crit = Random.value < critChance;

        float dmg = baseDamage * damageMultiplier;
        if (crit) dmg *= critMultiplier;
        return Mathf.Max(1, Mathf.RoundToInt(dmg));
    }
}
