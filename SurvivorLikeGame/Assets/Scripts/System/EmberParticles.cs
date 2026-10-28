using UnityEngine;
using UnityEngine.SceneManagement;

// 场景氛围粒子：金色余烬 + 少量尘埃，围绕当前镜头缓慢上飘。
// 场景加载后自动创建，跟随场景销毁；重开场景时由 sceneLoaded 重新创建。
// 全部粒子跑在世界空间，镜头移动时已经生成的火星会留在原地，形成自然的拖尾。
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)] // 晚于 CameraFollow，取到最终相机位置
public class EmberParticles : MonoBehaviour
{
    [Header("发射")]
    [Tooltip("金色余烬每秒发射数量")]
    [Min(0f)] public float emberRate = 12f;

    [Tooltip("尘埃每秒发射数量")]
    [Min(0f)] public float dustRate = 3f;

    [Tooltip("生成区域相对屏幕的外扩（世界单位）")]
    [Min(0f)] public float spawnPadding = 2f;

    [Header("余烬")]
    public Color emberColor = new Color(1f, 0.78f, 0.4f, 0.6f);
    public Color emberColorAlt = new Color(1f, 0.92f, 0.64f, 0.5f);
    [Range(0.01f, 0.2f)] public float emberSizeMin = 0.05f;
    [Range(0.01f, 0.2f)] public float emberSizeMax = 0.11f;

    [Header("尘埃")]
    public Color dustColor = new Color(0.62f, 0.68f, 0.8f, 0.14f);
    [Range(0.01f, 0.2f)] public float dustSizeMin = 0.02f;
    [Range(0.01f, 0.2f)] public float dustSizeMax = 0.05f;

    const int EmberSortingOrder = -5; // 高于背景(-10)、低于角色(0)

    Camera cam;
    ParticleSystem embers;
    ParticleSystem dust;
    Texture2D dotTexture;
    Material emberMaterial;
    Material dustMaterial;
    float lastAreaWidth;
    float lastAreaHeight;
    float appliedEmberRate;
    float appliedDustRate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHooks()
    {
        // 关闭 Domain Reload 时先清掉上一次 Play 留下的静态订阅
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CreateIfNeeded();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 跟随当前场景重建，不做 DontDestroyOnLoad
        CreateIfNeeded();
    }

    static void CreateIfNeeded()
    {
        if (Camera.main == null) return; // 无主相机时跳过，不报错
        if (FindFirstObjectByType<EmberParticles>() != null) return;

        var go = new GameObject("EmberParticles");
        go.AddComponent<EmberParticles>();
    }

    void Awake()
    {
        cam = Camera.main;
        dotTexture = CreateSoftDot();

        emberMaterial = CreateParticleMaterial(true, dotTexture);
        dustMaterial = CreateParticleMaterial(false, dotTexture);

        embers = CreateSystem(
            "Embers", transform, emberMaterial, emberColor, emberColorAlt,
            emberSizeMin, emberSizeMax, emberRate,
            riseMin: 0.08f, riseMax: 0.24f,
            lifeMin: 4f, lifeMax: 8f, maxParticles: 160,
            additive: true);

        dust = CreateSystem(
            "Dust", transform, dustMaterial, dustColor, dustColor,
            dustSizeMin, dustSizeMax, dustRate,
            riseMin: 0.02f, riseMax: 0.07f,
            lifeMin: 5f, lifeMax: 8f, maxParticles: 60,
            additive: false);

        appliedEmberRate = emberRate;
        appliedDustRate = dustRate;

        FollowCamera(forceArea: true);
    }

    void OnEnable()
    {
        // 组件重新启用时恢复发射；场景刚创建时也走这里
        if (embers != null) embers.Play();
        if (dust != null) dust.Play();
    }

