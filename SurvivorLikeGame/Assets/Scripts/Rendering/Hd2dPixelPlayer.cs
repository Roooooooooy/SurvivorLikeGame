using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RpgGame;

// 8 方向像素主角视图。
// - 逻辑/碰撞仍在 PlayerController 的 XY 平面
// - 视觉根节点映射到世界 (x, 0, y)，面向相机（billboard）
// - 每个方向 8 帧横向精灵表，跑步/待机切换
// - 朝向只在移动时更新；停下后保持最后一次奔跑方向，播放同方向的待机
// - 90° 换向先经过中间方向（如 W→S 经过 SW），180° 转身快速经过 3 个中间方向
// - 斜向退化成相邻正方向时有短暂防抖，避免松键先后把待机变成正方向
// - 待机、跑步、快跑、受击和三段拳击均使用八方向独立素材。
[DisallowMultipleComponent]
public class Hd2dPixelPlayer : MonoBehaviour
{
    public static Hd2dPixelPlayer Instance { get; private set; }
    public static bool Active => Instance != null && Instance.sr != null;

    [Header("精灵表（Resources 路径）")]
    public string runningRoot = "Player/Cat8Dir/Running";
    public string idleRoot = "Player/Cat8Dir/Idle";
    public string attackRoot = "Player/Cat8Dir/Attack";
    public string sprintRoot = "Player/Cat8Dir/Sprint";
    public string hurtRoot = "Player/Cat8Dir/Hurt";
    public string dodgeRoot = "Player/Cat8Dir/Dodge";
    public string deathRoot = "Player/Cat8Dir/Death";
    public int frameCount = 8;
    public int attackFrameCount = 16;
    public int frameWidth = 64;
    public int frameHeight = 64;
    public float pixelsPerUnit = 48f;

    [Header("节奏")]
    public float runFps = 10f;
    public float idleFps = 5f;
    public float moveThreshold = 0.05f;
    [Tooltip("跑步动画按这个移速对应 runFps；实际帧率会随移速缩放")]
    public float runSpeedReference = 5f;
    public float maxRunFps = 16f;
    [Tooltip("死亡动画帧率；9 帧 × 10fps ≈ 0.9 秒，PlayerHealth 会等待动画和消散播完")]
    public float deathFps = 10f;
    [Header("死亡消散")]
    [Tooltip("死亡动画播完后停留多久开始消散")]
    public float deathFadeDelay = 0.7f;
    [Tooltip("消散时长；PlayerHealth 会据此延长重生等待")]
    public float deathFadeDuration = 0.9f;

    [Header("朝向过渡")]
    [Tooltip("90° 换向时先经过中间方向的时长（如 W→S 经过 SW）；0 表示关闭过渡")]
    public float diagonalTransitionDuration = 0.07f;
    [Tooltip("180° 转身依次经过 3 个中间方向的总时长（如 W→E 经过 SW→S→SE）；0 表示关闭")]
    public float oppositeTransitionDuration = 0.12f;
    [Tooltip("斜向退化成相邻正方向时，需要持续这么久才提交切换；用于吃掉斜向松键有先后造成的抖动")]
    public float diagonalReleaseDelay = 0.1f;

    [Header("脚步尘土")]
    public bool dust = true;
    public Color dustColor = new Color(0.85f, 0.80f, 0.70f, 0.40f);
    public float dustSize = 0.45f;
    public float dustRate = 24f;
    public bool turnDust = true;
    public int turnDustCount = 3;
    public int stopDustCount = 5;
    public float turnDustCooldown = 0.12f;

    [Header("映射")]
    public float viewScale = 1.6f;
    public float worldYOffset = 0f;
    [Tooltip("起始朝南（正面）；0=东，1=东北，2=北，3=西北，4=西，5=西南，6=南，7=东南")]
    public int startDirection = 6;

    [Header("表现")]
    public bool groundShadow = true;
    public float shadowDiameter = 0.85f;
    public float shadowDepth = 0.50f;
    [Range(0f, 1f)] public float shadowOpacity = 0.42f;
    [Range(0f, 2f)] public float litAmbient = 0.85f;
    public bool xray = true;
    public Color xrayColor = new Color(0.35f, 0.82f, 1f, 0.45f);
    public Color tint = new Color(0.96f, 0.98f, 1f, 1f);
    public float flashDuration = 0.12f;

    [Header("动作模式反馈")]
    [Tooltip("未绑定攻击控制器时的备用帧率；正常攻击按每段判定时序播放")]
    public float attackFps = 20f;
    public float hurtFps = 20f;
    [Tooltip("某个方向缺少攻击素材时，用一次轻微缩放脉冲提示出手")]
    public float attackPulseDuration = 0.12f;
    public float attackPulseScale = 1.08f;

    // 世界移动方向索引（角度从 +X 逆时针：0=东，45=东北，90=北，135=西北，180=西，225=西南，270=南，315=东南）
    // 生成的美术整体左右与游戏世界相反，因此所有方向一起水平镜像：
    // E↔W、NE↔NW、SE↔SW，N/S 不变。
    static readonly string[] DirNames =
    {
        "west", "north-west", "north", "north-east",
        "east", "south-east", "south", "south-west"
    };

