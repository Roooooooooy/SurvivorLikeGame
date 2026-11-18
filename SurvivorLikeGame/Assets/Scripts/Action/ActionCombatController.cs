using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RpgGame
{
    // 一段平A：时序、判定框、刀光样式全部按段配置。
    // 判定框和刀光共用 hitboxCenterDistance / hitboxSize，保证视觉范围和实际命中范围一致。
    [System.Serializable]
    public class ComboStep
    {
        public string name = "Slash";
        public float damageMultiplier = 1f;

        [Header("时序（秒）")]
        public float windup = 0.08f;
        public float active = 0.12f;
        public float recovery = 0.16f;
        [Tooltip("纯侧面素材的出拳峰值比正面晚；负数表示使用 windup")]
        public float horizontalWindup = -1f;
        [Range(0f, 1f)] public float moveMultiplier = 0.35f;

        [Header("判定（矩形，沿攻击方向）")]
        public float hitboxCenterDistance = 0.7f;
        public Vector2 hitboxSize = new Vector2(1.3f, 0.9f);

        [Header("刀光")]
        public SlashStyle slashStyle = SlashStyle.Arc;
        public Color slashColor = new Color(1f, 0.95f, 0.75f, 0.95f);
        [Tooltip("在“刀光外缘 = 判定前缘”的基础上再乘一个美术系数")]
        public float slashScaleMultiplier = 1f;
        public float slashLifetime = 0.16f;
        public bool slashFlipY;
        [Tooltip("刀光贴图在攻击方向基础上的额外旋转（度），用来对齐每段素材的主朝向")]
        public float slashRotationOffset;
        [Tooltip("刀光在世界中的抬升高度（HD-2D 下是 billboard 中心离地高度）")]
        public float slashHeight = 0.85f;
    }

    // 动作区玩家攻击/闪避。
    // 每次输入打一段：左拳 -> 右拳 -> 上勾拳；攻击中可缓冲下一次输入。
    [RequireComponent(typeof(PlayerMotor))]
    public class ActionCombatController : MonoBehaviour
    {
        [Header("攻击")]
        public int damage = 10;
        [Min(0.01f)] public float comboResetDelay = 1f;
        // 连击序列只来自代码，避免场景里的旧数据覆盖最新配置。
        [System.NonSerialized] public ComboStep[] combo;
        public Hitbox hitbox;

        // 连击总段数（每次点击只打一段）。
        public int StepCount => combo != null ? combo.Length : 0;
        public bool IsAttacking => attacking;
        public int CurrentStep { get; private set; } = -1;
        public float AttackElapsed => attacking ? Time.time - attackStartedAt : 0f;
        public float AttackDuration => CurrentStep >= 0 && combo != null
            ? combo[CurrentStep].windup + combo[CurrentStep].active + combo[CurrentStep].recovery
            : 0f;

        [Header("调试")]
        [HideInInspector] public bool debugAutoAttack;
        [HideInInspector] public int debugForcedStep = -1;
        float debugNextAttack;

        [Header("闪避")]
        public float dodgeSpeed = 12f;
        public float dodgeDuration = 0.22f;
        public float dodgeCooldown = 0.6f;
        public float dodgeInvincible = 0.25f;

        // 表现层可订阅（HD-2D 像素主角用它做攻击/闪避反馈，不直接依赖输入）。
        // 每段开始时通知实际段号和锁定方向，动画与判定共用同一段时序。
        public event System.Action<Vector2> AttackStarted;
        public event System.Action<int, Vector2> AttackStepStarted;
        public event System.Action DodgeStarted;

        PlayerMotor motor;
        PlayerHealth health;
        BoxCollider2D hitboxCollider;
        float nextDodgeTime;
        float dodgeUntil;
        bool attacking;
        int nextComboStep;
        float lastAttackInputTime = float.NegativeInfinity;
        float attackStartedAt;
        bool attackQueued;
        float queuedInputTime;
        Vector2 queuedDirection;
        int observedFacing = -1;
        Coroutine attackRoutine;
        Vector2 attackDirection = Vector2.down;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            motor.FacingChanged += OnFacingChanged;
            health = GetComponent<PlayerHealth>();
            if (health != null) health.Damaged += InterruptAttack;

            if (hitbox != null)
            {
                hitbox.owner = gameObject;
                hitboxCollider = hitbox.GetComponent<BoxCollider2D>();
            }

            // combo 不参与序列化，运行时始终用 BuildPunchSequence 的最新配置。
            ResetCombo();
            observedFacing = DirectionIndex(motor.Facing);
        }

        void Start()
        {
            damage = Mathf.Max(1, CharacterStats.Current.attack);
            EquipmentService.Changed += OnEquipmentChanged;

            if (hitbox != null) hitbox.owner = gameObject;
        }

        void OnDestroy()
        {
            EquipmentService.Changed -= OnEquipmentChanged;
            if (motor != null) motor.FacingChanged -= OnFacingChanged;
            if (health != null) health.Damaged -= InterruptAttack;
        }

        void OnEquipmentChanged()
        {
            damage = Mathf.Max(1, CharacterStats.Current.attack);
        }

        void Update()
        {
            if (health != null && health.IsDead) return;
            if (EquipmentScreen.IsOpen) return;

            // 同一帧内闪避优先，避免攻击协程和闪避同时启动。
            if (WantsDodge())
            {
                TryDodge();
                return;
            }

            if (WantsAttack()) RequestAttack();

            // 调试：按相同输入入口循环三段连击。
            if (debugAutoAttack && !attacking && Time.time >= debugNextAttack)
            {
                // 调试模式尽量连续出手，方便在慢动作里截到 active 帧。
                debugNextAttack = Time.time + 0.15f;
                StartAttack();
            }
        }

        bool WantsAttack()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var gamepad = Gamepad.current;

            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && keyboard.jKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.buttonWest.wasPressedThisFrame);
        }

        static bool WantsDodge()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            return (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }

        public void RequestAttack()
        {
            if (!isActiveAndEnabled || (health != null && health.IsDead)
                || EquipmentScreen.IsOpen || Time.time < dodgeUntil) return;

            OnFacingChanged(motor.InputFacing);
            Vector2 direction = ResolveAttackDirection();

            if (attacking)
            {
                // 只保留下一次输入，避免快速连点后无输入仍自动打完整套。
                if (!attackQueued)
                {
                    attackQueued = true;
                    queuedInputTime = Time.time;
                    queuedDirection = direction;
                }
                return;
            }
            BeginAttack(Time.time, direction);
        }

        // 保留旧调试菜单的入口；它也必须遵守输入缓冲和闪避限制。
        void StartAttack() => RequestAttack();

        void BeginAttack(float inputTime, Vector2 direction)
        {
            if (combo == null || combo.Length == 0) ResetCombo();
            // 比较两次普攻输入的时间，不从收招时重新计时。
            if (inputTime - lastAttackInputTime >= comboResetDelay) nextComboStep = 0;
            if (DirectionIndex(direction) != DirectionIndex(attackDirection)) nextComboStep = 0;
            CurrentStep = debugForcedStep >= 0 ? debugForcedStep % combo.Length : nextComboStep;
            nextComboStep = (CurrentStep + 1) % combo.Length;
            lastAttackInputTime = inputTime;

            attacking = true;
            attackStartedAt = Time.time;
            attackDirection = direction;
            AttackStarted?.Invoke(attackDirection);
            AttackStepStarted?.Invoke(CurrentStep, attackDirection);
            attackRoutine = StartCoroutine(AttackSequence(combo[CurrentStep], attackDirection));
        }

        Vector2 ResolveAttackDirection()
        {
            // 有移动意图时，以本次输入方向出手；事件会同步切换身体朝向。
            // 尤其是攻击锁住移动期间，缓冲输入不能沿用上一拳的方向。
            if (motor.HasDirectionalInput) return motor.InputFacing;
            // 以像素主角当前显示的 8 方向为准。斜向松键有 0.1s 防抖、转身还有过渡，
            // 这期间 motor.Facing 可能已经收回正方向，直接用它会出现
            // “画面上朝东南/西南、攻击却朝南”的错位。
            if (Hd2dPixelPlayer.Instance != null
                && Hd2dPixelPlayer.Instance.TryGetVisualFacing(out Vector2 visual)
                && visual.sqrMagnitude > 0.01f)
                return visual.normalized;

            return motor.Facing.sqrMagnitude > 0.01f
                ? motor.Facing.normalized
                : Vector2.down;
        }

        // 一次输入只结算当前一拳；收招后消费至多一个缓冲输入。
        IEnumerator AttackSequence(ComboStep step, Vector2 direction)
        {
            motor.SpeedMultiplier = Mathf.Clamp(step.moveMultiplier, 0f, 1f);
            AimHitbox(step, direction);
            float windup = Mathf.Abs(direction.y) < 0.01f && step.horizontalWindup >= 0f
                ? step.horizontalWindup : step.windup;
            float activeEnd = windup + step.active;
            float duration = step.windup + step.active + step.recovery;
            bool activated = false;
            // 与视图共用出手时间，避免多次 WaitForSeconds 的帧取整累计推迟命中。
            while (Time.time - attackStartedAt < duration)
            {
                float elapsed = Time.time - attackStartedAt;
                if (!activated && elapsed >= windup && elapsed < activeEnd)
                {
                    activated = true;
                    if (step.slashLifetime > 0f) SpawnSlash(step, direction);
                    if (hitbox != null) hitbox.Activate();
                }
                if (activated && elapsed >= activeEnd && hitbox != null) hitbox.Deactivate();
                yield return null;
            }
            if (hitbox != null) hitbox.Deactivate();
            motor.SpeedMultiplier = 1f;
            attacking = false;
            attackRoutine = null;
            if (attackQueued)
            {
                attackQueued = false;
                if ((health == null || !health.IsDead) && !EquipmentScreen.IsOpen
                    && Time.time >= dodgeUntil)
                    BeginAttack(queuedInputTime, queuedDirection);
            }
        }

        void AimHitbox(ComboStep step, Vector2 direction)
        {
            if (hitbox == null) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            hitbox.transform.localPosition = direction * step.hitboxCenterDistance;
            hitbox.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (hitboxCollider != null) hitboxCollider.size = step.hitboxSize;
            hitbox.damage = Mathf.Max(1, Mathf.RoundToInt(damage * step.damageMultiplier));
        }

        // 刀光：以角色为中心，外缘对齐判定框前缘。
        // 拳击：命中点在判定框中心，冲击大小按判定框半宽反推。
        void SpawnSlash(ComboStep step, Vector2 direction)
        {
            bool punch = SlashArcEffect.IsPunch(step.slashStyle);
            float localRadius = Mathf.Max(0.01f, SlashArcEffect.GetLocalRadius(step.slashStyle));

            Vector3 position;
            float scale;
            if (punch)
            {
                position = transform.position + (Vector3)(direction * step.hitboxCenterDistance);
                float impactRadius = Mathf.Max(0.05f, step.hitboxSize.x * 0.5f);
                scale = impactRadius / localRadius * Mathf.Max(0.01f, step.slashScaleMultiplier);
            }
            else
            {
                float frontEdge = Mathf.Max(0.05f,
                    step.hitboxCenterDistance + step.hitboxSize.x * 0.5f);
                position = transform.position;
                scale = frontEdge / localRadius * Mathf.Max(0.01f, step.slashScaleMultiplier);
            }
            SlashArcEffect.Spawn(
                position,
                direction,
                step.slashColor,
                scale,
                step.slashStyle,
                step.slashLifetime,
                step.slashFlipY,
                step.slashRotationOffset,
                step.slashHeight);
        }

        void TryDodge() => RequestDodge();

        public bool RequestDodge()
        {
            if (!isActiveAndEnabled || !motor.controlEnabled || EquipmentScreen.IsOpen
                || (health != null && health.IsDead)
                || Time.time < nextDodgeTime || Time.time < dodgeUntil) return false;
            if (motor.Stamina == null || !motor.Stamina.TryDodge()) return false;

            nextDodgeTime = Time.time + dodgeCooldown;
            dodgeUntil = Time.time + dodgeDuration;

            Vector2 direction = motor.MoveInput.sqrMagnitude > 0.01f
                ? motor.MoveInput.normalized
                : (motor.Facing.sqrMagnitude > 0.01f ? motor.Facing.normalized : Vector2.down);

            if (health != null) health.SetInvulnerable(dodgeInvincible);
            DodgeStarted?.Invoke();
            StartCoroutine(DodgeRoutine(direction));
            return true;
        }

        IEnumerator DodgeRoutine(Vector2 direction)
        {
            motor.ExternalVelocity = direction * dodgeSpeed;
            yield return new WaitForSeconds(dodgeDuration);
            motor.ExternalVelocity = Vector2.zero;
        }

        public void ResetCombo()
        {
            combo = BuildPunchSequence();
            nextComboStep = 0;
            lastAttackInputTime = float.NegativeInfinity;
            attackQueued = false;
        }

        static int DirectionIndex(Vector2 direction)
        {
            int index = Mathf.RoundToInt(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg / 45f);
            return (index % 8 + 8) % 8;
        }

        void OnFacingChanged(Vector2 direction)
        {
            int index = DirectionIndex(direction);
            if (index == observedFacing) return;
            observedFacing = index;
            // 改方向即断开连击；即使转开后又转回来，也不能接着旧段数。
            nextComboStep = 0;
            lastAttackInputTime = float.NegativeInfinity;
            attackQueued = false;
        }

        void InterruptAttack()
        {
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = null;
            if (hitbox != null) hitbox.Deactivate();
            if (motor != null) motor.SpeedMultiplier = 1f;
            attacking = false;
            attackQueued = false;
        }

        // 三段各有 8 帧素材。命中窗口对应第 3~4 帧；上勾拳收招稍慢。
        static readonly Color PunchColor = new Color(1f, 0.86f, 0.55f, 1f);

        public static ComboStep[] BuildPunchSequence()
        {
            return new[]
            {
                new ComboStep
                {
                    name = "左拳",
                    damageMultiplier = 1f,
                    windup = 0.15f,
                    horizontalWindup = 0.20f,
                    active = 0.10f,
                    recovery = 0.15f,
                    moveMultiplier = 0f,
                    hitboxCenterDistance = 0.70f,
                    hitboxSize = new Vector2(1.5f, 1.15f),
                    slashStyle = SlashStyle.Arc,
                    slashColor = PunchColor,
                    slashScaleMultiplier = 0.70f,
                    // 刀光是旧三段斩的表现，会盖住出拳动作；先关掉，只保留判定和命中反馈。
                    // 以后要做拳击特效时把这里改成 > 0 即可恢复 SpawnSlash。
                    slashLifetime = 0f,
                    slashHeight = 1.0f,
                },
                new ComboStep
                {
                    name = "右拳",
                    damageMultiplier = 1.15f,
                    windup = 0.1875f,
                    horizontalWindup = 0.3125f,
                    active = 0.125f,
                    recovery = 0.1875f,
                    moveMultiplier = 0f,
                    hitboxCenterDistance = 0.75f,
                    hitboxSize = new Vector2(1.65f, 1.25f),
                    slashStyle = SlashStyle.Wide,
                    slashColor = PunchColor,
                    slashScaleMultiplier = 0.75f,
                    slashLifetime = 0f,
                    slashHeight = 1.0f,
                },
                new ComboStep
                {
                    name = "上勾拳",
                    damageMultiplier = 1.5f,
                    windup = 0.1875f,
                    active = 0.125f,
                    recovery = 0.1875f,
                    moveMultiplier = 0f,
                    hitboxCenterDistance = 0.78f,
                    hitboxSize = new Vector2(1.75f, 1.4f),
                    slashStyle = SlashStyle.Wide,
                    slashColor = PunchColor,
                    slashLifetime = 0f,
                    slashHeight = 1.0f,
                },
            };
        }

        // 在 Scene 视图里显示各段判定矩形，方便核对实际命中范围。
        void OnDrawGizmosSelected()
        {
            if (combo == null || combo.Length == 0) combo = BuildPunchSequence();
            if (combo == null || combo.Length == 0) return;

            Vector2 direction = motor != null && motor.Facing.sqrMagnitude > 0.01f
                ? motor.Facing.normalized
                : Vector2.right;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            Matrix4x4 old = Gizmos.matrix;
            for (int i = 0; i < combo.Length; i++)
            {
                var step = combo[i];
                Vector3 center = transform.position + (Vector3)(direction * step.hitboxCenterDistance);

                Gizmos.color = i == 0
                    ? new Color(1f, 0.8f, 0.2f, 0.9f)
                    : new Color(1f, 0.5f, 0.2f, 0.9f);
                Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(step.hitboxSize.x, step.hitboxSize.y, 0.1f));
                Gizmos.matrix = old;

                Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
                if (SlashArcEffect.IsPunch(step.slashStyle))
                {
                    // 拳击命中点：判定框中心，大小按半宽
                    Gizmos.DrawWireSphere(center, step.hitboxSize.x * 0.5f);
                }
                else
                {
                    // 刀光以角色为中心，外缘半径 = 判定框前缘
                    float frontEdge = step.hitboxCenterDistance + step.hitboxSize.x * 0.5f;
                    Gizmos.DrawWireSphere(transform.position, frontEdge);
                }
            }
        }

        void OnDisable()
        {
            StopAllCoroutines();
            attackRoutine = null;
            if (hitbox != null) hitbox.Deactivate();
            if (motor != null)
            {
                motor.SpeedMultiplier = 1f;
                motor.ExternalVelocity = Vector2.zero;
            }
            attacking = false;
            attackQueued = false;
            nextComboStep = 0;
            lastAttackInputTime = float.NegativeInfinity;
            dodgeUntil = 0f;
        }
    }
}
