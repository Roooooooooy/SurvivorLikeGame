using System.Collections;
using UnityEngine;

namespace RpgGame
{
    // Phase 0 临时桥：把旧 SampleScene 幸存者玩法接到 GameFlow/RunSession 上。
    // Phase 1 用正式的 RunController 替换。
    public class SurvivorRunBridge : MonoBehaviour
    {
        public float autoExitAfterDeath = 3f;
        public bool autoExitOnDeath = true;

        RunConfig config;
        bool finishing;
        float gameOverAt = -1f;

        public void Bind(RunConfig runConfig)
        {
            config = runConfig;
            if (!RunSession.Active) RunSession.Begin(runConfig);
        }

        void OnEnable() => CombatEvents.Kill += OnKill;
        void OnDisable() => CombatEvents.Kill -= OnKill;

        void Start()
        {
            StartCoroutine(StartSurvivorNextFrame());
        }

        IEnumerator StartSurvivorNextFrame()
        {
            // GameManager.Start 会在场景加载时把 timeScale 设为 0 并显示标题页；
            // 等一帧后强制开局，保证从 RPG 区域进入时直接进战斗。
            yield return null;

            var manager = GameManager.Instance;
            if (manager != null) manager.StartGame();
        }

        void OnKill(Vector2 position, int exp)
        {
            RunSession.AddKill();
        }

        void Update()
        {
            if (config == null || finishing) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
            {
                Finish(true); // 调试：提前成功结束本局
                return;
            }

            if (!autoExitOnDeath) return;

            var manager = GameManager.Instance;
            if (manager == null || !manager.isGameOver) return;

            if (gameOverAt < 0f) gameOverAt = Time.unscaledTime;
            if (Time.unscaledTime - gameOverAt >= autoExitAfterDeath)
                Finish(false);
        }

        void Finish(bool success)
        {
            if (finishing) return;
            finishing = true;

            var result = RunSession.End(success);
            if (GameFlow.Instance != null)
                GameFlow.Instance.ExitSurvivorRun(result);
        }
    }
}
