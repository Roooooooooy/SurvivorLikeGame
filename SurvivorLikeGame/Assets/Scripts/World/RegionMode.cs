namespace RpgGame
{
    // 区域玩法模式：进入区域时决定玩家启用哪套能力。
    public enum RegionMode
    {
        ActionRPG = 0,    // 自由探索 + 即时动作战斗
        SurvivorRift = 1  // 幸存者子玩法
    }
}
