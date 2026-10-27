using System.Collections.Generic;
using UnityEngine;

public enum EnemyBehavior
{
    Chase = 0,     // 追踪 + 接触伤害
    Exploder = 1,  // 贴近玩家后引燃，延时范围爆炸
    Ranged = 2,    // 远程：保持射程边缘，周期发射子弹
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

    [Header("远程（behavior = Ranged 时生效）")]
    public float attackRange = 7f;        // 开火距离；也是敌人试图保持的距离
    public float fireInterval = 2f;       // 开火间隔（秒）
    public float projectileSpeed = 6f;    // 子弹飞行速度
    public int projectileDamage = 8;      // 子弹命中伤害

    [Header("动画")]
    public int animFrames = 1;      // 图集横向帧数，1 = 静态
    public float animFps = 10f;
    public bool hasDeathAnim = false; // 图集第 2 行是死亡动画（行走=第 1 行）
}

[CreateAssetMenu(fileName = "EnemyTypes", menuName = "Survivor/Enemy Type Table")]
public class EnemyTypeTable : ScriptableObject
{
    public GameObject expGemPrefab;
    public GameObject projectilePrefab; // Ranged 敌人子弹的预制体（EnemyProjectile）
    public List<EnemyTypeData> types = new List<EnemyTypeData>();
}