    void OnDisable()
    {
        // 关闭组件即停止发射并清空现有粒子，画面立刻干净
        if (embers != null) embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnDestroy()
    {
        if (emberMaterial != null) Destroy(emberMaterial);
        if (dustMaterial != null) Destroy(dustMaterial);
        if (dotTexture != null) Destroy(dotTexture);
    }

    void Update()
    {
        // 允许运行时在 Inspector 调整发射量；只在数值变化时写回模块
        ApplyRateIfChanged(embers, ref appliedEmberRate, emberRate);
        ApplyRateIfChanged(dust, ref appliedDustRate, dustRate);
    }

    void LateUpdate()
    {
        FollowCamera(forceArea: false);
    }

    static void ApplyRateIfChanged(ParticleSystem ps, ref float applied, float value)
    {
        if (ps == null || Mathf.Approximately(applied, value)) return;
        applied = value;
        var emission = ps.emission;
        emission.rateOverTime = value;
    }

    // 发射区域跟随相机中心；世界空间粒子会自然留在身后
    void FollowCamera(bool forceArea)
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        var pos = cam.transform.position;
        transform.position = new Vector3(pos.x, pos.y, 0f);

        float baseHeight = cam.orthographic ? cam.orthographicSize * 2f : 10f;
        float height = baseHeight + spawnPadding * 2f;
        float width = baseHeight * Mathf.Max(cam.aspect, 0.5f) + spawnPadding * 2f;

        if (!forceArea &&
            Mathf.Abs(width - lastAreaWidth) < 0.01f &&
            Mathf.Abs(height - lastAreaHeight) < 0.01f)
            return;

        lastAreaWidth = width;
        lastAreaHeight = height;

        SetShapeScale(embers, width, height);
        SetShapeScale(dust, width, height);
    }

    static void SetShapeScale(ParticleSystem ps, float width, float height)
    {
        if (ps == null) return;
        var shape = ps.shape;
        shape.scale = new Vector3(width, height, 0.1f);
    }

    // 运行时创建一颗粒子系统；所有模块只在这里配置一次，避免逐帧 GC
    static ParticleSystem CreateSystem(
        string name, Transform parent, Material material, Color colorA, Color colorB,
        float sizeMin, float sizeMax, float rate,
        float riseMin, float riseMax, float lifeMin, float lifeMax,
        int maxParticles, bool additive)
    {
        var go = new GameObject(name);
        // 挂在根节点下：根节点跟随相机，粒子本身用世界空间模拟，已生成的火星留在原地
        go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = false; // 由 OnEnable 统一启动，等位置与区域就绪
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.useUnscaledTime = true; // 标题页/升级暂停时 timeScale=0，余烬仍缓慢飘动
        main.startSpeed = 0f; // 起始速度交给 Velocity over Lifetime，保证向上飘
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = additive ? -0.006f : -0.002f; // 负值 = 缓慢上浮
        main.maxParticles = maxParticles;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1f, 1f, 0.1f); // 具体尺寸由 FollowCamera 写入

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;

        // 上飘 + 轻微横向漂移；x/y/z 必须使用同一种曲线模式，否则 Unity 每帧报错
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        velocity.y = new ParticleSystem.MinMaxCurve(riseMin, riseMax);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 生命周期内淡入淡出，避免粒子凭空出现/消失
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, additive ? 0.12f : 0.25f),
                new GradientAlphaKey(additive ? 0.8f : 0.55f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        // 先胀后缩，像余烬明灭
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0.15f)));

        // 余烬加一点湍流，飘得更自然；尘埃保持笔直
        var noise = ps.noise;
        noise.enabled = additive;
        if (additive)
        {
            noise.strength = 0.05f;
            noise.frequency = 0.12f;
            noise.scrollSpeed = 0.03f;
            noise.damping = true;
            noise.octaveCount = 1;
        }

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingLayerID = 0;
        renderer.sortingOrder = EmberSortingOrder;
        renderer.maxParticleSize = 0.5f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = material;

        return ps;
    }

    static Material CreateParticleMaterial(bool additive, Texture2D texture)
    {
        var shader = FindParticleShader(additive);
        if (shader == null)
        {
            Debug.LogWarning("[EmberParticles] 找不到粒子着色器，粒子已跳过。");
            return null;
        }

        return new Material(shader)
        {
            name = additive ? "EmberAdditive (Runtime)" : "DustAlpha (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = texture
        };
    }

    static Shader FindParticleShader(bool additive)
    {
        // Built-in RP 自带的旧版粒子着色器；逐级兜底，避免粉红材质
        var shader = Shader.Find(additive
            ? "Legacy Shaders/Particles/Additive"
            : "Legacy Shaders/Particles/Alpha Blended");
        if (shader == null)
            shader = Shader.Find(additive ? "Particles/Additive" : "Particles/Alpha Blended");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        return shader;
    }

    // 运行时生成一张 32x32 的柔和圆点，不需要额外美术资源
    static Texture2D CreateSoftDot()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "EmberSoftDot",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];
        const float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // 收窄亮核、保留柔和边缘，让火星在亮地板上也能读出来
                float a = Mathf.Clamp01(1f - d * 1.2f);
                a = a * a * (3f - 2f * a); // smoothstep，边缘更柔和
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true); // 不再需要 CPU 端数据
        return texture;
    }
}