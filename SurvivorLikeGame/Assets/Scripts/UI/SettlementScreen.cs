using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 本局结算界面：运行时自建一套 Canvas（ScreenSpaceOverlay，sortingOrder 200），
// 不往场景里放任何对象，也不依赖任意 prefab。
// 死亡演出结束后由 YouDiedAnimator 调用 ShowRunSummary：
// 先取 RunStats.FinalizeRun 的快照（幂等，重复调用不会重复入账），再创建/刷新界面。
public class SettlementScreen : MonoBehaviour
{
    private const int CanvasSortingOrder = 200;   // 高于 WaveBanner(100) 与死亡遮罩，压在最上层
    private const float FadeInDuration = 0.4f;    // 界面淡入时长（unscaled）
    private const float PanelWidth = 760f;
    private const float PanelHeight = 600f;

    private static SettlementScreen instance;
    private static Font cachedFont;
    private static Sprite cachedWhiteSprite;

    private CanvasGroup canvasGroup;
    private Text timeValue;
    private Text killsValue;
    private Text coinsValue;
    private Text totalCoinsValue;
    private Coroutine fadeRoutine;

    // 结算入口：可重复调用（死亡演出、提前点重开、手动调试都走这里）。
    // FinalizeRun 幂等，因此多次调用只会得到同一份快照，不会重复累计金币。
    public static void ShowRunSummary()
    {
        // 只在运行时创建 UI，避免编辑模式下往场景里塞临时对象
        if (!Application.isPlaying) return;

        var summary = RunStats.FinalizeRun();

        var screen = instance != null ? instance : CreateInstance();
        screen.Refresh(summary);
        screen.Show();
    }

