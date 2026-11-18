using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RpgGame
{
    // Phase 0 调试 HUD：血量、交互提示、操作说明。
    public class DebugHud : MonoBehaviour
    {
        WorldInteractor interactor;
        GUIStyle staminaLabel;
        GUIStyle staminaStatus;

        void Start()
        {
            interactor = FindFirstObjectByType<WorldInteractor>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 调试：F9 把本区域宝箱恢复成未打开，方便重复验证开箱。
            if (keyboard.f9Key.wasPressedThisFrame)
                Chest.ResetAllOpened();

            // 调试：Ctrl+R 清空测试存档并重进当前区域。
            // 加修饰键避免误触；宝箱、背包、装备和角色成长都会回到初始状态。
            if (keyboard.ctrlKey.isPressed && keyboard.rKey.wasPressedThisFrame)
                ResetTestSave();
        }

        void ResetTestSave()
        {
            Debug.Log("[Debug] Ctrl+R 重置测试存档");
            SaveSystem.Delete();

            var flow = GameFlow.Instance;
            var root = FindFirstObjectByType<RegionRoot>();
            if (flow != null && root != null)
            {
                flow.LoadRegion(root.regionId);
                return;
            }

            // 直接 Play 区域场景、没有 GameFlow 时的兜底。
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void OnGUI()
        {
            var player = PlayerHealth.Player;
            if (player != null)
            {
                GUI.Label(new Rect(16f, 16f, 320f, 24f),
                    "HP " + player.currentHealth + " / " + player.maxHealth);
                var stamina = player.GetComponent<PlayerStamina>();
                if (stamina != null) DrawStamina(stamina);
            }

            if (interactor != null && interactor.Current != null)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 120f, Screen.height - 80f, 360f, 24f),
                    "[E] " + interactor.Current.Prompt);
            }

            GUI.Label(new Rect(16f, Screen.height - 32f, 1200f, 24f),
                "WASD move   Shift sprint   J/LMB 连拳 (转向/间隔1秒重置)   Space dodge   E interact   " +
                "F9 reset chest   Ctrl+R reset test save");
        }

        void DrawStamina(PlayerStamina stamina)
        {
            if (staminaLabel == null)
            {
                staminaLabel = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                staminaLabel.normal.textColor = Color.white;
                staminaStatus = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
                staminaStatus.normal.textColor = new Color(0.9f, 0.92f, 0.95f);
            }

            float width = Mathf.Min(284f, Screen.width - 32f);
            Color previous = GUI.color;
            GUI.color = new Color(0.04f, 0.06f, 0.09f, 0.9f);
            GUI.DrawTexture(new Rect(12f, 45f, width + 8f, 76f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(24f, 49f, width - 16f, 23f),
                "体力  " + Mathf.FloorToInt(stamina.Current) + " / " + Mathf.RoundToInt(stamina.maxStamina), staminaLabel);

            Rect bar = new Rect(24f, 76f, width - 16f, 12f);
            GUI.color = new Color(0.18f, 0.22f, 0.27f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = stamina.IsExhausted ? new Color(1f, 0.62f, 0.28f)
                : new Color(0.3f, 0.83f, 0.78f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * stamina.Normalized, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string status = stamina.IsExhausted ? "耗尽恢复中 · 回满后可快跑 / 闪避"
                : stamina.IsRecovering ? "恢复中"
                : stamina.Current < stamina.maxStamina ? "停止消耗后恢复" : "Shift 快跑 · 空格闪避";
            GUI.Label(new Rect(24f, 93f, width - 16f, 23f), status, staminaStatus);
            GUI.color = previous;
        }
    }
}
