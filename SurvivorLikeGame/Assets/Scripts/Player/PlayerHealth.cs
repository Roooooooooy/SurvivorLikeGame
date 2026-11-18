using System;
using System.Collections;
using UnityEngine;

namespace RpgGame
{
    // 动作区玩家生命：护甲减伤、无敌帧、受击闪红、死亡通知 GameFlow。
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public static PlayerHealth Player { get; private set; }

        public int maxHealth = 100;
        public int currentHealth;
        public float invincibleDuration = 0.5f;
        public float deathDelay = 1.2f;

        public bool IsDead => currentHealth <= 0;

        public event Action<int, int> HealthChanged;
        public event Action Damaged;
        public event Action Died;

        float invincibleUntil;
        SpriteRenderer view;
        Color baseColor;
        bool died;

        void Awake()
        {
            if (Player == null) Player = this;

            // GameFlow 的 sceneLoaded 回调早于 Start，此时必须保证 IsDead 为 false，
            // 否则 PlayerModeRig 会在初始化阶段把移动关掉。
            currentHealth = Mathf.Max(1, maxHealth);

            view = GetComponentInChildren<SpriteRenderer>();
            if (view != null) baseColor = view.color;
        }

        void Start()
        {
            ActionHitParticles.Prepare();
            var stats = CharacterStats.Current;
            if (stats.maxHealth > 0)
                maxHealth = Mathf.Max(1, stats.maxHealth);

            currentHealth = maxHealth;
            HealthChanged?.Invoke(currentHealth, maxHealth);
            EquipmentService.Changed += OnEquipmentChanged;
        }

        void OnDestroy()
        {
            EquipmentService.Changed -= OnEquipmentChanged;
            if (Player == this) Player = null;
        }

        // 穿脱装备后重算生命上限；按当前血量比例保留，避免换一件生命装备就回满。
        void OnEquipmentChanged()
        {
            var stats = CharacterStats.Current;
            if (stats.maxHealth <= 0) return;

            int oldMax = Mathf.Max(1, maxHealth);
            float ratio = Mathf.Clamp01((float)currentHealth / oldMax);
            maxHealth = Mathf.Max(1, stats.maxHealth);

            if (!IsDead)
                currentHealth = Mathf.Clamp(Mathf.RoundToInt(maxHealth * ratio), 1, maxHealth);

            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || Time.time < invincibleUntil) return;

            int defense = CharacterStats.Current.defense;
            int damage = Mathf.Max(1, info.amount - defense);
            currentHealth = Mathf.Max(0, currentHealth - damage);
            invincibleUntil = Time.time + invincibleDuration;

            HealthChanged?.Invoke(currentHealth, maxHealth);
            CombatEvents.RaiseHit(info.hitPoint, damage, info.crit);
            ActionHitParticles.Spawn(info.hitPoint, info.sourcePosition, true, info.crit);
            Damaged?.Invoke();
            StartCoroutine(Flash());

            if (currentHealth <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void SetInvulnerable(float seconds)
        {
            invincibleUntil = Mathf.Max(invincibleUntil, Time.time + Mathf.Max(0f, seconds));
        }

        IEnumerator Flash()
        {
            if (view == null) yield break;

            view.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            view.color = baseColor;
        }

        void Die()
        {
            if (died) return;
            died = true;

            var motor = GetComponent<PlayerMotor>();
            if (motor != null) motor.SetControlEnabled(false);

            Died?.Invoke();

            // 等死亡动画 + 消散都播完再切场景，避免角色硬切消失。
            float delay = deathDelay;
            if (Hd2dPixelPlayer.Instance != null)
                delay = Mathf.Max(delay, Hd2dPixelPlayer.Instance.TotalDeathDuration);

            if (GameFlow.Instance != null)
                GameFlow.Instance.NotifyPlayerDied(delay);
        }
    }
}
