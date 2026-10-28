using UnityEngine;

// 角色数据：初始武器、初始属性加成与被动描述。
// 一份资产 = 一个可选角色；选择结果存 PlayerPrefs，新局开始时应用。
[CreateAssetMenu(fileName = "Character", menuName = "Survivor/Character")]
public class CharacterData : ScriptableObject
{
    public string id;                    // 唯一标识，PlayerPrefs 选择键的依据
    public string displayName;
    [TextArea] public string description;
    [TextArea] public string passiveDescription;

    [Header("初始武器")]
    public WeaponTarget initialWeapon = WeaponTarget.Orb;

    [Header("初始属性加成")]
    public int maxHealthFlat;
    public float damagePct;
    public float moveSpeedPct;
    public float armorFlat;
    public float critChanceFlat;
    public float pickupRadiusPct;
    public float expGainPct;
    [Range(0f, 0.8f)] public float cooldownReductionFlat;
}
