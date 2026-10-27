using UnityEngine;

// W1 新武器公共基类：
// 1) 在 Awake 里把 target 设为子类的 WeaponTarget，供升级池归类/匹配；
// 2) 通过 LevelUp(UpgradeStat) 显式识别“数量”升级：
//    管理器对非“法球/飞弹”武器不会直接改字段，只会调用 LevelUp。
public abstract class AdvancedWeaponBase : WeaponBase
{
    // WeaponCountFlat 实际生效的次数（每选一次 +1）
    protected int countUpgrades;

    // 调试/验证用计数，不参与游戏逻辑，也不序列化进 prefab
    [System.NonSerialized] public int attackCount;
    [System.NonSerialized] public int lastHitCount;

    // 子类返回自己的升级归类
    protected abstract WeaponTarget WeaponType { get; }

    public int CountUpgrades => countUpgrades;

    protected virtual void Awake()
    {
        target = WeaponType;
    }

    public override void LevelUp(UpgradeStat stat)
    {
        if (stat == UpgradeStat.WeaponCountFlat) countUpgrades++;
        base.LevelUp(stat);
    }
}