    readonly Sprite[][] runFrames = new Sprite[8][];
    readonly Sprite[][] idleFrames = new Sprite[8][];
    readonly Sprite[][][] comboFrames = new Sprite[3][][]
    {
        new Sprite[8][], new Sprite[8][], new Sprite[8][]
    };
    static readonly string[] AttackFolders = { "LeftPunch", "RightPunch", "Uppercut" };
    readonly Sprite[][] sprintFrames = new Sprite[8][];
    readonly Sprite[][] hurtFrames = new Sprite[8][];
    readonly Sprite[][] dodgeFrames = new Sprite[8][];
    readonly Sprite[][] deathFrames = new Sprite[8][];
    readonly Dictionary<Texture, Material> litMats = new Dictionary<Texture, Material>();
    readonly Dictionary<Texture, Material> xrayMats = new Dictionary<Texture, Material>();

    SpriteRenderer sr;
    SpriteRenderer xraySr;
    Hd2dFootDust dustFx;
    Hd2dBlobShadow blobShadow;
    Hd2dEquipmentVisuals equipmentVisuals;
    float turnDustTimer;
    Transform player;
    Rigidbody2D rb;
    PlayerController pc;
    PlayerHealth actionHealth;
    ActionCombatController actionCombat;
    PlayerMotor actionMotor;
    Camera cam;
    Shader litShader;
    Shader xrayShader;

    int dir;
    int pendingDir = -1;
    float pendingDirTimer;
    readonly int[] transitionSteps = new int[3];
    int transitionStepCount;
    int transitionStepIndex;
    int transitionTargetDir = -1;
    float transitionTimer;
    float transitionStepTime;
    int frame;
    float timer;
    bool running;
    bool dead;
    int lastHealth;
    float flashTimer;
    float attackPulse;
    bool attackPlaying;
    int attackDir = -1;
    float attackElapsed;
    int attackFrame;
    int attackStep;
    bool hurtPlaying;
    int hurtDir;
    int hurtFrame;
    float hurtElapsed;
    bool dodgePlaying;
    int dodgeDir;
    int dodgeFrame;
    float dodgeElapsed;
    float dodgeDuration = 0.22f;
    bool deathPlaying;
    int deathDir;
    int deathFrame;
    float deathElapsed;
    public bool IsHurt => hurtPlaying;
    // 闪避/死亡时装备层没有对应偏移，先隐藏，避免帽子/披风脱离身体。
    public bool HideEquipment => dodgePlaying || deathPlaying;
    // PlayerHealth 用它决定重生等待，保证死亡动画 + 消散播完再切场景。
    public float TotalDeathDuration => DeathClipDuration() + deathFadeDelay + deathFadeDuration;

    // ---- 供装备图层读取的只读状态 ----
    public int Direction => dir;
    public int FrameIndex => hurtPlaying ? hurtFrame : (attackPlaying ? attackFrame : frame);
    public GearMotion Motion => !hurtPlaying && attackPlaying
        ? GearMotion.Attack
        : (running ? GearMotion.Run : GearMotion.Idle);
    public bool IsAttacking => attackPlaying;
    public Sprite BodySprite => sr != null ? sr.sprite : null;
    public Color BodyColor => sr != null ? sr.color : Color.white;
    public float PixelsPerUnit => pixelsPerUnit;
    public int FramePixelWidth => frameWidth;
    public int FramePixelHeight => frameHeight;

