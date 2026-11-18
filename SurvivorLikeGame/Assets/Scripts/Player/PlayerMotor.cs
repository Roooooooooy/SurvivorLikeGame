using UnityEngine;
using UnityEngine.InputSystem;

namespace RpgGame
{
    // 动作区移动：只负责输入、朝向和刚体速度，不涉及武器/经验/局内升级。
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerStamina))]
    public class PlayerMotor : MonoBehaviour
    {
        public float moveSpeed = 5f;
        [Min(1f)] public float sprintMultiplier = 1.6f;
        public bool controlEnabled = true;

        public Vector2 MoveInput { get; private set; }
        public Vector2 Facing { get; private set; } = Vector2.down;
        public Vector2 ExternalVelocity { get; set; }
        public float SpeedMultiplier { get; set; } = 1f;

        public bool IsMoving => MoveInput.sqrMagnitude > 0.001f;
        public bool IsSprinting { get; private set; }
        public PlayerStamina Stamina { get; private set; }
        public event System.Action<Vector2> FacingChanged;
        public bool HasDirectionalInput => controlEnabled && !EquipmentScreen.IsOpen
            && ReadInput().sqrMagnitude > 0.001f;

        // 攻击输入可能先于本帧移动 Update；直接读取意图，避免缓冲攻击沿用旧朝向。
        public Vector2 InputFacing
        {
            get
            {
                Vector2 input = controlEnabled && !EquipmentScreen.IsOpen ? ReadInput() : Vector2.zero;
                return input.sqrMagnitude > 0.001f ? input.normalized : Facing;
            }
        }

        Rigidbody2D body;
        SpriteRenderer view;
        Collider2D bodyCollider;
        readonly System.Collections.Generic.List<RaycastHit2D> movementHits
            = new System.Collections.Generic.List<RaycastHit2D>(16);
        ContactFilter2D movementFilter;
        Vector2 desiredVelocity;
        const float EnemyContactSkin = 0.02f;

        void Awake()
        {
            Stamina = GetComponent<PlayerStamina>();
            if (Stamina == null) Stamina = gameObject.AddComponent<PlayerStamina>();
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            movementFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            movementFilter.useTriggers = false;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            // 与幸存者 Player.prefab 对齐：插值开启后视觉更新与物理同步，转向不会一顿一顿。
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            view = GetComponentInChildren<SpriteRenderer>();
        }

        void Start()
        {
            var stats = CharacterStats.Current;
            if (stats.moveSpeed > 0.01f)
                moveSpeed = Mathf.Max(0.1f, stats.moveSpeed);

            EquipmentService.Changed += OnEquipmentChanged;
        }

        void OnDestroy()
        {
            EquipmentService.Changed -= OnEquipmentChanged;
        }

        void OnEquipmentChanged()
        {
            var stats = CharacterStats.Current;
            if (stats.moveSpeed > 0.01f)
                moveSpeed = Mathf.Max(0.1f, stats.moveSpeed);
        }

        void Update()
        {
            bool canReadInput = controlEnabled && !EquipmentScreen.IsOpen;
            MoveInput = canReadInput ? ReadInput() : Vector2.zero;
            var keyboard = Keyboard.current;
            bool wantsSprint = canReadInput && IsMoving && SpeedMultiplier > 0f
                && ExternalVelocity.sqrMagnitude < 0.01f && keyboard != null
                && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            IsSprinting = wantsSprint && Stamina.TrySprint(Time.deltaTime);

            if (MoveInput.sqrMagnitude > 0.001f)
            {
                Vector2 wanted = MoveInput.normalized;
                bool changedDirection = DirectionIndex(Facing) != DirectionIndex(wanted);
                Facing = wanted;
                if (changedDirection) FacingChanged?.Invoke(Facing);

                if (view != null)
                    view.flipX = wanted.x < -0.01f;
            }

            // 与幸存者 PlayerController 一致：在 Update 里直接写速度。
            // 这样 Hd2dPixelPlayer 在同一个 LateUpdate 里读到的方向就是本帧输入，
            // 不会因为 FixedUpdate 节拍多出一帧转向延迟。
            if (body != null)
            {
                desiredVelocity = MoveInput * (moveSpeed * SpeedMultiplier
                    * (IsSprinting ? sprintMultiplier : 1f)) + ExternalVelocity;
                body.linearVelocity = PreventEnemyPush(desiredVelocity);
            }
        }

        void FixedUpdate()
        {
            // 每次物理模拟前重新约束，低帧率下一帧有多个物理步时也不会顶走怪物。
            if (body != null) body.linearVelocity = PreventEnemyPush(desiredVelocity);
        }

        Vector2 PreventEnemyPush(Vector2 velocity)
        {
            if (bodyCollider == null || !bodyCollider.enabled) return velocity;
            float dt = Mathf.Max(0.001f, Time.fixedDeltaTime);
            for (int pass = 0; pass < 2 && velocity.sqrMagnitude > 0.0001f; pass++)
            {
                float speed = velocity.magnitude;
                Vector2 direction = velocity / speed;
                int count = bodyCollider.Cast(direction, movementFilter, movementHits,
                    speed * dt + EnemyContactSkin, true);
                bool blocked = false;
                RaycastHit2D nearest = default;
                for (int i = 0; i < count; i++)
                {
                    var hit = movementHits[i];
                    var enemy = hit.collider.GetComponentInParent<ActionEnemy>();
                    if (enemy == null || enemy.IsDead) continue;
                    Vector2 toEnemy = (Vector2)enemy.transform.position - body.position;
                    // 允许离开贴身接触；只限制朝怪物身体内的运动。
                    if (Vector2.Dot(direction, toEnemy) <= 0f) continue;
                    if (!blocked || hit.distance < nearest.distance)
                    {
                        nearest = hit;
                        blocked = true;
                    }
                }
                if (!blocked) break;

                float available = Mathf.Max(0f, nearest.distance - EnemyContactSkin);
                if (available > 0.001f)
                {
                    velocity = direction * Mathf.Min(speed, available / dt);
                    break;
                }
                // 接触时保留沿身体表面的切向速度，仍可绕过怪物。
                float inward = Vector2.Dot(velocity, nearest.normal);
                if (inward >= 0f) break;
                velocity -= nearest.normal * inward;
            }
            return velocity;
        }

        public void SetControlEnabled(bool enabled)
        {
            controlEnabled = enabled;

            if (!enabled)
            {
                MoveInput = Vector2.zero;
                IsSprinting = false;
                ExternalVelocity = Vector2.zero;
                desiredVelocity = Vector2.zero;
                if (body != null) body.linearVelocity = Vector2.zero;
            }
        }

        public void Teleport(Vector3 position)
        {
            Vector3 flat = new Vector3(position.x, position.y, transform.position.z);
            transform.position = flat;
            if (body != null) body.position = new Vector2(flat.x, flat.y);
        }

        static Vector2 ReadInput()
        {
            Vector2 input = Vector2.zero;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > input.sqrMagnitude) input = stick;
            }

            return Vector2.ClampMagnitude(input, 1f);
        }

        static int DirectionIndex(Vector2 direction)
        {
            int index = Mathf.RoundToInt(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg / 45f);
            return (index % 8 + 8) % 8;
        }
    }
}
