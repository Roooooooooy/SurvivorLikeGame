using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 标题页动态背景：四个菜单选项对应四套完全不同的视觉系统。
//   0 开始游戏 —— 星云法阵：程序化星云 + 双层符文 + 火星漩涡 + 冲击波 + 流星
//   1 永久商店 —— 黄金宝库：金色光柱 + 下落旋转的金币 + 金色尘埃
//   2 选择角色 —— 灵魂之流：青蓝极光帘幕 + 缓慢环绕的灵魂光球
//   3 选择难度 —— 血月雷雨：暗红雷云 + 竖向细雨 + 随机闪电 + 全局爆闪；
//      难度越高，雨越密越快、雷越频繁、颜色越深越重
// 切换选项时：旧的整套特效淡出、新的淡入，同时最深层的程序化背景做纹理级交叉淡化，
// 再从法阵中心放出一圈新主色的过渡波。全部使用 unscaled 时间。
// 贴图来自 Resources/TitleUI（Kenney Particle Pack，CC0）与项目内金币图标。
public class TitleBackdrop : MonoBehaviour
{
    const string ArtDir = "TitleUI/";

    enum Role
    {
        Primary,
        Secondary,
        Accent,
    }

    enum PlasmaMode
    {
        Nebula,
        GoldDust,
        Aurora,
        Storm,
    }

    // 一个菜单选项对应的背景“情绪”：主/副/强调色 + 各层权重与速度
    sealed class MoodPalette
    {
        public Color primary = Color.white;
        public Color secondary = Color.white;
        public Color accent = Color.white;
        public float cloudWeight = 1f;
        public float ribbonWeight = 1f;
        public float rimWeight = 1f;
        public float emberScale = 1f;
        public float pulseScale = 1f;
        public float cometScale = 1f;
        public float flow = 1f;
        public float rune = 1f;
        public float vortex = 1f;
        public float pulseRate = 1f;
    }

    sealed class Drifter
    {
        public RectTransform rt;
        public Image image;
        public Vector2 basePosition;
        public Vector2 amplitude;
        public float period;
        public float phase;
        public float baseAlpha;
        public float alphaSwing;
        public float spin;
        public Role role;
    }

    sealed class Ember
    {
        public RectTransform rt;
        public Image image;
        public float baseX;
        public float y;
        public float speed;
        public float sway;
        public float swaySpeed;
        public float phase;
        public float baseAlpha;
        public float halfHeight;
        public float halfWidth;
        public Role role;
    }

    sealed class VortexEmber
    {
        public RectTransform rt;
        public Image image;
        public float angle;
        public float speed;
        public float radius;
        public float centerY;
        public float wobbleSpeed;
        public float wobbleAmp;
        public float phase;
        public float baseAlpha;
        public Role role;
    }

    sealed class RuneRing
    {
        public RectTransform rt;
        public Image image;
        public Vector2 basePosition;
        public float angle;
        public float speed;
        public float pulseSpeed;
        public float baseAlpha;
        public Role role;
    }

    sealed class PulseRing
    {
        public RectTransform rt;
        public Image image;
        public Vector2 basePosition;
        public float offset;
        public float period;
        public float baseSize;
        public float baseAlpha;
        public Role role;
    }

    sealed class Comet
    {
        public RectTransform rt;
        public Image image;
        public float period;
        public float offset;
        public Vector2 start;
        public Vector2 end;
        public float angle;
        public float baseAlpha;
        public Role role;
    }

    sealed class Coin
    {
        public RectTransform rt;
        public Image image;
        public float baseX;
        public float y;
        public float speed;
        public float sway;
        public float swaySpeed;
        public float phase;
        public float flip;
        public float flipSpeed;
        public float baseAlpha;
        public float halfW;
        public float halfH;
        public Role role;
    }

    sealed class AuroraRibbon
    {
        public RectTransform rt;
        public Image image;
        public Vector2 basePosition;
        public float swayAmp;
        public float swaySpeed;
        public float riseSpeed;
        public float phase;
        public float baseAlpha;
        public Role role;
    }

    sealed class SoulOrb
    {
        public RectTransform rt;
        public Image image;
        public float angle;
        public float speed;
        public float radius;
        public float centerY;
        public float phase;
        public float baseAlpha;
        public Role role;
    }

    sealed class RainDrop
    {
        public RectTransform rt;
        public Image image;
        public float x;
        public float y;
        public float speed;
        public float length;
        public float baseAlpha;
        public float threshold; // 难度越高，越多雨滴越过可见阈值
        public float phase;
        public float halfW;
        public float halfH;
        public Role role;
    }

    sealed class LightningBolt
    {
        public RectTransform root;
        public Image[] segments;
        public float timer;
        public float flash;
        public float width;
        public int index;
    }

    const int PlasmaW = 160;
    const int PlasmaH = 90;
    const float PlasmaFps = 30f;

    readonly List<Drifter> drifters = new List<Drifter>();
    readonly List<Ember> embers = new List<Ember>();
    readonly List<VortexEmber> vortexEmbers = new List<VortexEmber>();
    readonly List<RuneRing> runeRings = new List<RuneRing>();
    readonly List<PulseRing> pulseRings = new List<PulseRing>();
    readonly List<Comet> comets = new List<Comet>();
    readonly List<Coin> coins = new List<Coin>();
    readonly List<AuroraRibbon> ribbons = new List<AuroraRibbon>();
    readonly List<SoulOrb> soulOrbs = new List<SoulOrb>();
    readonly List<RainDrop> rainDrops = new List<RainDrop>();
    readonly List<LightningBolt> bolts = new List<LightningBolt>();

    // 四套特效组，用 CanvasGroup 做整组交叉淡化
    readonly CanvasGroup[] groups = new CanvasGroup[4];
    readonly float[] groupAlpha = new float[4];
    int targetGroup;

    // 双层程序化背景：A 是当前模式，B 在切换时淡入
    RawImage plasmaRawA;
    RawImage plasmaRawB;
    Texture2D plasmaTexA;
    Texture2D plasmaTexB;
    Color32[] plasmaBufA;
    Color32[] plasmaBufB;
    PlasmaMode plasmaModeA;
    PlasmaMode plasmaModeB;
    float plasmaBlend = 1f;
    float plasmaTimer;
    float plasmaPhase;
    float stormFlash;

    float elapsed;
    float screenWidth = 1920f;
    float screenHeight = 1080f;
    float designScale = 1f;
    Vector2 parallax;

    UITheme theme;
    MoodPalette currentMood = new MoodPalette();
    MoodPalette targetMood = new MoodPalette();
    int moodIndex;
    float targetSeverity = 1f;   // 难度强度：越大雨越密、雷越频繁、闪光越强
    float currentSeverity = 1f;
    float transitionPulse;
    float transitionPhase = 2f;
    float pulseTime;
    Image transitionRing;
    RectTransform transitionRingRt;

    static Sprite vignetteSprite;
    static Sprite runeSprite;
    static Sprite shockwaveSprite;
    static Sprite streakSprite;
    static Sprite rainSprite;

    // 供二级窗口（如难度选择）临时改变背景色调
    public static TitleBackdrop Instance { get; private set; }

    bool difficultyTintActive;

    void Awake()
    {
        Instance = this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        vignetteSprite = null;
        runeSprite = null;
        shockwaveSprite = null;
        streakSprite = null;
        rainSprite = null;
        Instance = null;
    }

    // ---------------- 对外入口 ----------------