    // 装备图集直接复用身体帧的枢轴（脚底），避免装备层上下漂移。
    public Vector2 BodyPivotNormalized
    {
        get
        {
            var sprite = BodySprite;
            return sprite != null
                ? new Vector2(0.5f, sprite.pivot.y / Mathf.Max(1f, sprite.rect.height))
                : new Vector2(0.5f, 0.5f);
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        litShader = Shader.Find("HD2D/SpriteLit");
        xrayShader = Shader.Find("HD2D/SpriteXRay");
        if (litShader == null)
            Debug.LogWarning("[Hd2dPixelPlayer] 找不到 HD2D/SpriteLit，角色会用默认精灵材质");

        var go = new GameObject("PixelCat");
        go.transform.SetParent(transform, false);
        go.AddComponent<Hd2dProxyMarker>();
        sr = go.AddComponent<SpriteRenderer>();
        sr.shadowCastingMode = ShadowCastingMode.On;
        sr.receiveShadows = true;

        if (xray && xrayShader != null)
        {
            var xgo = new GameObject("PixelCatXRay");
            xgo.transform.SetParent(transform, false);
            xgo.AddComponent<Hd2dProxyMarker>();
            xraySr = xgo.AddComponent<SpriteRenderer>();
            xraySr.shadowCastingMode = ShadowCastingMode.Off;
            xraySr.receiveShadows = false;
            xraySr.color = Color.white;
        }

        if (groundShadow) SetupGroundShadow();
        if (dust)
        {
            var dustGo = new GameObject("[HD2D_FootDust]");
            dustFx = dustGo.AddComponent<Hd2dFootDust>();
            dustFx.Configure(dustColor, dustSize, dustRate);
        }

        dir = Mathf.Clamp(startDirection, 0, 7);
        LoadAll();
        ApplyFrame(true);

        // 装备层与身体共用同一个 billboard 根，按方向和槽位叠加。
        equipmentVisuals = gameObject.AddComponent<Hd2dEquipmentVisuals>();
        equipmentVisuals.Initialize(this);
    }

    void Start()
    {
        ResolvePlayer();

        if (player != null)
        {
            // 旧的可视化全部让位：Prefab 上的 SpriteRenderer 和自动挂的 PlayerAnimation
            var legacy = player.GetComponent<SpriteRenderer>();
            if (legacy != null)
            {
                legacy.enabled = false;
                legacy.forceRenderingOff = true;
            }
            var oldAnim = player.GetComponent<PlayerAnimation>();
            if (oldAnim != null) oldAnim.enabled = false;
        }

        cam = Camera.main;
        if (Hd2dSpriteBridge.Instance != null)
            Hd2dSpriteBridge.Instance.SuppressPlayerProxy();

        // 场景里如果还留着 Spine 主角，直接停掉，避免双重形象
        var spine = FindFirstObjectByType<Hd2dSpinePlayer>();
        if (spine != null)
        {
            if (spine.gameObject != gameObject) spine.gameObject.SetActive(false);
            else Destroy(spine);
        }
    }

    // 同一套像素视图同时服务两种玩法：
    // - 幸存者区：PlayerController（自动武器）
    // - 动作区：PlayerHealth + ActionCombatController（手动战斗）
    void ResolvePlayer()
    {
        // 动作区优先绑定 PlayerHealth，避免误加载的幸存者玩家抢走主角视图。
        if (!RegionRuntime.IsAction)
        {
            pc = PlayerController.Instance;
            if (pc != null)
            {
                player = pc.transform;
                rb = pc.GetComponent<Rigidbody2D>();
                lastHealth = pc.currentHealth;
                return;
            }
        }

        ResolveActionPlayer();
    }

    void ResolveActionPlayer()
    {
        pc = null;
        actionHealth = PlayerHealth.Player;
        if (actionHealth == null) return;

        player = actionHealth.transform;
        rb = actionHealth.GetComponent<Rigidbody2D>();
        lastHealth = actionHealth.currentHealth;
        actionMotor = actionHealth.GetComponent<PlayerMotor>();
        actionHealth.Damaged -= OnActionHurt;
        actionHealth.Damaged += OnActionHurt;

        actionCombat = actionHealth.GetComponent<ActionCombatController>();
        if (actionCombat != null)
        {
            actionCombat.AttackStepStarted -= OnActionAttack;
            actionCombat.AttackStepStarted += OnActionAttack;
            actionCombat.DodgeStarted -= OnActionDodge;
            actionCombat.DodgeStarted += OnActionDodge;
        }
    }

    int CurrentHealth()
    {
        if (pc != null) return pc.currentHealth;
        if (actionHealth != null) return actionHealth.currentHealth;
        return 0;
    }

    // 每次事件只播放实际出手的一段，与控制器共用时序。
    void OnActionAttack(int step, Vector2 direction)
    {
        hurtPlaying = false;
        dodgePlaying = false;
        attackStep = Mathf.Clamp(step, 0, comboFrames.Length - 1);
        int newDir = DirectionFromWorld(direction);
        attackDir = newDir;
        attackElapsed = 0f;
        attackFrame = 0;
        attackPlaying = HasAttackFrames(newDir);
        attackPulse = attackPlaying ? 0f : attackPulseDuration;

        // 有攻击素材时，动画方向以出拳方向为准，并锁住朝向直到收招。
        if (attackPlaying)
        {
            EndTransition();
            pendingDir = -1;
            pendingDirTimer = 0f;
            dir = newDir;
        }
    }

    void OnActionHurt()
    {
        if (actionHealth != null && actionHealth.IsDead) return;
        flashTimer = flashDuration;
        attackPlaying = false;
        dodgePlaying = false;
        attackPulse = 0f;
        hurtDir = dir;
        hurtFrame = 0;
        hurtElapsed = 0f;
        hurtPlaying = hurtFrames[hurtDir] != null;
        EndTransition();
        pendingDir = -1;
        ApplyFrame(true);
    }

    // 闪避事件没有方向参数：有移动输入就沿输入方向，否则沿当前朝向翻滚。
    void OnActionDodge()
    {
        if (actionHealth != null && actionHealth.IsDead) return;

        Vector2 direction = actionMotor != null && actionMotor.MoveInput.sqrMagnitude > 0.01f
            ? actionMotor.MoveInput
            : (actionMotor != null ? actionMotor.Facing : DirectionFromIndex(dir));

        dodgeDir = DirectionFromWorld(direction);
        dodgeFrame = 0;
        dodgeElapsed = 0f;
        dodgeDuration = actionCombat != null
            ? Mathf.Max(0.05f, actionCombat.dodgeDuration)
            : 0.22f;
        dodgePlaying = dodgeFrames[dodgeDir] != null;

        hurtPlaying = false;
        attackPlaying = false;
        attackPulse = 0f;

        if (dodgePlaying)
        {
            EndTransition();
            pendingDir = -1;
            pendingDirTimer = 0f;
            dir = dodgeDir;
            ApplyFrame(true);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (actionHealth != null) actionHealth.Damaged -= OnActionHurt;
        if (actionCombat != null)
        {
            actionCombat.AttackStepStarted -= OnActionAttack;
            actionCombat.DodgeStarted -= OnActionDodge;
        }
        if (dustFx != null) Destroy(dustFx.gameObject);
        foreach (var m in litMats.Values)
            if (m != null) Destroy(m);
        foreach (var m in xrayMats.Values)
            if (m != null) Destroy(m);
        litMats.Clear();
        xrayMats.Clear();
    }

    void LateUpdate()
    {
        if (sr == null) return;

        if (player == null)
        {
            ResolvePlayer();
            if (player == null) return;
        }
        if (cam == null) cam = Camera.main;

        Vector3 p = player.position;
        transform.position = new Vector3(p.x, worldYOffset, p.y);

        if (attackPulse > 0f) attackPulse -= Time.deltaTime;
        float pulse = attackPulse > 0f ? attackPulseScale : 1f;
        transform.localScale = Vector3.one * (viewScale * pulse);

        // 使用固定相机 yaw，避免角色偏离画面中心时 sprite 向相机偏转。
        if (cam != null)
            transform.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y + 180f, 0f);

        // 死亡后推进动画；播完停留一小段，然后按有序抖动慢慢消散。
        if (dead)
        {
            deathElapsed += Time.deltaTime;
            UpdateDeathAnimation();
            ApplyFrame(false);
            UpdateDeathDissolve();
            return;
        }

        var gm = GameManager.Instance;
        bool gameOver = gm != null && gm.isGameOver;
        bool healthDead = (pc != null && pc.currentHealth <= 0)
            || (actionHealth != null && actionHealth.IsDead);

        if (gameOver || healthDead)
        {
            Die();
            return;
        }

        int health = CurrentHealth();
        if (lastHealth > 0 && health < lastHealth)
        {
            flashTimer = flashDuration;
            if (pc != null) OnActionHurt();
        }
        lastHealth = health;

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            sr.color = Color.red;
        }
        else
        {
            sr.color = Color.white;
        }

        Vector2 v = rb != null ? rb.linearVelocity : Vector2.zero;
        float speed = v.magnitude;
        bool moving = speed > moveThreshold;

        if (turnDustTimer > 0f) turnDustTimer -= Time.deltaTime;

        // 只在移动时更新朝向。松开方向键后 velocity 归零，
        // dir 保留最后一次奔跑方向，停下后待机就播放同方向的 idle。
        // 攻击期间锁住朝向，避免边移动边换向把出拳方向拧走。
        if (moving && !attackPlaying && !hurtPlaying && !dodgePlaying) UpdateFacingDirection(v);

        if (moving != running)
        {
            if (!moving && running && turnDust && dustFx != null)
                dustFx.Burst(transform.position + Vector3.up * 0.06f, stopDustCount, 1.25f);
            running = moving;
            frame = 0;
            timer = 0f;
            // 停下时丢弃尚未确认的候选方向，保留最后确认的朝向作为待机方向。
            pendingDir = -1;
            pendingDirTimer = 0f;

            // 仅在停下时结算：若停在 90° 过渡中途，直接落到实际移动方向，
            // 保证待机与最后移动一致。开始移动的那一帧不能结算，否则会跳过中间方向。
            if (!moving && transitionTimer > 0f)
            {
                int target = transitionTargetDir;
                EndTransition();
                CommitDirection(target);
            }
        }

        var frames = CurrentFrames();
        float fps = running
            ? Mathf.Clamp(runFps * speed / Mathf.Max(0.01f, runSpeedReference), idleFps, maxRunFps)
            : idleFps;
        timer += Time.deltaTime;
        float step = 1f / Mathf.Max(0.01f, fps);
        while (timer >= step)
        {
            timer -= step;
            if (frames != null && frames.Length > 0)
                frame = (frame + 1) % frames.Length;
        }

        if (dustFx != null)
            dustFx.SetEmitting(running, transform.position + Vector3.up * 0.06f);

        if (hurtPlaying) UpdateHurtAnimation();
        if (attackPlaying) UpdateAttackAnimation();
        if (dodgePlaying) UpdateDodgeAnimation();

        ApplyFrame(false);
    }

