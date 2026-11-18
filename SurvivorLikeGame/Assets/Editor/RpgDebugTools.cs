using System.IO;
using UnityEditor;
using UnityEngine;
using RpgGame;

namespace RpgGameEditor
{
    public static class RpgDebugTools
    {
        [MenuItem("工具/角色扮演/截取游戏画面")]
        public static void CaptureGameScreenshot()
        {
            string path = Path.Combine(Path.GetTempPath(), "rpg_gameview.png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[RPG] 截图写入 " + path);
        }

        [MenuItem("工具/角色扮演/调试/切换自动攻击")]
        public static void ToggleAutoAttack()
        {
            var combat = Object.FindFirstObjectByType<ActionCombatController>();
            if (combat == null)
            {
                Debug.LogWarning("[RPG] 场景里没有 ActionCombatController（需要先 Play）");
                return;
            }

            combat.debugAutoAttack = !combat.debugAutoAttack;
            Debug.Log("[RPG] 自动攻击: " + (combat.debugAutoAttack ? "开" : "关"));
        }

        // Play 模式下按指定世界方向出拳，方便核对某个方向有没有接上攻击素材。
        [MenuItem("工具/角色扮演/调试/向西南攻击")]
        public static void AttackSouthWest() => AttackDirection(new Vector2(-1f, -1f));

        [MenuItem("工具/角色扮演/调试/向东南攻击")]
        public static void AttackSouthEast() => AttackDirection(new Vector2(1f, -1f));

        static void AttackDirection(Vector2 direction)
        {
            var combat = Object.FindFirstObjectByType<ActionCombatController>();
            if (combat == null)
            {
                Debug.LogWarning("[RPG] 场景里没有 ActionCombatController（需要先 Play）");
                return;
            }

            var motor = combat.GetComponent<PlayerMotor>();
            if (motor == null)
            {
                Debug.LogWarning("[RPG] 玩家没有 PlayerMotor");
                return;
            }

            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

            // Facing 是公开属性但 setter 私有，借用反射写入。
            typeof(PlayerMotor).GetProperty("Facing")?.SetValue(motor, direction.normalized);

            // 攻击方向现在以像素主角显示朝向为准，调试时也要同步写入 dir。
            var pixel = Object.FindFirstObjectByType<Hd2dPixelPlayer>();
            if (pixel != null)
            {
                float degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                int index = Mathf.RoundToInt(degrees / 45f);
                index = (index % 8 + 8) % 8;
                typeof(Hd2dPixelPlayer).GetField("dir", Flags)?.SetValue(pixel, index);
            }

            typeof(ActionCombatController).GetMethod("StartAttack", Flags)?.Invoke(combat, null);

            Debug.Log("[RPG] 定向攻击: " + direction.normalized);
        }

        static int burstLeft;
        static double burstNext;

        [MenuItem("工具/角色扮演/连续截图 6 秒")]
        public static void CaptureBurst()
        {
            burstLeft = 60;
            burstNext = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BurstUpdate;
            EditorApplication.update += BurstUpdate;
            Debug.Log("[RPG] 连拍 6 秒开始");
        }

        static void BurstUpdate()
        {
            if (EditorApplication.timeSinceStartup < burstNext) return;

            burstNext = EditorApplication.timeSinceStartup + 0.1;
            int index = 60 - burstLeft;
            string path = Path.Combine(Path.GetTempPath(), "rpg_burst_" + index.ToString("00") + ".png");
            ScreenCapture.CaptureScreenshot(path);

            burstLeft--;
            if (burstLeft <= 0)
            {
                EditorApplication.update -= BurstUpdate;
                Debug.Log("[RPG] 连拍结束");
            }
        }

