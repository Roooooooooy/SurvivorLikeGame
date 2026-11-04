using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

// HD-2D 主角 Spine 视图：
//   - 逻辑/碰撞仍在 PlayerController 的 XY 平面
//   - 这里只负责把 Catalyst 猫 Spine 放到 (x, 0, y)，面向相机，并根据移动/受击/攻击/死亡切换动作
//   - 提供 SetSkin / SetAttachment 接口，方便后续装备/换装系统
public class Hd2dSpinePlayer : MonoBehaviour
{
    public static Hd2dSpinePlayer Instance { get; private set; }
    public static bool Active => Instance != null && Instance.anim != null;

    [Header("Spine 数据")]
    public SkeletonDataAsset skeletonDataAsset;

    [Header("动画名")]
    public string defaultSkin = "default";
    public string idleAnim = "eyes blink";
    public string walkAnim = "walk";
    public string runAnim = "run";
    public string attackAnim = "attack-str";
    public string hitAnim = "hit";
    public string deathAnim = "defeat";
    public bool equipDemoGear = true;

    [Header("映射")]
    public float viewScale = 0.8f;
    public float worldYOffset = 0f;
    public bool invertFacing = true;
    public float runSpeedThreshold = 6f;

    [Header("节奏")]
    public float attackCooldown = 0.8f;
    public float hitCooldown = 0.45f;

    [Header("遮挡 X 光")]
    [Tooltip("被前景墙面/障碍/大怪挡住时，用低透明度剪影标出主角位置")]
    public bool xray = true;
    public Color xrayColor = new Color(0.42f, 0.66f, 1.0f, 0.30f);

    [Header("接地阴影")]
    public bool groundShadow = true;

    SkeletonAnimation anim;
    Transform view;
    Transform player;
    Rigidbody2D rb;
    PlayerController pc;
    Camera cam;
    MeshFilter sourceFilter;
    MeshRenderer sourceRenderer;
    MeshFilter xrayFilter;
    MeshRenderer xrayRenderer;
    Material xrayMaterial;

    string currentAnim;
    float attackTimer;
    float hitTimer;
    float lastAttackTime = -99f;
    float lastHitTime = -99f;
    int lastHealth;
    bool dead;
    bool facingRight = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (skeletonDataAsset == null)
            skeletonDataAsset = Resources.Load<SkeletonDataAsset>("Player/CatSpine/cat_spine_SkeletonData");
        if (skeletonDataAsset == null)
        {
            Debug.LogError("[HD2D] 找不到猫 Spine SkeletonDataAsset");
            return;
        }

        var go = SkeletonAnimation.NewSkeletonAnimationGameObject(skeletonDataAsset);
        go.name = "SpineCat";
        anim = go.GetComponent<SkeletonAnimation>();
        view = go.transform;
        view.SetParent(transform, false);
        view.localPosition = Vector3.zero;
        view.localScale = Vector3.one * viewScale;

        SetupXRay(go.gameObject);
        if (groundShadow) SetupGroundShadow();

        anim.Skeleton.SetSkin(defaultSkin);
        anim.Skeleton.SetSlotsToSetupPose();
        if (equipDemoGear)
            EquipGear("cat_hat_wizard", "cat_accessory_glasses", null);
        Play(idleAnim, true);