    void UpdateAttackAnimation()
    {
        attackElapsed += Time.deltaTime;

        var frames = ActiveAttackFrames();
        if (frames == null)
        {
            attackPlaying = false;
            return;
        }

        float duration = actionCombat != null
            ? actionCombat.AttackDuration : frames.Length / Mathf.Max(0.01f, attackFps);
        float elapsed = actionCombat != null ? actionCombat.AttackElapsed : attackElapsed;
        bool finished = actionCombat != null ? !actionCombat.IsAttacking : elapsed >= duration;
        if (finished)
        {
            attackPlaying = false;
            frame = 0;
            timer = 0f;
            return;
        }
        attackFrame = Mathf.Min(frames.Length - 1,
            Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, duration) * frames.Length));
    }

    void UpdateHurtAnimation()
    {
        hurtElapsed += Time.deltaTime;
        var frames = hurtFrames[hurtDir];
        hurtFrame = Mathf.FloorToInt(hurtElapsed * Mathf.Max(0.01f, hurtFps));
        if (frames == null || hurtFrame >= frames.Length)
        {
            hurtPlaying = false;
            frame = 0;
            timer = 0f;
        }
    }

    // 闪避动画铺满控制器的 dodgeDuration（默认 0.22s），结束后回到待机/奔跑。
    void UpdateDodgeAnimation()
    {
        var frames = dodgeFrames[dodgeDir];
        if (frames == null || frames.Length == 0)
        {
            dodgePlaying = false;
            return;
        }

        dodgeElapsed += Time.deltaTime;
        if (dodgeElapsed >= dodgeDuration)
        {
            dodgePlaying = false;
            frame = 0;
            timer = 0f;
            return;
        }

        dodgeFrame = Mathf.Min(frames.Length - 1,
            Mathf.FloorToInt(dodgeElapsed / Mathf.Max(0.01f, dodgeDuration) * frames.Length));
    }

    // 死亡动画只播一次，播完后停最后一帧。
    void UpdateDeathAnimation()
    {
        if (!deathPlaying) return;

        var frames = deathFrames[deathDir];
        if (frames == null || frames.Length == 0)
        {
            deathPlaying = false;
            return;
        }

        deathFrame = Mathf.Min(frames.Length - 1,
            Mathf.FloorToInt(deathElapsed * Mathf.Max(1f, deathFps)));
    }

    float DeathClipDuration()
    {
        // PlayerHealth 在 Hd2dPixelPlayer.Die() 之前查询时长，此时 deathDir 还没写入。
        int index = Mathf.Clamp(deathPlaying ? deathDir : dir, 0, 7);
        var frames = deathFrames[index];
        return frames != null && frames.Length > 0
            ? frames.Length / Mathf.Max(1f, deathFps)
            : 0f;
    }

    // 死亡动画结束后推进金色溶解；同时淡出接地阴影。
    void UpdateDeathDissolve()
    {
        float start = DeathClipDuration() + Mathf.Max(0f, deathFadeDelay);
        float duration = Mathf.Max(0.01f, deathFadeDuration);
        float raw = Mathf.Clamp01((deathElapsed - start) / duration);
        float t = raw * raw * (3f - 2f * raw);   // 缓入缓出，消散更自然

        SetDissolve(t);

        if (raw >= 1f)
        {
            if (sr != null) sr.enabled = false;
            if (blobShadow != null) blobShadow.SetOpacity(0f);
        }
    }

    void SetDissolve(float t)
    {
        if (sr != null && sr.sharedMaterial != null &&
            sr.sharedMaterial.HasProperty("_Dissolve"))
        {
            sr.sharedMaterial.SetFloat("_Dissolve", Mathf.Clamp01(t));
        }

        if (blobShadow != null)
            blobShadow.SetOpacity(shadowOpacity * (1f - Mathf.Clamp01(t)));
    }

    Sprite[] ActiveAttackFrames()
    {
        if (!attackPlaying) return null;
        if (attackDir < 0 || attackDir >= 8) return null;
        var frames = comboFrames[attackStep][attackDir];
        return frames != null && frames.Length > 0 ? frames : null;
    }

    bool HasAttackFrames(int direction)
    {
        if (direction < 0 || direction >= 8) return false;
        var frames = comboFrames[attackStep][direction];
        return frames != null && frames.Length > 0;
    }

    // 移动中更新朝向。规则：
    // 1) 90° 换向经过 1 个中间方向，180° 转身经过 3 个，播完再落到目标；
    // 2) 斜向退化成相邻正方向可能只是松键先后造成的抖动，挂起一小段时间再提交；
    // 3) 其它角度立即提交。
    void UpdateFacingDirection(Vector2 v)
    {
        int newDir = DirectionFromWorld(v);

        // 正在播放过渡：输入仍指向目标就一直播到结束。
        if (transitionTimer > 0f)
        {
            if (newDir != transitionTargetDir)
            {
                // 玩家又换了方向，放弃这次过渡，按新方向重新判断。
                EndTransition();
            }
            else
            {
                transitionTimer -= Time.deltaTime;
                if (transitionTimer > 0f) return;

                transitionStepIndex++;
                if (transitionStepIndex >= transitionStepCount)
                {
                    int target = transitionTargetDir;
                    EndTransition();
                    CommitDirection(target);
                }
                else
                {
                    transitionTimer = transitionStepTime;
                    CommitDirection(transitionSteps[transitionStepIndex]);
                }
                return;
            }
        }

        if (newDir == dir)
        {
            pendingDir = -1;
            pendingDirTimer = 0f;
            return;
        }

        // 90° 经过 1 个中间方向，180° 经过 3 个；其它角度直接切。
        if (StartDirectionTransition(dir, newDir)) return;

        if (IsDiagonalRelax(dir, newDir))
        {
            // 短暂单轴输入：先挂起候选方向，持续够久仍没变回斜向才真正切换。
            if (newDir != pendingDir)
            {
                pendingDir = newDir;
                pendingDirTimer = 0f;
            }
            pendingDirTimer += Time.deltaTime;
            if (pendingDirTimer >= diagonalReleaseDelay)
                CommitDirection(newDir);
            return;
        }

        CommitDirection(newDir);
    }

    // 世界方向（角度从 +X 逆时针）→ 0..7 索引，与 DirNames 的映射一致。
    static int DirectionFromWorld(Vector2 v)
    {
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        return Mathf.RoundToInt(angle / 45f) % 8;
    }

    // 0..7 索引 → 世界方向。
    static Vector2 DirectionFromIndex(int index)
    {
        float radians = index * 45f * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    // 当前画面上显示的朝向（含斜向松键防抖、转身过渡）。
    // 动作区攻击判定用它，保证“看到的朝向 = 打出去的方向”。
    public bool TryGetVisualFacing(out Vector2 direction)
    {
        direction = Vector2.down;
        if (sr == null) return false;
        direction = DirectionFromIndex(dir);
        return true;
    }

    // 启动一次朝向过渡：90° 经过 1 个中间方向，180° 经过 3 个。
    // 返回 false 表示这个角度不做过过渡（或对应过渡被关闭）。
    bool StartDirectionTransition(int from, int to)
    {
        int delta = (to - from + 8) % 8;

        if (delta == 2 || delta == 6)
        {
            if (diagonalTransitionDuration <= 0f) return false;
            transitionSteps[0] = MidDirection(from, to);
            transitionStepCount = 1;
            transitionStepTime = diagonalTransitionDuration;
        }
        else if (delta == 4)
        {
            if (oppositeTransitionDuration <= 0f) return false;
            BuildOppositePath(from, transitionSteps);
            transitionStepCount = 3;
            transitionStepTime = oppositeTransitionDuration / 3f;
        }
        else
        {
            return false;
        }

        transitionTargetDir = to;
        transitionStepIndex = 0;
        transitionTimer = transitionStepTime;
        CommitDirection(transitionSteps[0]);
        return true;
    }

    // 取 from → to 的 90° 弧上的中间方向（45°）。
    static int MidDirection(int from, int to)
    {
        int delta = (to - from + 8) % 8;
        return (from + (delta <= 4 ? 1 : 7)) % 8;
    }

    // 180° 转身依次经过的 3 个中间方向。
    // 左右翻转（E↔W）走南侧，保证转身过程中正面朝向可见；其它情况走东侧。
    static void BuildOppositePath(int from, int[] steps)
    {
        int preferred = (from == 0 || from == 4) ? 6 : 0;
        int sign = ((preferred - from + 8) % 8) <= 4 ? 1 : -1;
        steps[0] = (from + sign + 8) % 8;
        steps[1] = (from + 2 * sign + 16) % 8;
        steps[2] = (from + 3 * sign + 24) % 8;
    }

    void EndTransition()
    {
        transitionTimer = 0f;
        transitionStepCount = 0;
        transitionStepIndex = 0;
        transitionTargetDir = -1;
    }

    // 世界方向索引里奇数是斜向（1=NE、3=NW、5=SW、7=SE），偶数是正方向。
    // 只有“斜向退化成自己相邻的正方向”才可能只是松键先后造成的抖动。
    static bool IsDiagonalRelax(int from, int to)
    {
        if ((from & 1) == 0) return false;
        if ((to & 1) != 0) return false;
        return to == (from + 7) % 8 || to == (from + 1) % 8;
    }

    void CommitDirection(int newDir)
    {
        dir = newDir;
        pendingDir = -1;
        pendingDirTimer = 0f;
        frame = 0;
        timer = 0f;
        if (turnDust && dustFx != null && turnDustTimer <= 0f)
        {
            dustFx.Burst(transform.position + Vector3.up * 0.06f, turnDustCount, 1.1f);
            turnDustTimer = turnDustCooldown;
        }
    }

    Sprite[] CurrentFrames()
    {
        // 优先级：死亡 > 闪避 > 受击 > 攻击 > 移动/待机。
        if (deathPlaying) return deathFrames[deathDir];
        if (dodgePlaying) return dodgeFrames[dodgeDir];
        if (hurtPlaying) return hurtFrames[hurtDir];
        var attack = ActiveAttackFrames();
        if (attack != null) return attack;

        bool sprinting = running && actionMotor != null && actionMotor.IsSprinting;
        var arr = running ? (sprinting ? sprintFrames : runFrames) : idleFrames;
        if (dir < 0 || dir >= arr.Length) return null;
        return arr[dir];
    }

    void Die()
    {
        if (dead) return;
        dead = true;
        running = false;
        attackPlaying = false;
        hurtPlaying = false;
        dodgePlaying = false;
        frame = 0;
        if (xraySr != null) xraySr.enabled = false;

        // 按当前朝向播死亡动画；没有素材时停在当前帧（旧行为）。
        deathDir = Mathf.Clamp(dir, 0, 7);
        var frames = deathFrames[deathDir];
        deathPlaying = frames != null && frames.Length > 0;
        deathFrame = 0;
        deathElapsed = 0f;

        sr.enabled = true;
        sr.color = Color.white;
        sr.shadowCastingMode = ShadowCastingMode.Off;
        if (dustFx != null)
            dustFx.SetEmitting(false, transform.position + Vector3.up * 0.06f);

        ApplyFrame(true);
        if (sr != null) ConfigureDissolveMaterial(sr.sharedMaterial);
        SetDissolve(0f);
    }

    // 主角使用独立的金色溶解，亮边沿不规则团块推进。
    static void ConfigureDissolveMaterial(Material mat)
    {
        if (mat == null) return;
        mat.SetFloat("_DissolveGold", 1f);
        mat.SetColor("_DissolveEdgeColor", new Color(2.4f, 1.25f, 0.18f, 1f));
        mat.SetFloat("_DissolveEdgeWidth", 0.12f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3000;
    }

    // 调试用：不经过伤害/体力系统，直接预览新动画。
    public void DebugPlayDodge()
    {
        dodgeDir = Mathf.Clamp(dir, 0, 7);
        dodgeFrame = 0;
        // 停在翻滚中段，方便截图/检查；正式闪避仍用 ActionCombatController.dodgeDuration。
        dodgeElapsed = 400f;
        dodgeDuration = 999f;
        dodgePlaying = true;
        hurtPlaying = false;
        attackPlaying = false;
        ApplyFrame(true);
    }

    // 调试用：把死亡消散停在 t 附近，方便截图确认中间态。
    public void DebugPlayDeathDissolve(float t = 0.5f)
    {
        DebugPlayDeath();
        deathFadeDelay = 0f;
        deathFadeDuration = 60f;
        deathElapsed = DeathClipDuration() + Mathf.Clamp01(t) * deathFadeDuration;
        UpdateDeathAnimation();
        ApplyFrame(true);
        UpdateDeathDissolve();
    }

    public void DebugPlayDeath()
    {
        dead = false;
        Die();
    }

    // 运行时调 XRay 剪影颜色（材质缓存会一起刷新）。
    public void SetXrayColor(Color c)
    {
        xrayColor = c;
        if (xraySr != null && xraySr.sprite != null)
            xraySr.sharedMaterial = XRayMaterial(xraySr.sprite.texture);
    }

    void ApplyFrame(bool force)
    {
        var frames = CurrentFrames();
        if (frames == null || frames.Length == 0) return;

        int index = deathPlaying ? Mathf.Clamp(deathFrame, 0, frames.Length - 1)
            : dodgePlaying ? Mathf.Clamp(dodgeFrame, 0, frames.Length - 1)
            : hurtPlaying ? Mathf.Clamp(hurtFrame, 0, frames.Length - 1)
            : ActiveAttackFrames() != null ? Mathf.Clamp(attackFrame, 0, frames.Length - 1)
            : Mathf.Clamp(frame % frames.Length, 0, frames.Length - 1);
        var sprite = frames[index];
        if (!force && sr.sprite == sprite) return;

        sr.sprite = sprite;
        var tex = sprite.texture;
        var mat = LitMaterial(tex);
        if (mat != null) sr.sharedMaterial = mat;

        if (xraySr != null)
        {
            xraySr.sprite = sprite;
            var xmat = XRayMaterial(tex);
            if (xmat != null) xraySr.sharedMaterial = xmat;
        }
    }

    void LoadAll()
    {
        for (int i = 0; i < 8; i++)
        {
            runFrames[i] = LoadSheet($"{runningRoot}/run_{DirNames[i]}");
            idleFrames[i] = LoadSheet($"{idleRoot}/idle_{DirNames[i]}");
            sprintFrames[i] = LoadSheet($"{sprintRoot}/sprint_{DirNames[i]}", 8, false);
            hurtFrames[i] = LoadSheet($"{hurtRoot}/hurt_{DirNames[i]}", 6, false, true);
            dodgeFrames[i] = LoadSheet($"{dodgeRoot}/dodge_{DirNames[i]}", 9, true, true);
            deathFrames[i] = LoadSheet($"{deathRoot}/death_{DirNames[i]}", 9, true, true, true);
            for (int step = 0; step < comboFrames.Length; step++)
                comboFrames[step][i] = LoadSheet(
                    $"{attackRoot}/{AttackFolders[step]}/attack_{DirNames[i]}", 8, true, true);
        }

        // 文件缺失时的兜底：北 -> NE/NW；南 -> E/W。
        // 正常情况下八个方向都使用各自素材。
        for (int i = 0; i < 8; i++)
        {
            if (idleFrames[i] != null) continue;
            int fallback = (i == 1 || i == 2 || i == 3) ? 2 : 6;
            idleFrames[i] = idleFrames[fallback];
        }

        // 跑步表缺失时至少不要空着
        for (int i = 0; i < 8; i++)
        {
            if (runFrames[i] != null) continue;
            runFrames[i] = idleFrames[i];
        }
        for (int i = 0; i < 8; i++)
            if (sprintFrames[i] == null) sprintFrames[i] = runFrames[i];
    }

    Sprite[] LoadSheet(string path, int expectedFrames = -1, bool warnIfMissing = true,
        bool firstFramePivot = false, bool useSheetFrameSize = false)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null)
        {
            if (warnIfMissing)
                Debug.LogWarning($"[Hd2dPixelPlayer] 找不到精灵表 Resources/{path}");
            return null;
        }

        // 导入尺寸不对时（例如被 maxTextureSize 压成 512x32）直接放弃这张表，
        // 避免 Sprite.Create 越界抛异常把整个角色的 Awake 打断。
        if (tex.width < frameWidth || tex.height < frameHeight)
        {
            Debug.LogWarning($"[Hd2dPixelPlayer] 精灵表尺寸异常 {tex.width}x{tex.height}：" +
                $"Resources/{path}（需要至少 {frameWidth}x{frameHeight}）");
            return null;
        }

        // 死亡动作的斜向画布为 68px，保留倒地时伸出的身体，不裁成 64px。
        int tileWidth = useSheetFrameSize ? tex.height : frameWidth;
        int tileHeight = useSheetFrameSize ? tex.height : frameHeight;
        int wanted = expectedFrames > 0 ? expectedFrames : frameCount;
        int count = Mathf.Min(wanted, Mathf.Max(1, tex.width / Mathf.Max(1, tileWidth)));
        float pivotY = ComputePivotY(tex, firstFramePivot, tileWidth, tileHeight);
        var frames = new Sprite[count];
        var pivot = new Vector2(0.5f, pivotY);

        for (int i = 0; i < count; i++)
        {
            var rect = new Rect(i * tileWidth, 0f, tileWidth, tileHeight);
            frames[i] = Sprite.Create(tex, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            frames[i].name = $"{path}_{i}";
        }
        return frames;
    }

    // 枢轴放在整张表最低的脚底像素上：角色原点即落脚点。
    // 纹理需在导入设置里勾选 Read/Write（Cat8DirImportSettings 负责）。
    float ComputePivotY(Texture2D tex, bool firstFrameOnly = false,
        int sheetFrameWidth = 0, int sheetFrameHeight = 0)
    {
        int tileWidth = sheetFrameWidth > 0 ? sheetFrameWidth : frameWidth;
        int tileHeight = sheetFrameHeight > 0 ? sheetFrameHeight : frameHeight;
        try
        {
            var px = tex.GetPixels32();
            int w = tex.width;
            int h = tex.height;
            int minY = h;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                // 攻击/受击用第 0 帧站姿的脚底；上勾拳特效不能改变整个动作的落脚点。
                int scanWidth = firstFrameOnly ? Mathf.Min(w, tileWidth) : w;
                for (int x = 0; x < scanWidth; x++)
                {
                    if (px[row + x].a > 8)
                    {
                        if (y < minY) minY = y;
                        break;
                    }
                }
            }
            if (minY >= h) return 0.5f;
            return Mathf.Clamp01((float)minY / Mathf.Max(1, tileHeight));
        }
        catch (UnityException)
        {
            // 纹理不可读时的保守值（北/南待机脚底约 10px）
            return 10f / Mathf.Max(1, tileHeight);
        }
    }

    Material LitMaterial(Texture tex)
    {
        if (litShader == null) return null;
        if (tex != null && litMats.TryGetValue(tex, out var cached) && cached != null)
            return cached;

        var mat = new Material(litShader)
        {
            name = "PixelCatLit (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
        };
        mat.SetTexture("_MainTex", tex);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_AmbientBoost", litAmbient);
        mat.SetColor("_Color", tint);
        mat.SetFloat("_OutlineWidth", 0f);
        mat.SetColor("_OutlineColor", Color.clear);
        mat.SetFloat("_Dissolve", 0f);
        mat.SetInt("_ZTest", 4);   // LEqual
        mat.SetInt("_ZWrite", 1);
        mat.renderQueue = 2600;

        if (tex != null) litMats[tex] = mat;
        return mat;
    }

    Material XRayMaterial(Texture tex)
    {
        if (xrayShader == null) return null;
        if (tex != null && xrayMats.TryGetValue(tex, out var cached) && cached != null)
        {
            cached.SetColor("_Color", xrayColor);
            return cached;
        }

        var mat = new Material(xrayShader)
        {
            name = "PixelCatXRay (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
        };
        mat.SetTexture("_MainTex", tex);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetColor("_Color", xrayColor);
        mat.renderQueue = 2601;

        if (tex != null) xrayMats[tex] = mat;
        return mat;
    }

    void SetupGroundShadow()
    {
        var go = new GameObject("PlayerShadow");
        go.transform.SetParent(transform, false);
        var shadow = go.AddComponent<Hd2dBlobShadow>();
        shadow.follow = transform;
        shadow.diameter = shadowDiameter;
        shadow.depth = shadowDepth;
        shadow.opacity = shadowOpacity;
        blobShadow = shadow;
    }
}
