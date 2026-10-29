using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 本局结算界面：运行时自建一套 Canvas（ScreenSpaceOverlay，sortingOrder 200），
// 不往场景里放任何对象，也不依赖任意 prefab。
// 死亡演出结束后由 YouDiedAnimator 调用 ShowRunSummary：
// 先取 RunStats.FinalizeRun 的快照（幂等，重复调用不会重复入账），再创建/刷新界面。
// 视觉统一走 UITheme / UIFactory：金色标题、分隔线、暗色九宫格面板与主题按钮。
public class SettlementScreen : MonoBehaviour
{
    private const int CanvasSortingOrder = 200;   // 高于 WaveBanner(100) 与死亡遮罩，压在最上层
    private const float FadeInDuration = 0.4f;    // 界面淡入时长（unscaled）
    private const float PanelWidth = 760f;
    private const float PanelHeight = 600f;

    private static SettlementScreen instance;

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

    // 关闭域重载时清掉静态缓存，避免跨 Play 残留
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
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

        var theme = UITheme.Current;
        Color gold = theme != null ? theme.gold : new Color(0.79f, 0.66f, 0.42f, 1f);
        Color goldHi = theme != null ? theme.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
        Color parchment = theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
        Color tealHi = theme != null ? theme.tealHi : new Color(0.56f, 0.75f, 0.72f, 1f);
        Color borderColor = theme != null ? theme.WithAlpha(theme.gold, 0.65f) : new Color(0.30f, 0.25f, 0.20f, 0.92f);

        // 全屏深色遮罩：同时挡住后面死亡面板上的旧重开按钮
        var scrim = UIFactory.CreateImage(transform, "Scrim", null, new Color(0f, 0f, 0f, 0.72f),
            false, true);
        UIFactory.Stretch(scrim.rectTransform);

        // 面板外框（九宫格 border_010）+ 内衬（panel_tan），做出金属边框感
        var border = UIFactory.CreateImage(transform, "PanelBorder",
            theme != null ? theme.frameSprite : null, borderColor, true, false);
        SetCentered(border.rectTransform, PanelWidth + 30f, PanelHeight + 30f, Vector2.zero);

        var panel = UIFactory.CreatePanel(border.transform, "Panel");
        SetCentered(panel.rectTransform, PanelWidth, PanelHeight, Vector2.zero);

        // 标题（金色，DisplayFont；中文标题由 ResolveFont 切换到 CJK 子集）
        var title = UIFactory.CreateText(panel.transform, "Title", "本局结算", 60,
            goldHi, TextAnchor.MiddleCenter, true, FontStyle.Bold);
        SetCentered(title.rectTransform, PanelWidth - 100f, 80f, new Vector2(0f, 220f));

        // 标题下的分隔线
        var divider = UIFactory.CreateImage(panel.transform, "Divider", null,
            theme != null ? theme.WithAlpha(gold, 0.55f) : new Color(0.42f, 0.36f, 0.28f, 0.9f),
            false, false);
        SetCentered(divider.rectTransform, PanelWidth - 140f, 2f, new Vector2(0f, 168f));

        // 统计行：左侧标签、右侧数值；金币走语义色 gold
        timeValue = CreateStatRow(panel.transform, "时长", 105f, parchment);
        killsValue = CreateStatRow(panel.transform, "击杀", 40f, parchment);
        coinsValue = CreateStatRow(panel.transform, "获得金币", -25f, gold);
        totalCoinsValue = CreateStatRow(panel.transform, "累计金币", -90f, gold);

        // 操作提示（语义色 teal）
        var hint = UIFactory.CreateText(panel.transform, "Hint", "按 R 重新开始", 26,
            tealHi, TextAnchor.MiddleCenter, false, FontStyle.Normal);
        SetCentered(hint.rectTransform, PanelWidth - 120f, 40f, new Vector2(0f, -175f));

        // 重开按钮：Button 也走 GameManager.RestartGame
        CreateRestartButton(panel.transform, gold, goldHi);
    }

    // 一行统计：返回右侧数值 Text；label 固定在左、value 固定在右
    private Text CreateStatRow(Transform parent, string label, float y, Color valueColor)
    {
        var theme = UITheme.Current;
        Color labelColor = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);

        var labelText = UIFactory.CreateText(parent, "Label_" + label, label, 34,
            labelColor, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        SetCentered(labelText.rectTransform, PanelWidth - 140f, 44f, new Vector2(0f, y));

        // 行首图标：放在标签左边缘之外，不改变原有文字排版
        var icon = RowIcon.ForSettlement(label);
        if (icon != null)
        {
            RowIcon.Add(parent, "Icon_" + label, icon,
                new Vector2(-(PanelWidth * 0.5f - 42f), y), 40f);
        }

        // 数值是纯数字/时间，走 BodyFont（VT323）
        var value = UIFactory.CreateText(parent, "Value_" + label, string.Empty, 34,
            valueColor, TextAnchor.MiddleRight, false, FontStyle.Bold);
        SetCentered(value.rectTransform, PanelWidth - 140f, 44f, new Vector2(0f, y));
        return value;
    }

    private void CreateRestartButton(Transform parent, Color gold, Color goldHi)
    {
        var button = UIFactory.CreateButton(parent, "RestartButton", "RESTART (R)",
            new Vector2(300f, 74f), OnRestartClicked);
        SetCentered(button.GetComponent<RectTransform>(), 300f, 74f, new Vector2(0f, -250f));

        // 统一主题交互色：金色 hover / pressed
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = goldHi;
        colors.selectedColor = Color.white;
        colors.pressedColor = gold;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        button.colors = colors;

        var label = button.transform.Find("Label").GetComponent<Text>();
        label.fontSize = 30;
        label.color = goldHi;
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

    // 居中铺在父节点上，size 为宽高；pivot 居中
    private static void SetCentered(RectTransform rt, float width, float height, Vector2 anchoredPos)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = anchoredPos;
    }
}