        CombatEvents.HitDetailed += OnHitDetailed;
    }

    void Start()
    {
        pc = PlayerController.Instance;
        if (pc != null)
        {
            player = pc.transform;
            rb = pc.GetComponent<Rigidbody2D>();
            lastHealth = pc.currentHealth;
        }
        cam = Camera.main;

        // 让 SpriteRenderer 视觉桥不要再给玩家创建 2D 代理
        if (Hd2dSpriteBridge.Instance != null)
            Hd2dSpriteBridge.Instance.SuppressPlayerProxy();
    }

    void OnDestroy()
    {
        CombatEvents.HitDetailed -= OnHitDetailed;
        if (xrayMaterial != null) Destroy(xrayMaterial);
        if (Instance == this) Instance = null;
    }

    void OnHitDetailed(Vector2 pos, int damage, bool crit)
    {
        if (dead || player == null || hitTimer > 0f) return;
        if (Time.time - lastAttackTime < attackCooldown) return;
        lastAttackTime = Time.time;
        attackTimer = Mathf.Max(0.2f, Duration(attackAnim));
        Play(attackAnim, false);
    }

    void LateUpdate()
    {
        if (anim == null) return;

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

        if (cam != null)
        {
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
        }

        SyncXRay();

        if (dead) return;

        var gm = GameManager.Instance;
        if ((gm != null && gm.isGameOver) || (pc != null && pc.currentHealth <= 0))
        {
            Die();
            return;
        }

        if (pc != null && pc.currentHealth < lastHealth &&
            Time.time - lastHitTime >= hitCooldown)
        {
            lastHitTime = Time.time;
            hitTimer = Mathf.Max(0.2f, Duration(hitAnim));
            Play(hitAnim, false);
        }
        if (pc != null) lastHealth = pc.currentHealth;

        float dt = Time.deltaTime;
        if (hitTimer > 0f) hitTimer -= dt;
        if (attackTimer > 0f) attackTimer -= dt;

        if (rb != null)
        {
            float vx = rb.linearVelocity.x;
            if (Mathf.Abs(vx) > 0.1f)
                facingRight = (vx > 0f) != invertFacing;
        }
        anim.Skeleton.ScaleX = facingRight ? 1f : -1f;

        if (hitTimer <= 0f && attackTimer <= 0f)
            PlayLocomotion();
    }

    void PlayLocomotion()
    {
        if (rb == null)
        {
            Play(idleAnim, true);
            return;
        }

        float speed = rb.linearVelocity.sqrMagnitude;
        if (speed > runSpeedThreshold * runSpeedThreshold)
            Play(runAnim, true);
        else if (speed > 0.01f)
            Play(walkAnim, true);
        else
            Play(idleAnim, true);
    }

    void Play(string name, bool loop)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (currentAnim == name)
        {
            if (loop) return;
        }
        var data = anim.Skeleton.Data;
        if (data.FindAnimation(name) == null) return;
        anim.AnimationState.SetAnimation(0, name, loop);
        currentAnim = name;
    }

    float Duration(string name)
    {
        if (anim == null) return 0.4f;
        var a = anim.Skeleton.Data.FindAnimation(name);
        return a != null ? a.Duration : 0.4f;
    }

    void Die()
    {
        if (dead) return;
        dead = true;
        Play(deathAnim, false);
    }

    void SetupGroundShadow()
    {
        var go = new GameObject("PlayerShadow");
        go.transform.SetParent(transform, false);
        var shadow = go.AddComponent<Hd2dBlobShadow>();
        shadow.follow = transform;
    }

    // ---- 遮挡 X 光 ----
    // 主体继续走正常深度；这里复用 Spine 已经生成好的网格，再挂一个
    // ZTest Greater 的低透明度剪影，只在被前景挡住的像素上出现。
    void SetupXRay(GameObject skeletonGo)
    {
        if (!xray) return;

        var shader = Shader.Find("HD2D/SpineXRay");
        if (shader == null)
        {
            Debug.LogWarning("[HD2D] 找不到 HD2D/SpineXRay，主角被遮挡时不会有透视剪影");
            return;
        }

        sourceFilter = skeletonGo.GetComponent<MeshFilter>();
        sourceRenderer = skeletonGo.GetComponent<MeshRenderer>();
        if (sourceFilter == null || sourceRenderer == null) return;

        var xgo = new GameObject("SpineCatXRay");
        xgo.transform.SetParent(view, false);
        xrayFilter = xgo.AddComponent<MeshFilter>();
        xrayRenderer = xgo.AddComponent<MeshRenderer>();
        xrayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        xrayRenderer.receiveShadows = false;

        xrayMaterial = new Material(shader)
        {
            name = "SpineCatXRay (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
        };
        xrayMaterial.SetFloat("_Cutoff", 0.5f);
        xrayMaterial.SetColor("_Color", xrayColor);
        xrayMaterial.renderQueue = 3001;
        xrayRenderer.sharedMaterial = xrayMaterial;
        xrayRenderer.enabled = false; // 网格/贴图就绪后再开
    }

    void SyncXRay()
    {
        if (xrayRenderer == null || sourceFilter == null) return;

        var mesh = sourceFilter.sharedMesh;
        if (mesh == null || mesh.vertexCount == 0)
        {
            xrayRenderer.enabled = false;
            return;
        }

        if (xrayFilter.sharedMesh != mesh) xrayFilter.sharedMesh = mesh;

        // 贴图来自 Spine 图集材质，运行时再取一次，确保 SkeletonRenderer 已初始化
        var mat = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
        var tex = mat != null ? mat.mainTexture : null;
        if (tex == null)
        {
            xrayRenderer.enabled = false;
            return;
        }
        if (xrayMaterial.GetTexture("_MainTex") != tex)
            xrayMaterial.SetTexture("_MainTex", tex);
        xrayMaterial.SetColor("_Color", xrayColor);

        xrayRenderer.enabled = true;
    }

    // ---- 换装 / 装备接口 ----
    public string[] SkinNames()
    {
        if (anim == null) return new string[0];
        var skins = anim.Skeleton.Data.Skins;
        var names = new string[skins.Count];
        for (int i = 0; i < skins.Count; i++) names[i] = skins.Items[i].Name;
        return names;
    }

    public void EquipSkin(string skinName)
    {
        if (anim == null || string.IsNullOrEmpty(skinName)) return;
        if (anim.Skeleton.Data.FindSkin(skinName) == null) return;
        anim.Skeleton.SetSkin(skinName);
        anim.Skeleton.SetSlotsToSetupPose();
    }

    public void EquipAttachment(string slotName, string attachmentName)
    {
        if (anim == null) return;
        anim.Skeleton.SetAttachment(slotName, attachmentName);
    }

    // 基础猫（custom skin）+ 默认 skin 里的装备附件；后续换装系统可直接调用
    public void EquipGear(string hat = null, string accessory = null, string wings = null)
    {
        if (anim == null) return;
        var data = anim.Skeleton.Data;
        var baseSkin = data.FindSkin("custom");
        var gearSkin = data.FindSkin("default");
        if (baseSkin == null && gearSkin == null) return;

        var combined = new Skin("custom_gear");
        if (gearSkin != null) combined.AddSkin(gearSkin);
        if (baseSkin != null) combined.AddSkin(baseSkin);
        anim.Skeleton.SetSkin(combined);
        anim.Skeleton.SetSlotsToSetupPose();

        if (!string.IsNullOrEmpty(hat)) anim.Skeleton.SetAttachment("hat", hat);
        if (!string.IsNullOrEmpty(accessory)) anim.Skeleton.SetAttachment("accessory", accessory);
        if (!string.IsNullOrEmpty(wings))
        {
            anim.Skeleton.SetAttachment("front wing", wings);
            anim.Skeleton.SetAttachment("back wing", wings);
        }
    }
}
