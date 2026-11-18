using UnityEngine;

namespace RpgGame
{
    // 快跑和闪避共用一份体力；耗尽锁定只能在完全恢复时解除。
    [DisallowMultipleComponent]
    public class PlayerStamina : MonoBehaviour
    {
        [Min(1f)] public float maxStamina = 100f;
        [Min(0f)] public float sprintCostPerSecond = 20f;
        [Min(0f)] public float dodgeCost = 25f;
        [Min(0f)] public float recoveryDelay = 1.5f;
        [Min(0.01f)] public float recoveryPerSecond = 25f;
        [Min(0.01f)] public float exhaustedRecoveryPerSecond = 20f;

        public float Current { get; private set; }
        public bool IsExhausted { get; private set; }
        public bool IsRecovering { get; private set; }
        public float Normalized => Mathf.Clamp01(Current / Mathf.Max(1f, maxStamina));
        public bool CanUseAbilities => isActiveAndEnabled && !IsExhausted
            && Current > 0f && (health == null || !health.IsDead);

        PlayerHealth health;
        bool consumedThisFrame;
        float lastConsumedTime = float.NegativeInfinity;

        void Awake()
        {
            health = GetComponent<PlayerHealth>();
            Current = Mathf.Max(1f, maxStamina);
        }

        // 快跑允许用完最后一点体力；耗尽的这一帧立即退回普通移动。
        public bool TrySprint(float deltaTime)
        {
            if (!CanUseAbilities || deltaTime <= 0f) return false;
            Consume(sprintCostPerSecond * deltaTime);
            return !IsExhausted;
        }

        // 闪避必须付清一次消耗；失败时不扣体力、不进入冷却。
        public bool TryDodge()
        {
            if (!CanUseAbilities || Current < dodgeCost) return false;
            Consume(dodgeCost);
            return true;
        }

        void Consume(float amount)
        {
            if (amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            lastConsumedTime = Time.time;
            consumedThisFrame = true;
            IsRecovering = false;
            if (Current <= 0f) IsExhausted = true;
        }

        // 在移动/闪避都处理完后恢复，避免同一帧先恢复再消耗的执行顺序差异。
        void LateUpdate()
        {
            if (health != null && health.IsDead) return;
            if (consumedThisFrame)
            {
                consumedThisFrame = false;
                return;
            }

            float maximum = Mathf.Max(1f, maxStamina);
            IsRecovering = Current < maximum && (IsExhausted
                || Time.time - lastConsumedTime >= recoveryDelay);
            if (!IsRecovering || Time.deltaTime <= 0f) return;

            float rate = IsExhausted ? exhaustedRecoveryPerSecond : recoveryPerSecond;
            Current = Mathf.Min(maximum, Current + rate * Time.deltaTime);
            if (Current >= maximum)
            {
                IsExhausted = false;
                IsRecovering = false;
            }
        }
    }
}
