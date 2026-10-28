using UnityEngine;

// 永久升级影响的属性类别。数值含义：
// - Flat 类（生命/护甲）每级加固定值
// - Pct 类（伤害/移速/暴击/经验/拾取）每级加比例，例如 0.05 = +5%
public enum MetaStatType
{
    MaxHealthFlat = 0,
    DamagePct = 1,
    MoveSpeedPct = 2,
    ArmorFlat = 3,
    CritChanceFlat = 4,
    ExpGainPct = 5,
    PickupRadiusPct = 6,
}

// 永久升级数据：一份资产 = 商店里的一项可购买升级。
// 等级与累计金币一起存在 PlayerPrefs，跨局生效。
[CreateAssetMenu(fileName = "MetaUpgrade", menuName = "Survivor/Meta Upgrade")]
public class MetaUpgradeData : ScriptableObject
{
    public string id;                    // 唯一标识，PlayerPrefs 等级键的依据
    public string displayName;
    [TextArea] public string description;
    public int maxLevel = 5;             // 最高等级
    public int baseCost = 10;            // 第 1 级价格
    public float costGrowth = 1.6f;      // 每级价格增长倍率
    public MetaStatType stat;            // 影响的属性
    public float valuePerLevel = 0.05f;  // 每级加成（Flat 为固定值，Pct 为比例）
}