    public void Build(UITheme theme, float width, float height)
    {
        if (theme == null) return;

        this.theme = theme;
        currentMood = MoodFor(0);
        targetMood = MoodFor(0);
        moodIndex = 0;
        targetGroup = 0;

        screenWidth = width > 1f ? width : 1920f;
        screenHeight = height > 1f ? height : 1080f;
        designScale = screenHeight / 1080f;
        float halfW = screenWidth * 0.5f;
        float halfH = screenHeight * 0.5f;
        float k = designScale;

        CreatePlasmaLayers();
        AddSharedGlows(k);
        AddSharedSmoke(k);
        AddRisingEmbers(halfW, halfH, k);

        // ---- 四套独立特效组 ----
        var groupStart = CreateGroup("Effect_Start");
        AddRuneRings(groupStart.transform, screenHeight * 0.19f, k);
        AddVortexEmbers(groupStart.transform, screenHeight * 0.13f, k);
        AddPulseRings(groupStart.transform, screenHeight * 0.17f, k);
        AddComets(groupStart.transform, halfW, halfH, k);

        var groupShop = CreateGroup("Effect_Shop");
        AddCoins(groupShop.transform, halfW, halfH, k);

        var groupHero = CreateGroup("Effect_Hero");
        AddAurora(groupHero.transform, halfW, halfH, k);
        AddSoulOrbs(groupHero.transform, screenHeight * 0.19f, k);

        var groupStorm = CreateGroup("Effect_Storm");
        AddRain(groupStorm.transform, halfW, halfH, k);
        AddLightning(groupStorm.transform, halfW, k);

        groupAlpha[0] = 1f;
        groupStart.alpha = 1f;
        for (int i = 1; i < groups.Length; i++)
        {
            groupAlpha[i] = 0f;
            if (groups[i] != null) groups[i].alpha = 0f;
        }

        AddTransitionRing(k);
        AddVignette();
    }

    // 由标题菜单调用：整套视觉系统切换（背景模式 + 特效组 + 配色）
    public void SetMood(int index)
    {
        index = Mathf.Clamp(index, 0, 3);
        if (index == moodIndex) return;

        moodIndex = index;
        targetMood = MoodFor(index);
        targetGroup = index;
        difficultyTintActive = false;

        StartPlasmaTransition(ModeFor(index));

        transitionPulse = 1f;
        transitionPhase = 0f;
        if (transitionRing != null)
        {
            var c = RoleColor(Role.Primary);
            c.a = 0.5f;
            transitionRing.color = c;
        }
    }

    // 难度窗口打开时用：只改背景色调，不改变当前“风暴”特效组。
    // 不同难度传入不同的主/副/强调色，切换时和其他情绪一样平滑插值。
    public void SetDifficultyTint(Color primary, Color secondary, Color accent, float severity)
    {
        difficultyTintActive = true;
        targetMood.primary = primary;
        targetMood.secondary = secondary;
        targetMood.accent = accent;
        targetSeverity = Mathf.Clamp01(severity);
        transitionPulse = Mathf.Max(transitionPulse, 0.45f);
    }

    // 难度窗口关闭时恢复标题菜单当前选项的配色
    public void ClearDifficultyTint()
    {
        if (!difficultyTintActive) return;
        difficultyTintActive = false;
        targetMood = MoodFor(moodIndex);
        targetSeverity = 1f;
    }

    static PlasmaMode ModeFor(int index)
    {
        switch (index)
        {
            case 1: return PlasmaMode.GoldDust;
            case 2: return PlasmaMode.Aurora;
            case 3: return PlasmaMode.Storm;
            default: return PlasmaMode.Nebula;
        }
    }

    MoodPalette MoodFor(int index)
    {
        var m = new MoodPalette();
        if (theme == null) return m;

        switch (index)
        {
            case 1: // 永久商店：金色 + 少量青色，节奏放慢
                m.primary = theme.gold;
                m.secondary = theme.teal;
                m.accent = theme.goldHi;
                m.cloudWeight = 0.85f;
                m.ribbonWeight = 1.2f;
                m.rimWeight = 0.85f;
                m.flow = 0.8f;
                m.rune = 0.75f;
                m.vortex = 0.85f;
                m.pulseRate = 0.85f;
                break;
            case 2: // 选择角色：青蓝主调
                m.primary = theme.tealHi;
                m.secondary = theme.teal;
                m.accent = theme.gold;
                m.cloudWeight = 1.3f;
                m.ribbonWeight = 0.85f;
                m.rimWeight = 0.9f;
                m.flow = 1.05f;
                m.rune = 1.3f;
                m.vortex = 1.1f;
                break;
            case 3: // 选择难度：血色主调，急
                m.primary = theme.bloodHi;
                m.secondary = theme.blood;
                m.accent = theme.goldHi;
                m.cloudWeight = 1.05f;
                m.ribbonWeight = 1.3f;
                m.rimWeight = 1.45f;
                m.emberScale = 1.15f;
                m.pulseScale = 1.25f;
                m.cometScale = 1.25f;
                m.flow = 1.5f;
                m.rune = 1.65f;
                m.vortex = 1.5f;
                m.pulseRate = 1.35f;
                break;
            default: // 开始游戏：金 + 青 + 血
                m.primary = theme.goldHi;
                m.secondary = theme.tealHi;
                m.accent = theme.bloodHi;
                break;
        }
        return m;
    }

    // ---------------- 每帧更新 ----------------

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = elapsed;
        float dt = Time.unscaledDeltaTime;

        UpdateParallax(dt);
        UpdateMood(dt);
        UpdateGroups(dt);
        UpdatePlasma(dt);

        stormFlash = Mathf.Max(0f, stormFlash - dt / 0.25f);

        UpdateSharedLayers(t, dt);

        if (groupAlpha[0] > 0.01f) UpdateStartGroup(t, dt);
        if (groupAlpha[1] > 0.01f) UpdateShopGroup(t, dt);
        if (groupAlpha[2] > 0.01f) UpdateHeroGroup(t, dt);
        if (groupAlpha[3] > 0.01f) UpdateStormGroup(t, dt);

