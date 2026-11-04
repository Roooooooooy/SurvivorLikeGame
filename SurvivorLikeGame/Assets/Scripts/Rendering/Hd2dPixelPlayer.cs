using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 8 方向像素主角视图。
// - 逻辑/碰撞仍在 PlayerController 的 XY 平面
// - 视觉根节点映射到世界 (x, 0, y)，面向相机（billboard）
// - 每个方向 8 帧横向精灵表，跑步/待机切换
// - 待机缺失方向用北/南占位（北：NE/NW，南：E/W/SE/SW）
[DisallowMultipleComponent]
public class Hd2dPixelPlayer : MonoBehaviour
{
    public static Hd2dPixelPlayer Instance { get; private set; }
    public static bool Active => Instance != null && Instance.sr != null;

    [Header("精灵表（Resources 路径）")]
    public string runningRoot = "Player/Cat8Dir/Running";
    public string idleRoot = "Player/Cat8Dir/Idle";
    public int frameCount = 8;
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
    readonly Dictionary<Texture, Material> litMats = new Dictionary<Texture, Material>();
    readonly Dictionary<Texture, Material> xrayMats = new Dictionary<Texture, Material>();

    SpriteRenderer sr;
    SpriteRenderer xraySr;
    Hd2dFootDust dustFx;
    float turnDustTimer;
    Transform player;
    Rigidbody2D rb;
    PlayerController pc;
    Camera cam;
    Shader litShader;
    Shader xrayShader;

    int dir;
    int frame;
    float timer;
    bool running;
    bool dead;
    int lastHealth;
    float flashTimer;

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
    }

    void Start()
    {
        pc = PlayerController.Instance;
        if (pc != null)
        {
            player = pc.transform;
            rb = pc.GetComponent<Rigidbody2D>();
            lastHealth = pc.currentHealth;

            // 旧的可视化全部让位：Prefab 上的 SpriteRenderer 和自动挂的 PlayerAnimation
            var legacy = pc.GetComponent<SpriteRenderer>();
            if (legacy != null)
            {
                legacy.enabled = false;
                legacy.forceRenderingOff = true;
            }
            var oldAnim = pc.GetComponent<PlayerAnimation>();
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

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
            pc = PlayerController.Instance;
            if (pc == null) return;
            player = pc.transform;
            rb = pc.GetComponent<Rigidbody2D>();
            lastHealth = pc.currentHealth;
        }
        if (cam == null) cam = Camera.main;

        Vector3 p = player.position;
        transform.position = new Vector3(p.x, worldYOffset, p.y);
        transform.localScale = Vector3.one * viewScale;

        if (cam != null)
        {
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
        }

        if (dead) return;

        var gm = GameManager.Instance;
        if (gm != null && gm.isGameOver)
        {
            Die();
            return;
        }

        if (pc != null)
        {
            if (lastHealth > 0 && pc.currentHealth < lastHealth)
                flashTimer = flashDuration;
            lastHealth = pc.currentHealth;
        }

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

        if (moving)
        {
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            int newDir = Mathf.RoundToInt(angle / 45f) % 8;
            if (newDir != dir)
            {
                dir = newDir;
                frame = 0;
                timer = 0f;
                if (turnDust && dustFx != null && turnDustTimer <= 0f)
                {
                    dustFx.Burst(transform.position + Vector3.up * 0.06f, turnDustCount, 1.1f);
                    turnDustTimer = turnDustCooldown;
                }
            }
        }

        if (moving != running)
        {
            if (!moving && running && turnDust && dustFx != null)
                dustFx.Burst(transform.position + Vector3.up * 0.06f, stopDustCount, 1.25f);
            running = moving;
            frame = 0;
            timer = 0f;
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

        ApplyFrame(false);
    }

    Sprite[] CurrentFrames()
    {
        var arr = running ? runFrames : idleFrames;
        if (dir < 0 || dir >= arr.Length) return null;
        return arr[dir];
    }

    void Die()
    {
        if (dead) return;
        dead = true;
        running = false;
        frame = 0;
        sr.color = new Color(0.55f, 0.55f, 0.60f, 1f);
        if (xraySr != null) xraySr.enabled = false;
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

        int index = Mathf.Clamp(frame % frames.Length, 0, frames.Length - 1);
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
        }

        // 待机占位：北 -> NE/NW；南 -> E/W/SE/SW
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
    }

    Sprite[] LoadSheet(string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null)
        {
            Debug.LogWarning($"[Hd2dPixelPlayer] 找不到精灵表 Resources/{path}");
            return null;
        }

        int count = Mathf.Min(frameCount, Mathf.Max(1, tex.width / Mathf.Max(1, frameWidth)));
        float pivotY = ComputePivotY(tex);
        var frames = new Sprite[count];
        var pivot = new Vector2(0.5f, pivotY);

        for (int i = 0; i < count; i++)
        {
            var rect = new Rect(i * frameWidth, 0f, frameWidth, frameHeight);
            frames[i] = Sprite.Create(tex, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            frames[i].name = $"{path}_{i}";
        }
        return frames;
    }

    // 枢轴放在整张表最低的脚底像素上：角色原点即落脚点。
    // 纹理需在导入设置里勾选 Read/Write（Cat8DirImportSettings 负责）。
    float ComputePivotY(Texture2D tex)
    {
        try
        {
            var px = tex.GetPixels32();
            int w = tex.width;
            int h = tex.height;
            int minY = h;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (px[row + x].a > 8)
                    {
                        if (y < minY) minY = y;
                        break;
                    }
                }
            }
            if (minY >= h) return 0.5f;
            return Mathf.Clamp01((float)minY / Mathf.Max(1, frameHeight));
        }
        catch (UnityException)
        {
            // 纹理不可读时的保守值（北/南待机脚底约 10px）
            return 10f / Mathf.Max(1, frameHeight);
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
    }
}
