using System.Collections.Generic;
using UnityEngine;

public enum EnemyBehavior
{
    Chase = 0,     // 追踪 + 接触伤害
    Exploder = 1,  // 贴近玩家后引燃，延时范围爆炸
}

// 敌人类型数据：所有数值在 EnemyTypes 资产（Resources/EnemyTypes.asset）里配置
[System.Serializable]
public class EnemyTypeData
{
    public string typeName;
    public Sprite sprite;
    public Color color = Color.white;
    public float scale = 1f;
    public float moveSpeed = 3f;
    public int maxHealth = 30;
    public int damage = 10;
    public int expValue = 1;
    public EnemyBehavior behavior = EnemyBehavior.Chase;
    public float explodeTriggerRange = 1.5f;
    public float explodeRadius = 2.5f;
    public float fuseTime = 0.6f;
}

[CreateAssetMenu(fileName = "EnemyTypes", menuName = "Survivor/Enemy Type Table")]
public class EnemyTypeTable : ScriptableObject
{
    public GameObject expGemPrefab;
    public List<EnemyTypeData> types = new List<EnemyTypeData>();
}
