using UnityEngine;

// 局内进度：目前只记录 Boss 掉落的进化材料。
// 纯静态、不做持久化——每局开始由 BossReward 重置，后续 Meta 成长再接入存档。
public static class PlayerProgress
{
    public static int BossMaterials { get; private set; }

    // 关闭 Domain Reload 进入 Play 时静态字段不会自动清空，这里显式重置
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        BossMaterials = 0;
    }

    public static void AddBossMaterial(int amount)
    {
        if (amount <= 0) return;
        BossMaterials += amount;
    }

    // 材料足够时扣除并返回 true；不足时不扣除，返回 false
    public static bool ConsumeBossMaterials(int amount)
    {
        if (amount <= 0) return true;
        if (BossMaterials < amount) return false;
        BossMaterials -= amount;
        return true;
    }

    // 新的一局开始：清空材料
    public static void Reset()
    {
        BossMaterials = 0;
    }
}
