using UnityEngine;

namespace RpgGame
{
    // 应用层启动：在任何场景加载前创建常驻的 GameFlow。
    public static class AppBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (GameFlow.Instance != null) return;
            if (Object.FindFirstObjectByType<GameFlow>() != null) return;

            var root = new GameObject("[RPG App]");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<GameFlow>();
        }
    }
}
