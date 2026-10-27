using UnityEngine;
using UnityEngine.UI;

// Boss 血条：运行时自建一套 Canvas（ScreenSpaceOverlay），不往场景里放任何对象。
// 数据只读 BossStatus：Active 时淡入并平滑追赶 CurrentHealth/MaxHealth，
// 显示 Boss 名字与阶段；Active 关闭或玩家死亡（GameManager.isGameOver）时隐藏。
public class BossHealthBar : MonoBehaviour
{
    private const int CanvasSortingOrder = 90;   // 低于 WaveBanner 的 100，避免挡住波次横幅
    private const float HealthLerpSpeed = 10f;    // 指数插值速度：越大越快追平

    private static BossHealthBar instance;
    private static Font cachedFont;
    private static Sprite cachedWhiteSprite;

    private CanvasGroup canvasGroup;
    private Text nameText;
    private Text phaseText;
    private Image fillImage;

    private float displayedFill;   // 当前显示的血量比例（平滑追赶目标值）
    private bool wasVisible;       // 上一帧是否可见，用于处理显示/隐藏切换
    private bool labelDirty = true;
    private string lastName;
    private int lastPhase = -1;
    private int lastPhaseCount = -1;

    // 不依赖场景：进入 Play 后自动创建；DontDestroyOnLoad 让它在重开场景后仍能工作，
    // 显隐完全由 BossStatus.Active 决定，残留实例不会露出。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        var go = new GameObject("BossHealthBarCanvas", typeof(RectTransform));
        go.AddComponent<BossHealthBar>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject); // 保证同一时间只有一套 Boss 血条
            return;
        }
        instance = this;
        BuildUI();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // 运行时搭建 Canvas + 顶部居中的「名字 / 血条 / 阶段」布局
    private void BuildUI()
    {
        var rect = GetComponent<RectTransform>();
        if (rect == null) rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        // 与 WaveBanner 一致的 1920x1080 参考分辨率
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;           // 默认隐藏，等 BossStatus.Active
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var font = ResolveFont();

        // 容器：顶部居中，名字在上、血条居中、阶段在右下；只做布局，不参与绘制
        var container = new GameObject("BossBarRoot", typeof(RectTransform));
        var containerRt = container.GetComponent<RectTransform>();
        containerRt.SetParent(transform, false);
        containerRt.anchorMin = new Vector2(0.5f, 1f);
        containerRt.anchorMax = new Vector2(0.5f, 1f);
        containerRt.pivot = new Vector2(0.5f, 1f);
        containerRt.sizeDelta = new Vector2(1240f, 120f);
        containerRt.anchoredPosition = new Vector2(0f, -64f);

        // Boss 名字
        nameText = CreateText("BossName", containerRt, font, 36, new Vector2(1100f, 52f),
            new Vector2(0f, 0f), FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f, 1f),
            TextAnchor.MiddleCenter);

        // 血条背景（深色底 + 内缩的红色填充）
        var barBackground = CreateImage("BarBackground", containerRt, new Color(0.06f, 0.05f, 0.05f, 0.88f));
        var barRt = barBackground.rectTransform;
        barRt.anchorMin = new Vector2(0.5f, 1f);
        barRt.anchorMax = new Vector2(0.5f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.sizeDelta = new Vector2(1100f, 26f);
        barRt.anchoredPosition = new Vector2(0f, -58f);

        fillImage = CreateImage("Fill", barRt, new Color(0.62f, 0.09f, 0.1f, 1f));
        var fillRt = fillImage.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(3f, 3f);    // 留出 3px 深色边框
        fillRt.offsetMax = new Vector2(-3f, -3f);
        // Filled + Horizontal + Left：fillAmount 从左侧开始收缩
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;

        // 阶段文本
        phaseText = CreateText("Phase", containerRt, font, 22, new Vector2(1100f, 30f),
            new Vector2(0f, -88f), FontStyle.Normal, new Color(0.86f, 0.83f, 0.78f, 0.9f),
            TextAnchor.MiddleRight);
    }

    // 纯色 Image；统一使用运行时生成的 1x1 白色 Sprite（Filled 模式必须有 Sprite 才生效）
    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // 与 WaveBannerView 相同的文字样式：顶部锚点 + 描边 + 投影
    private static Text CreateText(string name, Transform parent, Font font, int fontSize, Vector2 size,
        Vector2 anchoredPos, FontStyle style, Color color, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.useGraphicAlpha = false;

        return text;
    }

    // 字体优先级：内置 LegacyRuntime → 场景中已有 Text 的字体 → 系统中文字体
    private static Font ResolveFont()
    {
        if (cachedFont != null) return cachedFont;

        try
        {
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch (System.Exception)
        {
            cachedFont = null;
        }

        if (cachedFont == null)
        {
            // 兜底 1：复用场景里现成 Text 的字体，保证一定拿得到字体资源
            var sceneText = FindFirstObjectByType<Text>(FindObjectsInactive.Include);
            if (sceneText != null) cachedFont = sceneText.font;
        }

        if (cachedFont == null)
        {
            // 兜底 2：内置字体不可用时，直接取系统中文字体（仅极端情况下执行）
            string[] candidates = { "PingFang SC", "Heiti SC", "Microsoft YaHei", "Noto Sans CJK SC" };
            string[] installed = Font.GetOSInstalledFontNames();
            for (int i = 0; i < candidates.Length; i++)
            {
                if (System.Array.IndexOf(installed, candidates[i]) >= 0)
                {
                    cachedFont = Font.CreateDynamicFontFromOSFont(candidates[i], 64);
                    break;
                }
            }
        }

        return cachedFont;
    }

    // 运行时生成 1x1 白色 Sprite 并缓存；Filled Image 没有 Sprite 时 fillAmount 不生效
    private static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite != null) return cachedWhiteSprite;

        var tex = Texture2D.whiteTexture;
        cachedWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
        cachedWhiteSprite.name = "BossHealthBarWhite";
        return cachedWhiteSprite;
    }

    void Update()
    {
        bool gameOver = GameManager.Instance != null && GameManager.Instance.isGameOver;
        bool visible = BossStatus.Active && !gameOver;

        if (visible)
        {
            RefreshLabels();

            int maxHealth = BossStatus.MaxHealth;
            float targetFill = maxHealth > 0
                ? Mathf.Clamp01(BossStatus.CurrentHealth / (float)maxHealth)
                : 0f;

            if (!wasVisible)
            {
                // 新 Boss 刚出现：直接对齐当前血量，避免从上一条血条的残留值滑过来
                displayedFill = targetFill;
            }
            else
            {
                // 指数插值：与帧率无关，timeScale = 0（升级/首页）时也能正常播放
                displayedFill = Mathf.Lerp(displayedFill, targetFill,
                    1f - Mathf.Exp(-HealthLerpSpeed * Time.unscaledDeltaTime));
                if (Mathf.Abs(displayedFill - targetFill) < 0.001f) displayedFill = targetFill;
            }

            // fillAmount 的 setter 会标脏重建网格，值没变时不写
            if (Mathf.Abs(fillImage.fillAmount - displayedFill) > 0.0005f)
            {
                fillImage.fillAmount = displayedFill;
            }
        }
        else
        {
            if (wasVisible)
            {
                // 隐藏后清掉文本缓存，下次出现时重新写入
                labelDirty = true;
                lastName = null;
                lastPhase = -1;
                lastPhaseCount = -1;
            }
            displayedFill = 0f;
        }

        // 只在可见性切换时改 alpha，避免每帧无谓的 UI 重建
        float targetAlpha = visible ? 1f : 0f;
        if (canvasGroup.alpha != targetAlpha) canvasGroup.alpha = targetAlpha;

        wasVisible = visible;
    }

    // 名字与阶段文本只在变化时更新，避免每帧产生字符串 GC
    private void RefreshLabels()
    {
        string bossName = BossStatus.BossName;
        if (labelDirty || !string.Equals(bossName, lastName))
        {
            lastName = bossName;
            nameText.text = string.IsNullOrEmpty(bossName) ? "BOSS" : bossName;
        }

        int phaseCount = Mathf.Max(1, BossStatus.PhaseCount);
        int phase = Mathf.Clamp(BossStatus.Phase, 1, phaseCount);
        if (labelDirty || phase != lastPhase || phaseCount != lastPhaseCount)
        {
            lastPhase = phase;
            lastPhaseCount = phaseCount;
            phaseText.text = "Phase " + phase + "/" + phaseCount;
        }

        labelDirty = false;
    }
}
