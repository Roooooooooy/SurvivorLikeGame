using UnityEngine;

namespace RpgGame
{
    // 局内会话：与 RPG 永久角色分离。退出 Run 后由 RunResult 结算，不把局内等级/武器带出去。
    public static class RunSession
    {
        public static RunConfig Config { get; private set; }
        public static int Kills { get; private set; }
        public static int Coins { get; private set; }
        public static int Exp { get; private set; }

        public static bool Active => Config != null;

        public static void Begin(RunConfig config)
        {
            Config = config;
            Kills = 0;
            Coins = 0;
            Exp = 0;
        }

        public static void AddKill()
        {
            if (Config != null) Kills++;
        }

        public static void AddCoins(int amount)
        {
            if (Config != null) Coins += Mathf.Max(0, amount);
        }

        public static void AddExp(int amount)
        {
            if (Config != null) Exp += Mathf.Max(0, amount);
        }

        public static RunResult End(bool success)
        {
            var result = new RunResult
            {
                success = success,
                kills = Kills,
                coins = Coins,
                exp = Exp,
            };

            // Phase 0 临时桥接：旧 RunStats 已经记录了本局的击杀/金币。
            // Phase 1 改为 RunSession 自己订阅拾取事件，删除这条依赖。
            var summary = global::RunStats.Current;
            if (summary.TimeSeconds > 0f) result.duration = summary.TimeSeconds;
            if (result.kills == 0) result.kills = summary.Kills;
            if (result.coins == 0) result.coins = summary.CoinsEarned;

            Config = null;
            return result;
        }

        public static void Clear()
        {
            Config = null;
            Kills = 0;
            Coins = 0;
            Exp = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();
    }
}