    // 懒加载：跟随当前场景创建（不做 DontDestroyOnLoad），重开场景时结算界面一起销毁
    private static SettlementScreen CreateInstance()
    {
        var go = new GameObject("SettlementCanvas", typeof(RectTransform));
        return go.AddComponent<SettlementScreen>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject); // 保证同一时间只有一套结算界面
            return;
        }
        instance = this;
        BuildUI();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // 运行时搭建「深色遮罩 + 居中面板 + 统计行 + 重开按钮」
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

        // 与 WaveBanner / BossHealthBar 一致的 1920x1080 参考分辨率
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 重开按钮需要点击事件，Canvas 必须有 GraphicRaycaster（事件系统由场景提供）
        gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;           // 默认隐藏，等 ShowRunSummary
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var font = ResolveFont();

        // 全屏深色遮罩：同时挡住后面死亡面板上的旧重开按钮
        var scrim = CreateImage("Scrim", transform, new Color(0f, 0f, 0f, 0.72f));
        scrim.rectTransform.anchorMin = Vector2.zero;
        scrim.rectTransform.anchorMax = Vector2.one;
        scrim.rectTransform.offsetMin = Vector2.zero;
        scrim.rectTransform.offsetMax = Vector2.zero;
        scrim.raycastTarget = true;

        // 面板外框 + 内衬，做出一点黑魂风的金属边框感
        var border = CreateImage("PanelBorder", transform, new Color(0.30f, 0.25f, 0.20f, 0.92f));
        SetCentered(border.rectTransform, PanelWidth + 30f, PanelHeight + 30f, Vector2.zero);

        var panel = CreateImage("Panel", border.transform, new Color(0.075f, 0.07f, 0.065f, 0.97f));
        SetCentered(panel.rectTransform, PanelWidth, PanelHeight, Vector2.zero);

        // 标题
        CreateText("Title", panel.transform, font, 60, new Vector2(PanelWidth - 100f, 80f),
            new Vector2(0f, 220f), FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f, 1f),
            TextAnchor.MiddleCenter).text = "本局结算";

        // 标题下的分隔线
        var divider = CreateImage("Divider", panel.transform, new Color(0.42f, 0.36f, 0.28f, 0.9f));
        SetCentered(divider.rectTransform, PanelWidth - 140f, 2f, new Vector2(0f, 168f));

        // 统计行：左侧标签、右侧数值
        timeValue = CreateStatRow(panel.transform, font, "时长", 105f);
        killsValue = CreateStatRow(panel.transform, font, "击杀", 40f);
        coinsValue = CreateStatRow(panel.transform, font, "获得金币", -25f);
        totalCoinsValue = CreateStatRow(panel.transform, font, "累计金币", -90f);

        // 操作提示
        CreateText("Hint", panel.transform, font, 26, new Vector2(PanelWidth - 120f, 40f),
            new Vector2(0f, -175f), FontStyle.Normal, new Color(0.86f, 0.83f, 0.78f, 0.75f),
            TextAnchor.MiddleCenter).text = "按 R 重新开始";

        // 重开按钮：Button 也走 GameManager.RestartGame
        CreateRestartButton(panel.transform, font);
    }

    // 一行统计：返回右侧数值 Text；label 固定在左、value 固定在右
    private Text CreateStatRow(Transform parent, Font font, string label, float y)
    {
        var labelText = CreateText("Label_" + label, parent, font, 34,
            new Vector2(PanelWidth - 140f, 44f), new Vector2(0f, y), FontStyle.Normal,
            new Color(0.86f, 0.83f, 0.78f, 0.9f), TextAnchor.MiddleLeft);
        labelText.text = label;

        var value = CreateText("Value_" + label, parent, font, 34,
            new Vector2(PanelWidth - 140f, 44f), new Vector2(0f, y), FontStyle.Bold,
            new Color(0.96f, 0.93f, 0.86f, 1f), TextAnchor.MiddleRight);
        return value;
    }

    private void CreateRestartButton(Transform parent, Font font)
    {
        var go = new GameObject("RestartButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        SetCentered(rt, 300f, 74f, new Vector2(0f, -250f));

        var image = go.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = new Color(0.16f, 0.14f, 0.12f, 1f);
        image.raycastTarget = true;

        // Button 的 ColorTint 会直接用状态色覆盖 targetGraphic 的颜色，
        // 因此深色底直接写在 ColorBlock 里，而不是 image.color。
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.14f, 0.12f, 1f);
        colors.highlightedColor = new Color(0.26f, 0.22f, 0.18f, 1f);
        colors.pressedColor = new Color(0.10f, 0.09f, 0.08f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.10f, 0.09f, 0.08f, 0.5f);
        button.colors = colors;
        button.onClick.AddListener(OnRestartClicked);

        var label = CreateText("Text", rt, font, 32, new Vector2(280f, 60f), Vector2.zero,
            FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f, 1f), TextAnchor.MiddleCenter);
        label.text = "RESTART (R)";
    }

    private void OnRestartClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.RestartGame();
    }

    // 用 RunSummary 刷新数值；同一快照重复刷新结果不变
    private void Refresh(RunSummary summary)
    {
        if (timeValue == null) return; // UI 尚未搭建完成

        timeValue.text = FormatTime(summary.TimeSeconds);
        killsValue.text = summary.Kills.ToString();
        coinsValue.text = summary.CoinsEarned.ToString();
        totalCoinsValue.text = summary.TotalCoins.ToString();
    }

    // mm:ss；负数与 NaN 兜底为 0
    private static string FormatTime(float seconds)
    {
        if (float.IsNaN(seconds) || seconds < 0f) seconds = 0f;
        int total = (int)seconds;
        return string.Format("{0:00}:{1:00}", total / 60, total % 60);
    }

    private void Show()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeIn());
    }

    // 淡入：全程 unscaled 时间，死亡时 timeScale 仍为 1 也能正常播放
    private IEnumerator FadeIn()
    {
        // 立即接管点击，避免淡入的 0.4 秒内点穿到后面死亡面板的旧按钮
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        float from = canvasGroup.alpha;
        float t = 0f;
        while (t < FadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / FadeInDuration);
            canvasGroup.alpha = Mathf.Lerp(from, 1f, Smooth(p));
            yield return null;
        }

        canvasGroup.alpha = 1f;
        fadeRoutine = null;
    }

    // 与 WaveBanner / YouDiedAnimator 一致的 smoothstep 缓动
    private static float Smooth(float p)
    {
        return p * p * (3f - 2f * p);
    }

    // 纯色 Image；统一使用运行时生成的 1x1 白色 Sprite
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

    // 居中铺在父节点上，size 为宽高；pivot 居中
    private static void SetCentered(RectTransform rt, float width, float height, Vector2 anchoredPos)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = anchoredPos;
    }

    // 文字样式：与 WaveBanner / BossHealthBar 相同（描边 + 投影），居中锚点
    private static Text CreateText(string name, Transform parent, Font font, int fontSize, Vector2 size,
        Vector2 anchoredPos, FontStyle style, Color color, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        SetCentered(rt, size.x, size.y, anchoredPos);

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

    // 运行时生成 1x1 白色 Sprite 并缓存
    private static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite != null) return cachedWhiteSprite;

        var tex = Texture2D.whiteTexture;
        cachedWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
        cachedWhiteSprite.name = "SettlementWhite";
        return cachedWhiteSprite;
    }
}
