using UnityEngine;

// 一局结束时的统计快照：结算界面与存档系统共用。
public struct RunSummary
{
    public float TimeSeconds;   // 本局时长
    public int Kills;           // 击杀数
    public int CoinsEarned;     // 本局获得金币
    public int TotalCoins;      // 结算后累计金币
}
