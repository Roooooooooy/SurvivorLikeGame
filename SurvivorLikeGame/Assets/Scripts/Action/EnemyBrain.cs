using UnityEngine;

namespace RpgGame
{
    // 动作敌人状态机：Idle → Chase → Windup → Attack → Recover，受击时进入 Hit。
    // 只做直线追击；绕墙导航在 Phase 1 用区域路点图替换。
    // 有 DirectionalEnemyView 时，攻击/受击会驱动 8 方向动画；受击期间锁定移动。
    [RequireComponent(typeof(ActionEnemy))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyBrain : MonoBehaviour
    {
        public float moveSpeed = 2.5f;
        public float aggroRange = 8f;
        public float loseTargetRange = 14f;
        public float attackRange = 1.1f;
        public float attackWindup = 0.35f;
        public float attackRecover = 0.8f;
        public int attackDamage = 8;
        [Tooltip("受击硬直的最短时长；有受击动画时取动画时长")]
        public float minHitStun = 0.15f;

        enum State { Idle, Chase, Windup, Recover, Hit }

        ActionEnemy self;
        Rigidbody2D body;
        Collider2D bodyCollider;
        ContactFilter2D movementFilter;
        readonly System.Collections.Generic.List<RaycastHit2D> movementHits
            = new System.Collections.Generic.List<RaycastHit2D>(16);
        const float ActorContactSkin = 0.025f;
        DirectionalEnemyView view;
        Transform target;
        State state = State.Idle;
        float timer;
        int facing = 6;   // 南 / 正面

        public int Facing => facing;

        void Awake()
        {
            self = GetComponent<ActionEnemy>();
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            movementFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            movementFilter.useTriggers = false;
            view = GetComponent<DirectionalEnemyView>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }

        void Start()
        {
            ResolveTarget();
        }

        void Update()
        {
            if (self == null || self.IsDead)
            {
                state = State.Idle;
                return;
            }

            ResolveTarget();
            if (target == null) return;

            if (PlayerHealth.Player != null && PlayerHealth.Player.IsDead)
            {
                state = State.Idle;
                return;
            }

            float distance = Vector2.Distance(transform.position, target.position);

            switch (state)
            {
                case State.Idle:
                    if (distance <= aggroRange) state = State.Chase;
                    break;

                case State.Chase:
                    if (distance > loseTargetRange)
                    {
                        state = State.Idle;
                    }
                    else if (distance <= attackRange)
                    {
                        BeginAttack();
                    }
                    break;

                case State.Windup:
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        if (distance <= attackRange * 1.3f) DealDamage();
                        state = State.Recover;
                        timer = attackRecover;
                    }
                    break;

                case State.Recover:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) state = State.Chase;
                    break;

                case State.Hit:
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                        state = distance <= loseTargetRange ? State.Chase : State.Idle;
                    break;
            }
        }

        void FixedUpdate()
        {
            if (body == null) return;

            if (state == State.Chase && target != null)
            {
                Vector2 direction = ((Vector2)target.position - body.position).normalized;
                body.linearVelocity = PreventActorPush(direction * moveSpeed);
                facing = DirectionalEnemyView.DirectionFromWorld(direction);
                if (view != null) view.SetFacing(facing);
            }
            else
            {
                // 出招/收招/受击都不移动。
                body.linearVelocity = Vector2.zero;
            }
        }

        // 与玩家的主动移动一样，在物理模拟前留出接触间隙。
        // 不让追击速度持续顶入主角，也阻止多个怪物互推后把推力传给主角。
        Vector2 PreventActorPush(Vector2 velocity)
        {
            if (bodyCollider == null || !bodyCollider.enabled) return velocity;
            float dt = Mathf.Max(0.001f, Time.fixedDeltaTime);
            for (int pass = 0; pass < 2 && velocity.sqrMagnitude > 0.0001f; pass++)
            {
                float speed = velocity.magnitude;
                Vector2 direction = velocity / speed;
                int count = bodyCollider.Cast(direction, movementFilter, movementHits,
                    speed * dt + ActorContactSkin, true);
                bool blocked = false;
                RaycastHit2D nearest = default;
                for (int i = 0; i < count; i++)
                {
                    var hit = movementHits[i];
                    var player = hit.collider.GetComponentInParent<PlayerHealth>();
                    var enemy = hit.collider.GetComponentInParent<ActionEnemy>();
                    if (player == null && (enemy == null || enemy == self || enemy.IsDead)) continue;
                    Vector2 toOther = (Vector2)hit.collider.transform.position - body.position;
                    if (Vector2.Dot(direction, toOther) <= 0f) continue;
                    if (!blocked || hit.distance < nearest.distance) { nearest = hit; blocked = true; }
                }
                if (!blocked) break;
                float available = Mathf.Max(0f, nearest.distance - ActorContactSkin);
                if (available > 0.001f)
                {
                    velocity = direction * Mathf.Min(speed, available / dt);
                    break;
                }
                float inward = Vector2.Dot(velocity, nearest.normal);
                if (inward >= 0f) break;
                velocity -= nearest.normal * inward;
            }
            return velocity;
        }

        void BeginAttack()
        {
            Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
                facing = DirectionalEnemyView.DirectionFromWorld(toTarget);

            if (view != null)
            {
                view.SetFacing(facing);
                view.PlayAttack(facing, attackWindup + attackRecover);
            }

            state = State.Windup;
            timer = attackWindup;
        }

        void DealDamage()
        {
            if (target == null) return;

            var health = target.GetComponent<PlayerHealth>();
            if (health == null) return;

            Vector2 source = transform.position;
            Vector2 toTarget = (Vector2)target.position - source;

            health.TakeDamage(new DamageInfo
            {
                amount = attackDamage,
                hitPoint = target.position,
                sourcePosition = source,
                knockback = toTarget.normalized * 2f,
                source = gameObject,
            });
        }

        // 受击：有动画时朝伤害来源方向播放受击并锁住移动；没有动画的敌人保持旧逻辑。
        public void OnDamaged(int hitDirection = -1)
        {
            if (self == null || self.IsDead) return;

            if (view == null)
            {
                if (state == State.Idle) state = State.Chase;
                return;
            }

            facing = hitDirection >= 0 ? hitDirection : view.Facing;
            float duration = view.PlayHit(facing);

            state = State.Hit;
            timer = Mathf.Max(minHitStun, duration);
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        void ResolveTarget()
        {
            if (target != null) return;
            if (PlayerHealth.Player != null)
                target = PlayerHealth.Player.transform;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aggroRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
