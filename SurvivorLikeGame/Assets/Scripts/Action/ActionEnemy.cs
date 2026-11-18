using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace RpgGame
{
    // 动作区敌人：少量、有生命和受击反馈，不使用 EnemySimulation。
    [RequireComponent(typeof(Rigidbody2D))]
    public class ActionEnemy : MonoBehaviour, IDamageable
    {
        public int maxHealth = 30;
        public int currentHealth;
        public float knockbackResistance = 0.75f;
        [Tooltip("至少保留尸体的时长；有死亡动画时会取动画时长 + deathLinger")]
        public float deathDelay = 0.35f;
        public float deathLinger = 0.5f;
        [Tooltip("没有 DirectionalEnemyView 的敌人（如史莱姆）用材质抖动消散的时长")]
        public float deathFadeDuration = 0.65f;
        public int expReward = 10;

        public event Action<ActionEnemy> Died;

        public bool IsDead => currentHealth <= 0;

        Rigidbody2D body;
        SpriteRenderer spriteView;
        DirectionalEnemyView animator;
        EnemyBrain brain;
        Color baseColor;
        bool dying;
        Material deathMaterial;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteView = GetComponentInChildren<SpriteRenderer>();
            animator = GetComponent<DirectionalEnemyView>();
            brain = GetComponent<EnemyBrain>();
            if (spriteView != null) baseColor = spriteView.color;
            currentHealth = maxHealth;
        }

        void Start()
        {
            // 组件可能在 Awake 之后才补齐（例如场景搭建脚本）。
            if (brain == null) brain = GetComponent<EnemyBrain>();
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead) return;

            int amount = Mathf.Max(1, info.amount);
            currentHealth = Mathf.Max(0, currentHealth - amount);

            CombatEvents.RaiseHit(info.hitPoint, amount, info.crit);
            ActionHitParticles.Spawn(info.hitPoint, info.sourcePosition, false, info.crit);

            if (body != null && info.knockback.sqrMagnitude > 0.0001f)
                body.AddForce(info.knockback * (1f - Mathf.Clamp01(knockbackResistance)), ForceMode2D.Impulse);

            // 受击动画按伤害来源方向播放；来源缺失时由 EnemyBrain 用当前朝向兜底。
            int hitDirection = -1;
            Vector2 toSource = info.sourcePosition - (Vector2)transform.position;
            if (toSource.sqrMagnitude > 0.0001f)
                hitDirection = DirectionalEnemyView.DirectionFromWorld(toSource);

            StartCoroutine(Flash());
            if (brain != null) brain.OnDamaged(hitDirection);

            if (currentHealth <= 0) Die();
        }

        IEnumerator Flash()
        {
            if (spriteView == null) yield break;

            spriteView.color = Color.red;
            yield return new WaitForSeconds(0.08f);
            // 死亡时也要恢复，避免尸体一直发红。
            spriteView.color = baseColor;
        }

        void Die()
        {
            if (dying) return;
            dying = true;

            CombatEvents.RaiseKill(transform.position, expReward);

            if (brain != null) brain.enabled = false;
            if (body != null) body.linearVelocity = Vector2.zero;

            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;

            // 有逐帧动画的敌人由 view 播死亡 + 消散；其余敌人用材质抖动消散。
            float deathDuration = animator != null
                ? animator.PlayDeath(animator.Facing)
                : StartGenericDissolve();

            Died?.Invoke(this);
            Destroy(gameObject, Mathf.Max(deathDelay, deathDuration + deathLinger));
        }

        // 没有逐帧死亡动画的敌人（史莱姆）：换 HD2D/SpriteLit 材质做快速抖动消散 + 尘土爆发。
        float StartGenericDissolve()
        {
            if (spriteView == null || spriteView.sprite == null) return 0f;

            var shader = Shader.Find("HD2D/SpriteLit");
            if (shader == null) return 0f;

            var mat = new Material(shader)
            {
                name = "EnemyDissolve (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            mat.SetTexture("_MainTex", spriteView.sprite.texture);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_AmbientBoost", 0.8f);
            mat.SetFloat("_OutlineWidth", 0f);
            mat.SetFloat("_Dissolve", 0f);
            mat.SetInt("_ZTest", 4);
            mat.SetInt("_ZWrite", 0);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = 3000;

            deathMaterial = mat;
            spriteView.sharedMaterial = mat;

            // 桥可能已经用旧材质建好代理，强制同步一次。
            if (Hd2dSpriteBridge.Instance != null)
                Hd2dSpriteBridge.Instance.RefreshMaterial(spriteView);

            StartCoroutine(DissolveRoutine(mat, Mathf.Max(0.05f, deathFadeDuration)));
            return deathFadeDuration;
        }

        IEnumerator DissolveRoutine(Material mat, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && mat != null)
            {
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / duration);
                mat.SetFloat("_Dissolve", raw * raw * (3f - 2f * raw));
                yield return null;
            }

            if (mat != null) mat.SetFloat("_Dissolve", 1f);
            if (spriteView != null) spriteView.enabled = false;
        }

        void OnDestroy()
        {
            if (deathMaterial != null) Destroy(deathMaterial);
        }
    }
}
