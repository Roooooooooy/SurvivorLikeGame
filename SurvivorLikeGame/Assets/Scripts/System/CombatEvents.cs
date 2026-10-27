using UnityEngine;

// 战斗事件总线：玩法层（伤害、击杀、拾取）只负责发事件，
// 表现层（音效、伤害数字、震屏等）自行订阅，避免互相直接依赖。
// 事件参数统一为世界坐标；订阅方必须在 OnDisable/OnDestroy 里退订。
public static class CombatEvents
{
    public static event System.Action<Vector2, int> Hit;    // 命中：位置、伤害
    public static event System.Action<Vector2, int> Kill;   // 死亡：位置、经验值
    public static event System.Action<Vector2, int> Pickup; // 拾取：位置、经验值

    public static void RaiseHit(Vector2 pos, int damage) => Hit?.Invoke(pos, damage);
    public static void RaiseKill(Vector2 pos, int expValue) => Kill?.Invoke(pos, expValue);
    public static void RaisePickup(Vector2 pos, int expValue) => Pickup?.Invoke(pos, expValue);
}