        [MenuItem("工具/角色扮演/调试/治疗玩家")]
        public static void HealPlayer()
        {
            var hp = PlayerHealth.Player;
            if (hp == null)
            {
                Debug.LogWarning("[RPG] 玩家还没生成（需要先 Play）");
                return;
            }

            hp.Heal(9999);
            hp.SetInvulnerable(120f);
            Debug.Log("[RPG] 玩家回满并 120 秒无敌");
        }

        [MenuItem("工具/角色扮演/调试/切换慢动作")]
        public static void ToggleSlowMotion()
        {
            Time.timeScale = Time.timeScale > 0.5f ? 0.2f : 1f;
            Debug.Log("[RPG] Time.timeScale = " + Time.timeScale);
        }

        [MenuItem("工具/角色扮演/调试/玩家无敌 60 秒")]
        public static void InvinciblePlayer()
        {
            var hp = PlayerHealth.Player;
            if (hp == null)
            {
                Debug.LogWarning("[RPG] 玩家还没生成（需要先 Play）");
                return;
            }

            hp.SetInvulnerable(60f);
            Debug.Log("[RPG] 玩家 60 秒无敌（调试）");
        }

        [MenuItem("工具/角色扮演/调试/循环固定拳段")]
        public static void CycleForcedStep()
        {
            var combat = Object.FindFirstObjectByType<ActionCombatController>();
            if (combat == null)
            {
                Debug.LogWarning("[RPG] 场景里没有 ActionCombatController（需要先 Play）");
                return;
            }

            int count = Mathf.Max(1, combat.StepCount);
            combat.debugForcedStep = combat.debugForcedStep >= count - 1
                ? -1
                : combat.debugForcedStep + 1;
            Debug.Log("[RPG] 固定拳段: " + (combat.debugForcedStep < 0 ? "循环" : combat.debugForcedStep.ToString()));
        }

        [MenuItem("工具/角色扮演/调试/冻结敌人")]
        public static void FreezeEnemies()
        {
            var brains = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
            for (int i = 0; i < brains.Length; i++)
            {
                brains[i].enabled = false;
                var body = brains[i].GetComponent<Rigidbody2D>();
                if (body != null) body.linearVelocity = Vector2.zero;
            }
            Debug.Log("[RPG] 已冻结敌人 " + brains.Length + " 只");
        }

        [MenuItem("工具/角色扮演/调试/触发闪避")]
        public static void TriggerDodge()
        {
            var combat = Object.FindFirstObjectByType<ActionCombatController>();
            if (combat == null)
            {
                Debug.LogWarning("[RPG] 场景里没有 ActionCombatController（需要先 Play）");
                return;
            }

            bool ok = combat.RequestDodge();
            Debug.Log("[RPG] 闪避" + (ok ? "触发成功" : "失败（冷却/体力/锁定中）"));
        }

        [MenuItem("工具/角色扮演/调试/预览闪避动画")]
        public static void PreviewDodge()
        {
            var pixel = Object.FindFirstObjectByType<Hd2dPixelPlayer>();
            if (pixel == null)
            {
                Debug.LogWarning("[RPG] 需要先 Play");
                return;
            }

            pixel.DebugPlayDodge();
            Debug.Log("[RPG] 预览闪避动画");
        }

        [MenuItem("工具/角色扮演/调试/预览死亡动画")]
        public static void PreviewDeath()
        {
            var pixel = Object.FindFirstObjectByType<Hd2dPixelPlayer>();
            if (pixel == null)
            {
                Debug.LogWarning("[RPG] 需要先 Play");
                return;
            }

            pixel.DebugPlayDeath();
            Debug.Log("[RPG] 预览死亡动画");
        }

        [MenuItem("工具/角色扮演/调试/预览死亡消散")]
        public static void PreviewDeathDissolve()
        {
            var pixel = Object.FindFirstObjectByType<Hd2dPixelPlayer>();
            if (pixel == null)
            {
                Debug.LogWarning("[RPG] 需要先 Play");
                return;
            }

            pixel.DebugPlayDeathDissolve(0.5f);
            Debug.Log("[RPG] 预览死亡消散（50%）");
        }

