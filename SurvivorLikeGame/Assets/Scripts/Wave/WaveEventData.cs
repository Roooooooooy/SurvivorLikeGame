using UnityEngine;

// 波次事件类型
public enum WaveEventType
{
    Elite,        // 玩家周围环形生成一批精英
    SurroundTide, // 玩家周围均匀生成一圈普通敌人
    TimedElite,   // 生成精英并开启限时击杀窗口，达标奖励经验
    Boss,         // 单只超大精英（多阶段 Boss 的占位）
}

// 波次事件数据：一份资产 = 时间线上一个到点触发的事件。
// 节奏在 Resources/WaveEvents 下配置，运行时由 WaveEventScheduler 读取。
[CreateAssetMenu(fileName = "WaveEvent", menuName = "Survivor/Wave Event")]
public class WaveEventData : ScriptableObject
{
    [Header("类型与时间")]
    public WaveEventType eventType;
    public float triggerTime;        // 开局第几秒触发

    [Header("生成")]
    public int enemyTypeIndex;       // 对应 EnemyTypes.asset 的类型下标
    public int count = 1;            // 生成数量
    public float radius = 12f;       // 环绕玩家的生成半径

    [Header("横幅")]
    public string bannerTitle = "波次事件";
    public string bannerSubtitle;    // 可为空
    public float duration = 3f;      // 横幅停留秒数；TimedElite 时同时作为击杀窗口时长

    [Header("TimedElite")]
    public int bonusExp;             // 窗口结束达标时奖励的经验
    public int requiredKills;        // 窗口内需要击杀的数量
}
