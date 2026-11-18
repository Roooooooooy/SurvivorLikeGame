using System.Collections.Generic;
using UnityEngine;

namespace RpgGame
{
    // 攻击判定盒：攻击帧内开启；同一次攻击对同一目标只结算一次。
    [RequireComponent(typeof(Collider2D))]
    public class Hitbox : MonoBehaviour
    {
        public int damage = 10;
        public float knockback = 1f;
        public LayerMask targetLayers = ~0;
        public GameObject owner;

        readonly HashSet<int> hitTargets = new HashSet<int>();
        readonly List<Collider2D> overlaps = new List<Collider2D>(16);
        Collider2D trigger;
        bool initialized;

        void Awake()
        {
            // 初始隐藏的判定盒会在第一次 Activate 后才 Awake。
            // Activate 已完成初始化时不能再把本次攻击的判定关掉。
            if (initialized) return;
            EnsureInitialized();
            gameObject.SetActive(false);
        }

        public void Activate()
        {
            EnsureInitialized();
            hitTargets.Clear();
            gameObject.SetActive(true);
            Physics2D.SyncTransforms();
            ScanTargets();
        }

        public void Deactivate()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        void EnsureInitialized()
        {
            if (initialized) return;

            trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            initialized = true;
        }

        void OnTriggerEnter2D(Collider2D other) => TryHit(other);
        void OnTriggerStay2D(Collider2D other) => TryHit(other);

        // 静止刚体休眠后不会持续派发触发回调；每个有效物理帧主动查询重叠。
        // 与触发回调共用 hitTargets，整段攻击对每个目标仍只结算一次。
        void FixedUpdate() => ScanTargets();

        void ScanTargets()
        {
            if (trigger == null || !trigger.enabled) return;
            var filter = new ContactFilter2D();
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            int count = trigger.Overlap(filter, overlaps);
            for (int i = 0; i < count; i++) TryHit(overlaps[i]);
        }

        void TryHit(Collider2D other)
        {
            if (other == null) return;
            if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform))) return;
            if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

            var target = other.GetComponentInParent<IDamageable>();
            var targetComponent = target as Component;
            if (target == null || targetComponent == null || target.IsDead) return;

            int id = targetComponent.GetInstanceID();
            if (!hitTargets.Add(id)) return;

            Vector2 source = owner != null
                ? (Vector2)owner.transform.position
                : (Vector2)transform.position;

            Vector2 direction = (Vector2)other.transform.position - source;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;

            target.TakeDamage(new DamageInfo
            {
                amount = damage,
                hitPoint = other.ClosestPoint(transform.position),
                sourcePosition = source,
                knockback = direction.normalized * knockback,
                source = owner,
            });
        }
    }
}