        [MenuItem("工具/角色扮演/调试/预览怪物死亡消散")]
        public static void PreviewEnemyDeathDissolve()
        {
            var view = Object.FindFirstObjectByType<DirectionalEnemyView>();
            if (view == null)
            {
                Debug.LogWarning("[RPG] 场景里没有 DirectionalEnemyView（需要先 Play）");
                return;
            }

            view.DebugPreviewDeathDissolve(0.5f);
            Debug.Log("[RPG] 预览怪物死亡消散（50%）");
        }

        [MenuItem("工具/角色扮演/调试/击杀玩家")]
        public static void KillPlayer()
        {
            var health = PlayerHealth.Player;
            if (health == null)
            {
                Debug.LogWarning("[RPG] 玩家还没生成（需要先 Play）");
                return;
            }

            // 调试：清掉无敌帧，确保能立刻看到死亡动画。
            var field = typeof(PlayerHealth).GetField("invincibleUntil",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            if (field != null) field.SetValue(health, 0f);

            int before = health.currentHealth;
            health.TakeDamage(new DamageInfo
            {
                amount = 9999,
                hitPoint = health.transform.position,
                sourcePosition = health.transform.position + Vector3.right,
                source = null,
            });
            Debug.Log("[RPG] 玩家 HP " + before + " → " + health.currentHealth +
                      (health.IsDead ? "（死亡）" : "（未生效）"));
        }

        [MenuItem("工具/角色扮演/调试/击杀最近的哥布林")]
        public static void KillNearestGoblin()
        {
            var enemies = Object.FindObjectsByType<ActionEnemy>(FindObjectsSortMode.None);
            ActionEnemy nearest = null;
            float best = float.MaxValue;
            var health = PlayerHealth.Player;
            Vector3 center = health != null ? health.transform.position : Vector3.zero;

            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.IsDead) continue;
                if (!enemy.name.Contains("Goblin")) continue;

                float distance = (enemy.transform.position - center).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = enemy;
                }
            }

            if (nearest == null)
            {
                Debug.LogWarning("[RPG] 没有可击杀的哥布林（需要先 Play）");
                return;
            }

            nearest.TakeDamage(new DamageInfo
            {
                amount = 9999,
                hitPoint = nearest.transform.position,
                sourcePosition = center,
                source = health != null ? health.gameObject : null,
            });
            Debug.Log("[RPG] 击杀 " + nearest.name);
        }

        [MenuItem("工具/角色扮演/调试/传送到宝箱")]
        public static void TeleportToChest()
        {
            var motor = Object.FindFirstObjectByType<PlayerMotor>();
            if (motor == null) { Debug.LogWarning("[RPG] 需要先 Play"); return; }
            motor.Teleport(new Vector3(-1.9f, -1.5f, 0f));
            Debug.Log("[RPG] 已传送到宝箱旁");
        }

        [MenuItem("工具/角色扮演/调试/传送到传送门")]
        public static void TeleportToPortal()
        {
            var motor = Object.FindFirstObjectByType<PlayerMotor>();
            if (motor == null) { Debug.LogWarning("[RPG] 需要先 Play"); return; }
            motor.Teleport(new Vector3(0f, 3.5f, 0f));
            Debug.Log("[RPG] 已传送到传送门前");
        }

        [MenuItem("工具/角色扮演/调试/打开最近的宝箱")]
        public static void OpenNearestChest()
        {
            var chest = Object.FindFirstObjectByType<Chest>();
            if (chest == null)
            {
                Debug.LogWarning("[RPG] 场景里没有 Chest（需要先 Play）");
                return;
            }

            chest.Interact();
        }

        [MenuItem("工具/角色扮演/调试/重置所有宝箱")]
        public static void ResetChests()
        {
            Chest.ResetAllOpened();
        }
    }
}
