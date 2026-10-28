using UnityEngine;
using UnityEngine.UI;

// Boss 血条：运行时自建一套 Canvas（ScreenSpaceOverlay），不往场景里放任何对象。
// 数据只读 BossStatus：Active 时淡入并平滑追赶 CurrentHealth/MaxHealth，
// 显示 Boss 名字与阶段；Active 关闭或玩家死亡（GameManager.isGameOver）时隐藏。
// 视觉统一走 UITheme / UIFactory：Boss 名用 DisplayFont，阶段用 BodyFont，
// 血条为 barFrameSprite 外框 + blood 色 barSprite 填充。
public class BossHealthBar : MonoBehaviour
{
    private const int CanvasSortingOrder = 90;   // 低于 WaveBanner 的 100，避免挡住波次横幅
    private const float HealthLerpSpeed = 10f;    // 指数插值速度：越大越快追平

    private static BossHealthBar instance;

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

    // 关闭域重载时清掉静态状态，避免跨 Play 残留
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
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

        var theme = UITheme.Current;
        Color nameColor = theme != null ? theme.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
        Color phaseColor = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
        Color fillColor = theme != null ? theme.blood : new Color(0.55f, 0.18f, 0.20f, 1f);

        // 容器：顶部居中，名字在上、血条居中、阶段在右下；只做布局，不参与绘制
        var container = new GameObject("BossBarRoot", typeof(RectTransform));
        var containerRt = container.GetComponent<RectTransform>();
        containerRt.SetParent(transform, false);
        containerRt.anchorMin = new Vector2(0.5f, 1f);
        containerRt.anchorMax = new Vector2(0.5f, 1f);
        containerRt.pivot = new Vector2(0.5f, 1f);
        containerRt.sizeDelta = new Vector2(1240f, 120f);
        containerRt.anchoredPosition = new Vector2(0f, -64f);

        // Boss 名字（主题 DisplayFont；中文名字由 ResolveFont 在刷新时切换）
        nameText = UIFactory.CreateText(containerRt, "BossName", "BOSS", 36, nameColor,
            TextAnchor.MiddleCenter, true, FontStyle.Bold);
        UIFactory.SetRect(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(1100f, 52f), Vector2.zero);

        // 血条：九宫格外框 + blood 色 Filled 填充（内缩 3px 的深色边框）
        var barFrame = UIFactory.CreateBar(containerRt, "BossHealthBar", fillColor, 0f, out fillImage);
        UIFactory.SetRect(barFrame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(1100f, 26f), new Vector2(0f, -58f));

        // 阶段文本（纯拉丁/数字，BodyFont）
        phaseText = UIFactory.CreateText(containerRt, "Phase", string.Empty, 22, phaseColor,
            TextAnchor.MiddleRight, false, FontStyle.Normal);
        UIFactory.SetRect(phaseText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(1100f, 30f), new Vector2(0f, -88f));
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
            string display = string.IsNullOrEmpty(bossName) ? "BOSS" : bossName;
            nameText.text = display;
            var theme = UITheme.Current;
            if (theme != null) nameText.font = theme.ResolveFont(display, true);
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
