using System.Collections.Generic;
using UnityEngine;

// 数值显式固定，序列化资产按 int 存储，插入新枚举时不要改动已有值
public enum UpgradeStat
{
    MoveSpeedPct = 0,          // 移速（乘算）
    MaxHealthFlat = 1,         // 生命上限（加算，同时回血）
    PickupRadiusPct = 2,       // 拾取范围（乘算）
    ArmorFlat = 3,             // 护甲（固定减伤）
    CritChanceFlat = 4,        // 暴击率（加算，0~1）
    CritDamageFlat = 5,        // 暴击倍率（加算）
    ExpGainPct = 6,            // 经验获取（乘算）
    AreaPct = 7,               // 攻击范围（乘算）
    CooldownReductionFlat = 8, // 冷却缩减（加算，上限 0.8）
    DamagePct = 9,             // 全局伤害（乘算）
    WeaponDamagePct = 10,      // 指定武器伤害（乘算）
    WeaponCountFlat = 11,      // 指定武器数量（加算）
    WeaponSpeedPct = 12,       // 指定武器速度（乘算：法球转速/飞弹速度）
    WeaponIntervalPct = 13,    // 指定武器攻击间隔（乘算缩减）
    WeaponGrant = 14,          // 获得新武器：使用 UpgradeEffect.weaponPrefab
}

public enum WeaponTarget
{
    All = 0,
    Orb = 1,
    Projectile = 2,
    Pierce = 3,      // 穿透箭
    Lightning = 4,   // 闪电链
    Nova = 5,        // 新星爆发
    Boomerang = 6,   // 回旋镖
}

[System.Serializable]
public class UpgradeEffect
{
    public UpgradeStat stat;
    public float value;
    public WeaponTarget weapon = WeaponTarget.All; // 仅武器类效果使用
    public GameObject weaponPrefab;                // 仅 WeaponGrant 使用：要授予的武器 prefab
}

[CreateAssetMenu(fileName = "UpgradeData", menuName = "Survivor/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    public string id;                    // 唯一标识：堆叠计数与进化条件的依据
    public string displayName;
    [TextArea] public string description;
    [Range(1, 100)] public int weight = 10; // 抽取权重
    public int maxStack = 5;             // 最多可选次数
    public List<UpgradeEffect> effects = new List<UpgradeEffect>();
}

// 升级界面上的一个可选项：普通升级或武器进化
public class UpgradeOption
{
    public string displayName;
    public string description;
    public UpgradeData upgrade;       // 普通升级
    public EvolutionRecipe evolution; // 进化（此时 evolveTarget 有效）
    public WeaponBase evolveTarget;

    public bool IsEvolution => evolution != null;
}
