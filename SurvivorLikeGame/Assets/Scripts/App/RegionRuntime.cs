using UnityEngine;

namespace RpgGame
{
    // 当前运行时处于哪种玩法。幸存者的自动引导系统（刷怪/障碍/压测）只允许在 SurvivorRun 下运行。
    public enum RuntimeMode
    {
        Menu = 0,
        ActionRegion = 1,
        SurvivorRun = 2,
    }

    public static class RegionRuntime
    {
        public static RuntimeMode Current { get; private set; } = RuntimeMode.Menu;

        public static bool IsAction => Current == RuntimeMode.ActionRegion;
        public static bool IsSurvivor => Current == RuntimeMode.SurvivorRun;

        public static void Set(RuntimeMode mode) => Current = mode;

        public static void SetFromRegion(RegionMode mode)
            => Current = mode == RegionMode.SurvivorRift ? RuntimeMode.SurvivorRun : RuntimeMode.ActionRegion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = RuntimeMode.Menu;
    }
}