        UpdateTransitionRing();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // 运行时创建的贴图跟随标题页销毁，避免重开场景时泄漏
        if (plasmaTexA != null) Destroy(plasmaTexA);
        if (plasmaTexB != null) Destroy(plasmaTexB);
    }

    void UpdateMood(float dt)
    {
        float k = 1f - Mathf.Exp(-3.2f * dt);
        currentMood.primary = Color.Lerp(currentMood.primary, targetMood.primary, k);
        currentMood.secondary = Color.Lerp(currentMood.secondary, targetMood.secondary, k);
        currentMood.accent = Color.Lerp(currentMood.accent, targetMood.accent, k);
        currentMood.cloudWeight = Mathf.Lerp(currentMood.cloudWeight, targetMood.cloudWeight, k);
        currentMood.ribbonWeight = Mathf.Lerp(currentMood.ribbonWeight, targetMood.ribbonWeight, k);
        currentMood.rimWeight = Mathf.Lerp(currentMood.rimWeight, targetMood.rimWeight, k);
        currentMood.emberScale = Mathf.Lerp(currentMood.emberScale, targetMood.emberScale, k);
        currentMood.pulseScale = Mathf.Lerp(currentMood.pulseScale, targetMood.pulseScale, k);
        currentMood.cometScale = Mathf.Lerp(currentMood.cometScale, targetMood.cometScale, k);
        currentMood.flow = Mathf.Lerp(currentMood.flow, targetMood.flow, k);
        currentMood.rune = Mathf.Lerp(currentMood.rune, targetMood.rune, k);
        currentMood.vortex = Mathf.Lerp(currentMood.vortex, targetMood.vortex, k);
        currentMood.pulseRate = Mathf.Lerp(currentMood.pulseRate, targetMood.pulseRate, k);
        currentSeverity = Mathf.Lerp(currentSeverity, targetSeverity, k);

        transitionPulse = Mathf.Max(0f, transitionPulse - dt / 0.8f);
        plasmaPhase += dt * currentMood.flow;
        pulseTime += dt * currentMood.pulseRate;
        if (transitionPhase < 1.5f) transitionPhase += dt / 0.85f;
    }

    void UpdateGroups(float dt)
    {
        float k = 1f - Mathf.Exp(-5f * dt);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null) continue;
            float target = i == targetGroup ? 1f : 0f;
            groupAlpha[i] = Mathf.Lerp(groupAlpha[i], target, k);
            groups[i].alpha = groupAlpha[i];
        }
    }

    Color RoleColor(Role role)
    {
        switch (role)
        {
            case Role.Secondary: return currentMood.secondary;
            case Role.Accent: return currentMood.accent;
            default: return currentMood.primary;
        }
    }

    // 鼠标位置带来的轻微视差
    void UpdateParallax(float dt)
    {
        Vector2 target = Vector2.zero;
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 p = mouse.position.ReadValue();
            target.x = Mathf.Clamp(p.x / Mathf.Max(1f, screenWidth) - 0.5f, -0.5f, 0.5f);
            target.y = Mathf.Clamp(p.y / Mathf.Max(1f, screenHeight) - 0.5f, -0.5f, 0.5f);
            target = -target;
        }
        parallax = Vector2.Lerp(parallax, target, 1f - Mathf.Exp(-3.5f * dt));
    }

    void UpdateSharedLayers(float t, float dt)
    {
        float boost = 1f + transitionPulse * 0.45f;

        for (int i = 0; i < drifters.Count; i++)
        {
            var d = drifters[i];
            if (d.rt == null || d.image == null) continue;

            float a = t / d.period * Mathf.PI * 2f + d.phase;
            d.rt.anchoredPosition = d.basePosition +
                new Vector2(Mathf.Sin(a) * d.amplitude.x, Mathf.Cos(a * 0.83f + d.phase) * d.amplitude.y) +
                parallax * 26f;

            var c = RoleColor(d.role);
            c.a = d.baseAlpha * (1f - d.alphaSwing * 0.5f + d.alphaSwing * 0.5f * Mathf.Sin(a * 0.7f + 1.4f)) * boost;
            d.image.color = c;

            if (Mathf.Abs(d.spin) > 0.01f)
                d.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(a * 0.35f) * d.spin);
        }

        for (int i = 0; i < embers.Count; i++)
        {
            var e = embers[i];
            if (e.rt == null || e.image == null) continue;

            e.y += e.speed * (0.7f + 0.4f * currentMood.flow) * dt;
            if (e.y > e.halfHeight + 40f)
            {
                e.y = -e.halfHeight - 40f;
                e.baseX = Random.Range(-e.halfWidth + 20f, e.halfWidth - 20f);
            }

            float swayPhase = t * e.swaySpeed + e.phase;
            e.rt.anchoredPosition = new Vector2(
                e.baseX + Mathf.Sin(swayPhase) * e.sway + parallax.x * 16f,
                e.y + parallax.y * 12f);

            float twinkle = 0.55f + 0.45f * Mathf.Sin(t * (0.9f + e.swaySpeed) + e.phase * 2.3f);
            var c = RoleColor(e.role);
            c.a = e.baseAlpha * twinkle * currentMood.emberScale;
            e.image.color = c;
        }
    }

    // ---------------- 选项 0：星云法阵 ----------------

    void UpdateStartGroup(float t, float dt)
    {
        for (int i = 0; i < runeRings.Count; i++)
        {
            var r = runeRings[i];
            if (r.rt == null || r.image == null) continue;

            r.angle += r.speed * (currentMood.rune + transitionPulse * 1.5f) * dt;
            r.rt.localRotation = Quaternion.Euler(0f, 0f, r.angle);
            r.rt.anchoredPosition = r.basePosition + parallax * 34f;

            var c = RoleColor(r.role);
            c.a = r.baseAlpha * (0.62f + 0.38f * Mathf.Sin(t * r.pulseSpeed + i * 1.7f)) *
                  (1f + transitionPulse * 0.45f);
            r.image.color = c;
        }

        for (int i = 0; i < vortexEmbers.Count; i++)
        {
            var v = vortexEmbers[i];
            if (v.rt == null || v.image == null) continue;

            v.angle += v.speed * (currentMood.vortex + transitionPulse * 0.8f) * dt;
            float radius = v.radius + Mathf.Sin(t * v.wobbleSpeed + v.phase) * v.wobbleAmp;
            float x = Mathf.Cos(v.angle) * radius + parallax.x * 46f;
            float y = v.centerY + Mathf.Sin(v.angle) * radius * 0.55f + parallax.y * 32f;
            v.rt.anchoredPosition = new Vector2(x, y);

            float depth = 0.5f + 0.5f * Mathf.Sin(v.angle);
            v.rt.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.15f, depth);

            var c = RoleColor(v.role);
            c.a = v.baseAlpha * Mathf.Lerp(0.35f, 1f, depth) *
                  (0.75f + 0.25f * Mathf.Sin(t * 1.6f + v.phase * 2f)) *
                  (1f + transitionPulse * 0.3f);
            v.image.color = c;
        }

        for (int i = 0; i < pulseRings.Count; i++)
        {
            var p = pulseRings[i];
            if (p.rt == null || p.image == null) continue;

            float phase = Mathf.Repeat((pulseTime + p.offset) / p.period, 1f);
            p.rt.sizeDelta = Vector2.one * p.baseSize * (0.35f + phase * 2.35f);
            p.rt.anchoredPosition = p.basePosition + parallax * 22f;

            var c = RoleColor(p.role);
            c.a = p.baseAlpha * (1f - phase) * (1f - phase) * currentMood.pulseScale;
            p.image.color = c;
        }

        for (int i = 0; i < comets.Count; i++)
        {
            var c = comets[i];
            if (c.rt == null || c.image == null) continue;

            float phase = Mathf.Repeat((pulseTime + c.offset) / c.period, 1f);
            const float travel = 0.15f;
            if (phase < travel)
            {
                float q = phase / travel;
                float ease = q * q * (3f - 2f * q);
                c.rt.anchoredPosition = Vector2.Lerp(c.start, c.end, ease) + parallax * 38f;
                var tint = RoleColor(c.role);
                tint.a = c.baseAlpha * currentMood.cometScale * Mathf.Sin(q * Mathf.PI);
                c.image.color = tint;
            }
            else
            {
                var tint = RoleColor(c.role);
                tint.a = 0f;
                c.image.color = tint;
            }
        }
    }

    // ---------------- 选项 1：黄金宝库 ----------------

    void UpdateShopGroup(float t, float dt)
    {
        for (int i = 0; i < coins.Count; i++)
        {
            var c = coins[i];
            if (c.rt == null || c.image == null) continue;

            c.y -= c.speed * dt;
            if (c.y < -c.halfH - 50f)
            {
                c.y = c.halfH + 50f;
                c.baseX = Random.Range(-c.halfW + 40f, c.halfW - 40f);
            }

            float x = c.baseX + Mathf.Sin(t * c.swaySpeed + c.phase) * c.sway + parallax.x * 18f;
            c.rt.anchoredPosition = new Vector2(x, c.y + parallax.y * 12f);

            // 用横向缩放模拟金币翻转
            c.flip += c.flipSpeed * dt;
            float sx = Mathf.Max(0.10f, Mathf.Abs(Mathf.Cos(c.flip)));
            c.rt.localScale = new Vector3(sx, 1f, 1f);

            var tint = RoleColor(c.role);
            tint.a = c.baseAlpha * (0.75f + 0.25f * Mathf.Sin(t * 2.1f + c.phase));
            c.image.color = tint;
        }
    }

    // ---------------- 选项 2：灵魂之流 ----------------

    void UpdateHeroGroup(float t, float dt)
    {
        for (int i = 0; i < ribbons.Count; i++)
        {
            var r = ribbons[i];
            if (r.rt == null || r.image == null) continue;

            float sway = Mathf.Sin(t * r.swaySpeed + r.phase);
            float rise = Mathf.Sin(t * r.riseSpeed * Mathf.PI * 2f + r.phase);
            r.rt.anchoredPosition = new Vector2(
                r.basePosition.x + sway * r.swayAmp + parallax.x * 30f,
                r.basePosition.y + rise * 180f * designScale + parallax.y * 22f);
            r.rt.localRotation = Quaternion.Euler(0f, 0f, sway * 4f);

            var c = RoleColor(r.role);
            c.a = r.baseAlpha * (0.55f + 0.45f * Mathf.Sin(t * 0.8f + r.phase));
            r.image.color = c;
        }

        for (int i = 0; i < soulOrbs.Count; i++)
        {
            var o = soulOrbs[i];
            if (o.rt == null || o.image == null) continue;

            o.angle += o.speed * dt;
            float radius = o.radius + Mathf.Sin(t * 0.5f + o.phase) * 28f * designScale;
            float x = Mathf.Cos(o.angle) * radius + parallax.x * 40f;
            float y = o.centerY + Mathf.Sin(o.angle) * radius * 0.55f + parallax.y * 28f;
            o.rt.anchoredPosition = new Vector2(x, y);

            float depth = 0.5f + 0.5f * Mathf.Sin(o.angle);
            o.rt.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.2f, depth);

            var c = RoleColor(o.role);
            c.a = o.baseAlpha * Mathf.Lerp(0.4f, 1f, depth) *
                  (0.7f + 0.3f * Mathf.Sin(t * 1.3f + o.phase * 2f));
            o.image.color = c;
        }
    }

    // ---------------- 选项 3：血月风暴 ----------------

    void UpdateStormGroup(float t, float dt)
    {
        // 难度越高：雨滴越快、可见数量越多；雷更频繁，闪电更亮
        for (int i = 0; i < rainDrops.Count; i++)
        {
            var r = rainDrops[i];
            if (r.rt == null || r.image == null) continue;

            float speed = r.speed * Mathf.Lerp(0.60f, 1.35f, currentSeverity);
            r.y -= speed * dt;

            if (r.y < -r.halfH - 80f)
            {
                r.y = r.halfH + Random.Range(0f, 180f);
                r.x = Random.Range(-r.halfW, r.halfW);
            }

            // 近乎垂直下落，只留一点点空气摆动
            float x = r.x + Mathf.Sin(t * 0.5f + r.phase) * 1.5f;
            r.rt.anchoredPosition = new Vector2(x, r.y);

            // 每条雨滴有独立阈值：难度越高，越多雨滴越过可见线，密度肉眼可见地增加
            float visible = Mathf.Clamp01((currentSeverity - r.threshold) * 3.5f);
            var c = RoleColor(r.role);
            c.a = r.baseAlpha * visible * (0.8f + 0.2f * Mathf.Sin(t * 3.2f + r.phase));
            r.image.color = c;
        }

        for (int i = 0; i < bolts.Count; i++)
        {
            var b = bolts[i];
            if (b.root == null) continue;

            b.timer -= dt;
            if (b.timer <= 0f && b.flash <= 0f)
            {
                TriggerBolt(b, t);
                b.timer = Random.Range(1.1f, 3.2f) * Mathf.Lerp(1.8f, 0.45f, currentSeverity);
                stormFlash = 1f;
            }

            if (b.flash > 0f)
            {
                b.flash = Mathf.Max(0f, b.flash - dt / 0.22f);
                float flicker = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 55f + b.index * 2.1f));
                SetBoltAlpha(b, b.flash * flicker * Mathf.Lerp(0.75f, 1f, currentSeverity));
            }
            else
            {
                SetBoltAlpha(b, 0f);
            }
        }
    }

    void TriggerBolt(LightningBolt bolt, float t)
    {
        bolt.flash = 1f;

        float k = designScale;
        float width = Random.Range(90f, 220f) * k;
        float height = Random.Range(420f, 760f) * k;
        float x = Random.Range(-screenWidth * 0.40f, screenWidth * 0.40f);

        bolt.root.anchoredPosition = new Vector2(x, screenHeight * 0.16f);
        bolt.root.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-7f, 7f));
        bolt.root.localScale = new Vector3(Random.value < 0.5f ? -1f : 1f,
            Random.Range(0.8f, 1.25f), 1f);

        int n = bolt.segments.Length;
        float px = Random.Range(-width * 0.3f, width * 0.3f);
        float prevX = px;
        float prevY = height * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float py = height * 0.5f - height * (i + 1) / n;
            px += Random.Range(-width * 0.22f, width * 0.22f);
            px = Mathf.Clamp(px, -width, width);

            var seg = bolt.segments[i];
            Vector2 a = new Vector2(prevX, prevY);
            Vector2 b = new Vector2(px, py);
            Vector2 d = b - a;

            seg.rectTransform.anchoredPosition = (a + b) * 0.5f;
            seg.rectTransform.sizeDelta = new Vector2(d.magnitude, bolt.width * k);
            seg.rectTransform.localRotation = Quaternion.Euler(0f,
                0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);

            prevX = px;
            prevY = py;
        }
    }

    void SetBoltAlpha(LightningBolt bolt, float alpha)
    {
        for (int i = 0; i < bolt.segments.Length; i++)
        {
            var img = bolt.segments[i];
            if (img == null) continue;
            var c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }

    // ---------------- 程序化背景（四种模式） ----------------

    void CreatePlasmaLayers()
    {
        plasmaTexA = CreatePlasmaTexture("TitlePlasmaA");
        plasmaTexB = CreatePlasmaTexture("TitlePlasmaB");
        plasmaBufA = new Color32[PlasmaW * PlasmaH];
        plasmaBufB = new Color32[PlasmaW * PlasmaH];
        plasmaModeA = PlasmaMode.Nebula;
        plasmaModeB = PlasmaMode.Nebula;
        plasmaBlend = 1f;

        plasmaRawA = CreatePlasmaImage("PlasmaA", plasmaTexA, 1f);
        plasmaRawB = CreatePlasmaImage("PlasmaB", plasmaTexB, 0f);

        FillPlasma(plasmaBufA, plasmaModeA, 0f);
        plasmaTexA.SetPixels32(plasmaBufA);
        plasmaTexA.Apply(false);
    }

    Texture2D CreatePlasmaTexture(string name)
    {
        return new Texture2D(PlasmaW, PlasmaH, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
    }

    RawImage CreatePlasmaImage(string name, Texture2D tex, float alpha)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-60f, -60f);
        rt.offsetMax = new Vector2(60f, 60f);

        var raw = go.GetComponent<RawImage>();
        raw.texture = tex;
        raw.raycastTarget = false;
        raw.color = new Color(1f, 1f, 1f, alpha);
        return raw;
    }

    void StartPlasmaTransition(PlasmaMode mode)
    {
        if (mode == plasmaModeA && plasmaBlend >= 1f) return;

        // 若上一次过渡未完成，先立即完成它，避免颜色/纹理跳变
        if (plasmaBlend < 1f) CommitPlasmaB();

        plasmaModeB = mode;
        plasmaBlend = 0f;
        plasmaRawB.color = new Color(1f, 1f, 1f, 0f);

        FillPlasma(plasmaBufB, plasmaModeB, plasmaPhase);
        plasmaTexB.SetPixels32(plasmaBufB);
        plasmaTexB.Apply(false);
    }

    void CommitPlasmaB()
    {
        var tmpTex = plasmaTexA;
        plasmaTexA = plasmaTexB;
        plasmaTexB = tmpTex;

        var tmpBuf = plasmaBufA;
        plasmaBufA = plasmaBufB;
        plasmaBufB = tmpBuf;

        plasmaModeA = plasmaModeB;
        plasmaRawA.texture = plasmaTexA;
        plasmaRawB.texture = plasmaTexB;
        plasmaRawB.color = new Color(1f, 1f, 1f, 0f);
        plasmaBlend = 1f;
    }

    void UpdatePlasma(float dt)
    {
        plasmaTimer -= dt;
        if (plasmaTimer <= 0f)
        {
            plasmaTimer = 1f / PlasmaFps;

            FillPlasma(plasmaBufA, plasmaModeA, plasmaPhase);
            plasmaTexA.SetPixels32(plasmaBufA);
            plasmaTexA.Apply(false);

            if (plasmaBlend < 1f)
            {
                FillPlasma(plasmaBufB, plasmaModeB, plasmaPhase);
                plasmaTexB.SetPixels32(plasmaBufB);
                plasmaTexB.Apply(false);
            }
        }

        if (plasmaBlend < 1f)
        {
            plasmaBlend = Mathf.Min(1f, plasmaBlend + dt / 0.9f);
            float a = Mathf.SmoothStep(0f, 1f, plasmaBlend);
            plasmaRawB.color = new Color(1f, 1f, 1f, a);
            if (plasmaBlend >= 1f) CommitPlasmaB();
        }
    }

    void FillPlasma(Color32[] buffer, PlasmaMode mode, float t)
    {
        switch (mode)
        {
            case PlasmaMode.GoldDust: FillGoldDust(buffer, t); break;
            case PlasmaMode.Aurora: FillAurora(buffer, t); break;
            case PlasmaMode.Storm: FillStorm(buffer, t); break;
            default: FillNebula(buffer, t); break;
        }
    }

    void FillNebula(Color32[] buffer, float t)
    {
        int i = 0;
        for (int y = 0; y < PlasmaH; y++)
        {
            float v = (y + 0.5f) / PlasmaH * 2f - 1f;
            for (int x = 0; x < PlasmaW; x++)
            {
                float u = (x + 0.5f) / PlasmaW * 2f - 1f;
                float rad = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u);

                float warp = Mathf.Sin(v * 2.4f + t * 0.09f) * 1.4f
                           + Mathf.Cos(u * 1.8f - t * 0.07f) * 0.9f;
                float f1 = Mathf.Sin(u * 2.6f - t * 0.16f + warp);
                float f2 = Mathf.Cos(v * 2.2f + t * 0.13f + warp * 0.7f);
                float cloud = Mathf.Clamp01(0.5f + 0.25f * (f1 + f2));
                cloud *= cloud;

                float ribbon = 0.5f + 0.5f * Mathf.Sin(ang * 2f + rad * 9f - t * 0.45f
                                                      + Mathf.Sin(rad * 5f - t * 0.3f));
                ribbon *= ribbon;
                ribbon *= ribbon;

                float edge = Mathf.Clamp01(1f - Mathf.Pow(rad / 1.18f, 2f));
                float rim = Mathf.Clamp01((rad - 0.6f) / 0.55f);
                float waveBoost = 1f + transitionPulse * 0.7f;

                float cloudTerm = cloud * edge * 0.32f * currentMood.cloudWeight;
                float ribbonTerm = ribbon * edge * 0.36f * currentMood.ribbonWeight * waveBoost;
                float rimTerm = rim * rim * 0.26f * currentMood.rimWeight;

                float du = u;
                float dv = v - 0.34f;
                float core = Mathf.Exp(-(du * du + dv * dv) * 5.5f) * (0.7f + 0.3f * Mathf.Sin(t * 0.6f));
                float coreTerm = core * (0.14f + transitionPulse * 0.1f);

                buffer[i++] = ToColor(
                    0.012f + currentMood.secondary.r * cloudTerm + currentMood.primary.r * ribbonTerm
                           + currentMood.accent.r * rimTerm + currentMood.primary.r * coreTerm,
                    0.017f + currentMood.secondary.g * cloudTerm + currentMood.primary.g * ribbonTerm
                           + currentMood.accent.g * rimTerm + currentMood.primary.g * coreTerm,
                    0.032f + currentMood.secondary.b * cloudTerm + currentMood.primary.b * ribbonTerm
                           + currentMood.accent.b * rimTerm + currentMood.primary.b * coreTerm);
            }
        }
    }

    // 金色光柱 + 下落金币尘埃的底子
    void FillGoldDust(Color32[] buffer, float t)
    {
        int i = 0;
        for (int y = 0; y < PlasmaH; y++)
        {
            float v = (y + 0.5f) / PlasmaH * 2f - 1f;
            for (int x = 0; x < PlasmaW; x++)
            {
                float u = (x + 0.5f) / PlasmaW * 2f - 1f;
                float rad = Mathf.Sqrt(u * u + v * v);

                float shaftWave = Mathf.Sin(u * 7.5f + Mathf.Sin(v * 1.6f + t * 0.16f) * 1.3f + t * 0.1f);
                float shaft = Mathf.Pow(Mathf.Clamp01(shaftWave), 14f);

                float dust = Mathf.Sin(u * 31f + v * 19f + t * 2.1f)
                           * Mathf.Cos(u * 15f - v * 27f - t * 1.6f);
                dust = Mathf.Pow(Mathf.Clamp01(dust), 16f);

                float haze = 0.5f + 0.5f * Mathf.Sin(u * 2.1f + t * 0.08f
                            + Mathf.Sin(v * 1.7f - t * 0.1f) * 1.2f);
                haze *= haze;

                float edge = Mathf.Clamp01(1f - rad * 0.55f);
                float core = Mathf.Exp(-(u * u + (v - 0.28f) * (v - 0.28f)) * 4.5f);
                float boost = 1f + transitionPulse * 0.7f;

                buffer[i++] = ToColor(
                    0.020f + currentMood.primary.r * (shaft * 0.34f + core * 0.12f) * edge
                           + currentMood.accent.r * dust * 0.55f * edge
                           + currentMood.secondary.r * haze * 0.05f * edge * boost,
                    0.014f + currentMood.primary.g * (shaft * 0.34f + core * 0.12f) * edge
                           + currentMood.accent.g * dust * 0.55f * edge
                           + currentMood.secondary.g * haze * 0.05f * edge * boost,
                    0.008f + currentMood.primary.b * (shaft * 0.34f + core * 0.12f) * edge
                           + currentMood.accent.b * dust * 0.55f * edge
                           + currentMood.secondary.b * haze * 0.05f * edge * boost);
            }
        }
    }

    // 青蓝极光帘幕
    void FillAurora(Color32[] buffer, float t)
    {
        int i = 0;
        for (int y = 0; y < PlasmaH; y++)
        {
            float v = (y + 0.5f) / PlasmaH * 2f - 1f;
            for (int x = 0; x < PlasmaW; x++)
            {
                float u = (x + 0.5f) / PlasmaW * 2f - 1f;
                float rad = Mathf.Sqrt(u * u + v * v);

                float c1 = 0.5f + 0.5f * Mathf.Sin(u * 3.4f
                          + Mathf.Sin(v * 1.3f - t * 0.22f) * 2.0f + t * 0.1f);
                float c2 = 0.5f + 0.5f * Mathf.Sin(u * 5.1f
                          - Mathf.Sin(v * 1.9f + t * 0.16f) * 1.5f - t * 0.07f);
                float fall = Mathf.Clamp01(0.85f - v * 0.45f);
                float a1 = Mathf.Pow(c1, 3.5f) * fall;
                float a2 = Mathf.Pow(c2, 4.5f) * fall;
                float filament = Mathf.Pow(Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(
                    u * 12f + Mathf.Sin(v * 2.2f + t * 0.3f) * 2f + t * 0.2f)), 24f) * fall;

                float edge = Mathf.Clamp01(1f - rad * 0.35f);
                float boost = 1f + transitionPulse * 0.6f;

                buffer[i++] = ToColor(
                    0.006f + currentMood.primary.r * (a1 * 0.30f + filament * 0.38f) * edge * boost
                           + currentMood.secondary.r * a2 * 0.16f * edge
                           + currentMood.accent.r * 0.01f,
                    0.014f + currentMood.primary.g * (a1 * 0.30f + filament * 0.38f) * edge * boost
                           + currentMood.secondary.g * a2 * 0.16f * edge
                           + currentMood.accent.g * 0.012f,
                    0.030f + currentMood.primary.b * (a1 * 0.30f + filament * 0.38f) * edge * boost
                           + currentMood.secondary.b * a2 * 0.16f * edge
                           + currentMood.accent.b * 0.012f);
            }
        }
    }

    // 暗红雷云 + 闪电爆闪；去掉高频条纹，雨感交给前景的细雨丝
    void FillStorm(Color32[] buffer, float t)
    {
        float sev = currentSeverity;
        int i = 0;
        for (int y = 0; y < PlasmaH; y++)
        {
            float v = (y + 0.5f) / PlasmaH * 2f - 1f;
            for (int x = 0; x < PlasmaW; x++)
            {
                float u = (x + 0.5f) / PlasmaW * 2f - 1f;
                float rad = Mathf.Sqrt(u * u + v * v);

                // 两层低频云团，避免任何高频横条/斜条
                float n1 = Mathf.Sin(u * 2.6f + t * 0.18f
                          + Mathf.Sin(v * 1.7f - t * 0.25f) * 1.2f);
                float n2 = Mathf.Cos(v * 2.2f - t * 0.14f
                          + Mathf.Sin(u * 1.5f + t * 0.11f) * 1.1f);
                float cloud = Mathf.Clamp01(0.5f + 0.25f * (n1 + n2));
                cloud = Mathf.Pow(cloud, Mathf.Lerp(2.3f, 1.7f, sev));

                // 云底略亮、顶部更沉
                float shade = Mathf.Lerp(1.05f, 0.78f, (v + 1f) * 0.5f);

                float rim = Mathf.Clamp01((rad - 0.55f) / 0.6f);
                rim *= rim;

                float flash = stormFlash * (0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * 42f)))
                            * Mathf.Lerp(0.7f, 1.5f, sev);

                // 难度越高，底色越深，云层越重
                float baseR = Mathf.Lerp(0.034f, 0.018f, sev);
                float baseG = Mathf.Lerp(0.007f, 0.003f, sev);
                float baseB = Mathf.Lerp(0.014f, 0.007f, sev);
                float cloudWeight = (0.22f + sev * 0.10f) * shade;
                float rimWeight = 0.30f + sev * 0.20f;

                buffer[i++] = ToColor(
                    baseR + currentMood.primary.r * cloud * cloudWeight
                          + currentMood.primary.r * rim * rimWeight
                          + currentMood.secondary.r * 0.015f
                          + flash * 0.55f,
                    baseG + currentMood.primary.g * cloud * cloudWeight
                          + currentMood.primary.g * rim * rimWeight
                          + currentMood.secondary.g * 0.015f
                          + flash * 0.45f,
                    baseB + currentMood.primary.b * cloud * cloudWeight
                          + currentMood.primary.b * rim * rimWeight
                          + currentMood.secondary.b * 0.015f
                          + flash * 0.35f);
            }
        }
    }

    static Color32 ToColor(float r, float g, float b)
    {
        return new Color32(
            (byte)(Mathf.Clamp01(r) * 255f),
            (byte)(Mathf.Clamp01(g) * 255f),
            (byte)(Mathf.Clamp01(b) * 255f),
            255);
    }

    // ---------------- 共享层 ----------------

    void AddSharedGlows(float k)
    {
        AddDrifter("GlowPrimary", "glow_soft", Role.Primary, 0.16f,
            new Vector2(-screenWidth * 0.22f, screenHeight * 0.14f),
            new Vector2(1300f, 1300f) * k,
            new Vector2(70f, 46f) * k, 34f, 0f, 0f, 0.22f);

        AddDrifter("GlowSecondary", "glow_soft", Role.Secondary, 0.12f,
            new Vector2(screenWidth * 0.25f, -screenHeight * 0.04f),
            new Vector2(1100f, 1100f) * k,
            new Vector2(58f, 40f) * k, 41f, 2.1f, 0f, 0.18f);

        AddDrifter("GlowAccent", "glow_soft", Role.Accent, 0.10f,
            new Vector2(0f, -screenHeight * 0.34f),
            new Vector2(1500f, 900f) * k,
            new Vector2(46f, 28f) * k, 47f, 4.2f, 0f, 0.25f);
    }

    void AddSharedSmoke(float k)
    {
        AddDrifter("SmokeWide", "smoke_wide", Role.Secondary, 0.08f,
            new Vector2(0f, screenHeight * 0.16f),
            new Vector2(2050f, 1150f) * k,
            new Vector2(screenWidth * 0.10f, 26f * k), 58f, 1.2f, 4f, 0.32f);

        AddDrifter("SmokeWisp", "smoke_wisp", Role.Primary, 0.055f,
            new Vector2(-screenWidth * 0.08f, -screenHeight * 0.18f),
            new Vector2(1650f, 1050f) * k,
            new Vector2(screenWidth * 0.07f, 34f * k), 44f, 3.6f, -6f, 0.26f);
    }

    void AddDrifter(string name, string artName, Role role, float alpha, Vector2 position, Vector2 size,
        Vector2 amplitude, float period, float phase, float spin, float alphaSwing)
    {
        var sprite = Resources.Load<Sprite>(ArtDir + artName);
        if (sprite == null) return;

        Color initial = RoleColor(role);
        initial.a = alpha;

        var image = UIFactory.CreateImage(transform, name, sprite, initial, false, false);
        image.preserveAspect = false;

        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;

        drifters.Add(new Drifter
        {
            rt = rt,
            image = image,
            basePosition = position,
            amplitude = amplitude,
            period = period,
            phase = phase,
            baseAlpha = alpha,
            alphaSwing = alphaSwing,
            spin = spin,
            role = role,
        });
    }

    void AddRisingEmbers(float halfW, float halfH, float k)
    {
        var root = new GameObject("SharedEmbers", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;

        var sprite = Resources.Load<Sprite>(ArtDir + "ember_star");
        if (sprite == null) return;

        int count = Mathf.RoundToInt(26f * Mathf.Clamp(k, 0.8f, 1.3f));
        for (int i = 0; i < count; i++)
        {
            Role role;
            float alpha;
            switch (i % 4)
            {
                case 0: role = Role.Primary; alpha = 0.75f; break;
                case 1: role = Role.Secondary; alpha = 0.6f; break;
                case 2: role = Role.Accent; alpha = 0.65f; break;
                default: role = Role.Primary; alpha = 0.6f; break;
            }

            Color initial = RoleColor(role);
            initial.a = alpha;

            var image = UIFactory.CreateImage(rt, "Ember_" + i, sprite, initial, false, false);
            image.preserveAspect = true;

            float size = Random.Range(8f, 24f) * k;
            var irt = image.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(size, size);

            float x = Random.Range(-halfW + 20f, halfW - 20f);
            float y = Random.Range(-halfH - 40f, halfH);
            irt.anchoredPosition = new Vector2(x, y);

            embers.Add(new Ember
            {
                rt = irt,
                image = image,
                baseX = x,
                y = y,
                speed = Random.Range(18f, 55f) * k,
                sway = Random.Range(8f, 30f) * k,
                swaySpeed = Random.Range(0.25f, 0.9f),
                phase = Random.Range(0f, Mathf.PI * 2f),
                baseAlpha = alpha,
                halfHeight = halfH,
                halfWidth = halfW,
                role = role,
            });
        }
    }

    CanvasGroup CreateGroup(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var cg = go.GetComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null)
            {
                groups[i] = cg;
                break;
            }
        }
        return cg;
    }

    // ---------------- 选项 0 的构建 ----------------

    void AddRuneRings(Transform parent, float centerY, float k)
    {
        if (runeSprite == null) runeSprite = BuildRuneSprite();

        AddRune(parent, "RuneOuter", Role.Primary, 0.30f, centerY, 980f * k, 3.4f, 0.55f);
        AddRune(parent, "RuneInner", Role.Accent, 0.20f, centerY, 760f * k, -2.1f, 0.8f);
    }

    void AddRune(Transform parent, string name, Role role, float alpha, float centerY, float size,
        float speed, float pulseSpeed)
    {
        Color initial = RoleColor(role);
        initial.a = alpha;

        var image = UIFactory.CreateImage(parent, name, runeSprite, initial, false, false);
        image.preserveAspect = true;

        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        Vector2 basePosition = new Vector2(0f, centerY);
        rt.anchoredPosition = basePosition;

        runeRings.Add(new RuneRing
        {
            rt = rt,
            image = image,
            basePosition = basePosition,
            speed = speed,
            pulseSpeed = pulseSpeed,
            baseAlpha = alpha,
            role = role,
        });
    }

    void AddVortexEmbers(Transform parent, float centerY, float k)
    {
        var sprite = Resources.Load<Sprite>(ArtDir + "ember_star");
        if (sprite == null) return;

        int count = 18;
        for (int i = 0; i < count; i++)
        {
            Role role;
            float alpha;
            switch (i % 4)
            {
                case 1: role = Role.Accent; alpha = 0.62f; break;
                case 2: role = Role.Secondary; alpha = 0.60f; break;
                case 3: role = Role.Primary; alpha = 0.72f; break;
                default: role = Role.Primary; alpha = 0.80f; break;
            }

            Color initial = RoleColor(role);
            initial.a = alpha;

            var image = UIFactory.CreateImage(parent, "VortexEmber_" + i, sprite, initial, false, false);
            image.preserveAspect = true;

            float size = Random.Range(7f, 15f) * k;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            vortexEmbers.Add(new VortexEmber
            {
                rt = rt,
                image = image,
                angle = Random.Range(0f, Mathf.PI * 2f),
                speed = Random.Range(0.16f, 0.38f) * (i % 5 == 0 ? -1f : 1f),
                radius = Random.Range(180f, 460f) * k,
                centerY = centerY,
                wobbleSpeed = Random.Range(0.3f, 0.9f),
                wobbleAmp = Random.Range(8f, 30f) * k,
                phase = Random.Range(0f, Mathf.PI * 2f),
                baseAlpha = alpha,
                role = role,
            });
        }
    }

    void AddPulseRings(Transform parent, float centerY, float k)
    {
        if (shockwaveSprite == null) shockwaveSprite = BuildShockwaveSprite();

        AddPulse(parent, Role.Primary, 0.20f, centerY, 360f * k, 6.2f, 0f);
        AddPulse(parent, Role.Secondary, 0.15f, centerY, 360f * k, 6.2f, 3.1f);
        AddPulse(parent, Role.Accent, 0.14f, centerY, 360f * k, 9.4f, 1.4f);
    }

    void AddPulse(Transform parent, Role role, float alpha, float centerY, float baseSize,
        float period, float offset)
    {
        Color initial = RoleColor(role);
        initial.a = alpha;

        var image = UIFactory.CreateImage(parent, "Pulse", shockwaveSprite, initial, false, false);
        image.preserveAspect = true;

        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 basePosition = new Vector2(0f, centerY);
        rt.anchoredPosition = basePosition;
        rt.sizeDelta = new Vector2(baseSize, baseSize);

        pulseRings.Add(new PulseRing
        {
            rt = rt,
            image = image,
            basePosition = basePosition,
            offset = offset,
            period = period,
            baseSize = baseSize,
            baseAlpha = alpha,
            role = role,
        });
    }

    void AddComets(Transform parent, float halfW, float halfH, float k)
    {
        if (streakSprite == null) streakSprite = BuildStreakSprite();

        AddComet(parent, Role.Primary, 0.75f,
            new Vector2(-halfW * 1.25f, halfH * 0.78f),
            new Vector2(halfW * 1.25f, halfH * 0.05f), 9.5f, 0f, k);
        AddComet(parent, Role.Secondary, 0.6f,
            new Vector2(halfW * 1.25f, halfH * 0.62f),
            new Vector2(-halfW * 1.25f, -halfH * 0.02f), 13.5f, 6.2f, k);
    }

    void AddComet(Transform parent, Role role, float alpha, Vector2 start, Vector2 end,
        float period, float offset, float k)
    {
        Color initial = RoleColor(role);
        initial.a = 0f;

        var image = UIFactory.CreateImage(parent, "Comet", streakSprite, initial, false, false);
        image.preserveAspect = false;

        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(260f, 10f) * k;
        rt.anchoredPosition = start;

        Vector2 dir = end - start;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        comets.Add(new Comet
        {
            rt = rt,
            image = image,
            period = period,
            offset = offset,
            start = start,
            end = end,
            angle = angle,
            baseAlpha = alpha,
            role = role,
        });
    }

    // ---------------- 选项 1 的构建：金币雨 ----------------

    void AddCoins(Transform parent, float halfW, float halfH, float k)
    {
        var sprite = Resources.Load<Sprite>("Icons/ic_coin");
        if (sprite == null) return;

        int count = 16;
        for (int i = 0; i < count; i++)
        {
            Role role = i % 4 == 0 ? Role.Accent : Role.Primary;
            Color initial = Color.Lerp(RoleColor(role), Color.white, 0.35f);
            initial.a = Random.Range(0.5f, 0.85f);

            var image = UIFactory.CreateImage(parent, "Coin_" + i, sprite, initial, false, false);
            image.preserveAspect = true;

            float size = Random.Range(18f, 38f) * k;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            float x = Random.Range(-halfW + 40f, halfW - 40f);
            float y = Random.Range(-halfH, halfH);
            rt.anchoredPosition = new Vector2(x, y);

            coins.Add(new Coin
            {
                rt = rt,
                image = image,
                baseX = x,
                y = y,
                speed = Random.Range(18f, 50f) * k,
                sway = Random.Range(10f, 36f) * k,
                swaySpeed = Random.Range(0.3f, 0.8f),
                phase = Random.Range(0f, Mathf.PI * 2f),
                flip = Random.Range(0f, Mathf.PI * 2f),
                flipSpeed = Random.Range(1.4f, 3.2f),
                baseAlpha = initial.a,
                halfW = halfW,
                halfH = halfH,
                role = role,
            });
        }
    }

    // ---------------- 选项 2 的构建：极光帘幕 + 灵魂光球 ----------------

    void AddAurora(Transform parent, float halfW, float halfH, float k)
    {
        var sprite = Resources.Load<Sprite>(ArtDir + "smoke_wisp");
        if (sprite == null) return;

        for (int i = 0; i < 5; i++)
        {
            Role role = i % 3 == 0 ? Role.Accent : (i % 2 == 0 ? Role.Secondary : Role.Primary);
            Color initial = RoleColor(role);
            initial.a = Random.Range(0.05f, 0.10f);

            var image = UIFactory.CreateImage(parent, "Aurora_" + i, sprite, initial, false, false);
            image.preserveAspect = false;

            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Random.Range(700f, 1200f) * k, 2300f * k);
            Vector2 pos = new Vector2(Random.Range(-halfW * 0.8f, halfW * 0.8f),
                Random.Range(-halfH * 0.2f, halfH * 0.5f));
            rt.anchoredPosition = pos;

            ribbons.Add(new AuroraRibbon
            {
                rt = rt,
                image = image,
                basePosition = pos,
                swayAmp = Random.Range(60f, 160f) * k,
                swaySpeed = Random.Range(0.10f, 0.24f),
                riseSpeed = Random.Range(0.015f, 0.045f),
                phase = Random.Range(0f, Mathf.PI * 2f),
                baseAlpha = initial.a,
                role = role,
            });
        }
    }

    void AddSoulOrbs(Transform parent, float centerY, float k)
    {
        var sprite = Resources.Load<Sprite>(ArtDir + "glow_soft");
        if (sprite == null) return;

        for (int i = 0; i < 8; i++)
        {
            Role role = i % 3 == 0 ? Role.Accent : (i % 2 == 0 ? Role.Primary : Role.Secondary);
            Color initial = RoleColor(role);
            initial.a = Random.Range(0.25f, 0.5f);

            var image = UIFactory.CreateImage(parent, "SoulOrb_" + i, sprite, initial, false, false);
            image.preserveAspect = true;

            float size = Random.Range(40f, 110f) * k;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            soulOrbs.Add(new SoulOrb
            {
                rt = rt,
                image = image,
                angle = Random.Range(0f, Mathf.PI * 2f),
                speed = Random.Range(0.06f, 0.18f) * (i % 4 == 0 ? -1f : 1f),
                radius = Random.Range(220f, 540f) * k,
                centerY = centerY,
                phase = Random.Range(0f, Mathf.PI * 2f),
                baseAlpha = initial.a,
                role = role,
            });
        }
    }

    // ---------------- 选项 3 的构建：雨 + 闪电 ----------------

    void AddRain(Transform parent, float halfW, float halfH, float k)
    {
        if (rainSprite == null) rainSprite = BuildRainSprite();

        // 远近两层：远处更细更淡更慢，近处稍亮稍快，叠出细腻的雨幕
        int count = 160;
        for (int i = 0; i < count; i++)
        {
            bool near = i % 4 == 0;
            Role role = i % 11 == 0 ? Role.Accent : Role.Primary;
            Color initial = Color.Lerp(RoleColor(role), Color.white, 0.32f);
            initial.a = near ? Random.Range(0.48f, 0.82f) : Random.Range(0.24f, 0.46f);

            var image = UIFactory.CreateImage(parent, "Rain_" + i, rainSprite, initial, false, false);
            image.preserveAspect = false;

            float length = (near ? Random.Range(30f, 66f) : Random.Range(16f, 40f)) * k;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(
                (near ? Random.Range(2.2f, 3.6f) : Random.Range(1.4f, 2.4f)) * k, length);
            rt.localRotation = Quaternion.identity;

            float x = Random.Range(-halfW, halfW);
            float y = Random.Range(-halfH, halfH);
            rt.anchoredPosition = new Vector2(x, y);

            rainDrops.Add(new RainDrop
            {
                rt = rt,
                image = image,
                x = x,
                y = y,
                speed = (near ? Random.Range(1000f, 1600f) : Random.Range(600f, 1000f)) * k,
                length = length,
                baseAlpha = initial.a,
                threshold = Random.Range(0f, 0.55f),
                phase = Random.Range(0f, Mathf.PI * 2f),
                halfW = halfW,
                halfH = halfH,
                role = role,
            });
        }
    }

    void AddLightning(Transform parent, float halfW, float k)
    {
        for (int i = 0; i < 3; i++)
        {
            var root = new GameObject("Bolt_" + i, typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;

            var segments = new Image[10];
            for (int s = 0; s < segments.Length; s++)
            {
                var img = UIFactory.CreateImage(rt, "Seg_" + s, UIFactory.WhiteSprite,
                    new Color(1f, 1f, 1f, 0f), false, false);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                segments[s] = img;
            }

            bolts.Add(new LightningBolt
            {
                root = rt,
                segments = segments,
                timer = Random.Range(0.6f, 2.8f),
                flash = 0f,
                width = Random.Range(3.5f, 6f),
                index = i,
            });
        }
    }

    // ---------------- 过渡波与暗角 ----------------

    void AddTransitionRing(float k)
    {
        if (shockwaveSprite == null) shockwaveSprite = BuildShockwaveSprite();

        var image = UIFactory.CreateImage(transform, "MoodTransitionRing",
            shockwaveSprite, Color.clear, false, false);
        image.preserveAspect = true;

        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, screenHeight * 0.17f);
        rt.sizeDelta = new Vector2(400f, 400f) * k;

        transitionRing = image;
        transitionRingRt = rt;
    }

    void UpdateTransitionRing()
    {
        if (transitionRing == null || transitionRingRt == null) return;

        if (transitionPhase < 1f)
        {
            float p = Mathf.Clamp01(transitionPhase);
            transitionRingRt.sizeDelta = Vector2.one * (420f * designScale) * (0.35f + p * 2.8f);
            var c = RoleColor(Role.Primary);
            c.a = (1f - p) * (1f - p) * 0.5f;
            transitionRing.color = c;
        }
        else if (transitionRing.color.a > 0f)
        {
            var c = transitionRing.color;
            c.a = 0f;
            transitionRing.color = c;
        }
    }

    void AddVignette()
    {
        if (vignetteSprite == null) vignetteSprite = BuildVignetteSprite();

        var image = UIFactory.CreateImage(transform, "Vignette", vignetteSprite, Color.white, false, false);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;

        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-40f, -40f);
        rt.offsetMax = new Vector2(40f, 40f);
    }

    // ---------------- 运行时生成的贴图 ----------------

    static Sprite BuildRuneSprite()
    {
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            name = "TitleRuneRing",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            float v = (y + 0.5f) / n * 2f - 1f;
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u);
                float a = 0f;

                a = Mathf.Max(a, Band(r, 0.955f, 0.006f));
                a = Mathf.Max(a, Band(r, 0.875f, 0.004f));

                float ticks = Mathf.Repeat((ang / (Mathf.PI * 2f)) * 48f, 1f);
                if (r > 0.88f && r < 0.945f && ticks < 0.10f) a = 1f;

                float longTick = Mathf.Repeat((ang / (Mathf.PI * 2f)) * 6f, 1f);
                if (r > 0.855f && r < 0.955f && longTick < 0.035f) a = 1f;

                float dashes = Mathf.Repeat((ang / (Mathf.PI * 2f)) * 24f, 1f);
                if (r > 0.70f && r < 0.715f && dashes < 0.45f) a = 1f;

                pixels[y * n + x] = new Color32(255, 255, 255,
                    (byte)Mathf.Clamp(a * 255f, 0f, 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite BuildShockwaveSprite()
    {
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            name = "TitleShockwave",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            float v = (y + 0.5f) / n * 2f - 1f;
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Band(r, 0.86f, 0.018f);
                a *= a;
                pixels[y * n + x] = new Color32(255, 255, 255,
                    (byte)Mathf.Clamp(a * 255f, 0f, 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite BuildStreakSprite()
    {
        const int w = 128;
        const int h = 16;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "TitleComet",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float fy = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
            float falloff = Mathf.Clamp01(1f - fy);
            falloff *= falloff;
            for (int x = 0; x < w; x++)
            {
                float fx = (x + 0.5f) / w;
                float tail = Mathf.Pow(fx, 2.4f);
                float head = Mathf.Clamp01((fx - 0.82f) / 0.18f);
                float a = Mathf.Clamp01(tail * falloff + head * falloff);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    // 雨丝：窄芯 + 柔和边缘，落点略亮、向上渐隐
    static Sprite BuildRainSprite()
    {
        const int w = 16;
        const int h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "TitleRain",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float fy = (y + 0.5f) / h;           // 0 = 底部落点
            float head = Mathf.Pow(1f - fy, 1.5f);
            for (int x = 0; x < w; x++)
            {
                float fx = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float edge = Mathf.Pow(Mathf.Clamp01(1f - fx), 1.6f);
                float a = head * edge;
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite BuildVignetteSprite()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            name = "TitleVignette",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v) * 0.7071f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, d)) * 0.82f;
                pixels[y * n + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static float Band(float r, float center, float width)
    {
        return Mathf.Clamp01(1f - Mathf.Abs(r - center) / width);
    }
}
