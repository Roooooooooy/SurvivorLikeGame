using UnityEngine;

// 难度数据：敌人血量/伤害/速度与金币收益的全局倍率。
// 一份资产 = 一个难度档位；选择结果存 PlayerPrefs，新局开始时由 EnemySimulation 读取。
[CreateAssetMenu(fileName = "Difficulty", menuName = "Survivor/Difficulty")]
public class DifficultyData : ScriptableObject
{
    public string id;                    // 唯一标识，PlayerPrefs 选择/解锁键的依据
    public string displayName;
    [TextArea] public string description;

    [Header("敌人倍率")]
    public float enemyHealthMultiplier = 1f;
    public float enemyDamageMultiplier = 1f;
    public float enemySpeedMultiplier = 1f;

    [Header("收益")]
    public float coinMultiplier = 1f;

    [Header("解锁")]
    public bool unlockedByDefault = true;
    public int unlockCost = 0;
}
