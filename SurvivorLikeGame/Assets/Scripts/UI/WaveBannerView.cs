using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 波次横幅的运行时实现：懒加载一套 Canvas + 主/副标题 Text + CanvasGroup，
// 按 FIFO 队列依次播放「淡入 → 停留 → 淡出」，全程使用 unscaled 时间，
// 因此升级暂停（timeScale = 0）或首页停留时也能正常播放。
public class WaveBannerView : MonoBehaviour
{
    // 一次 Show 请求；用 struct 避免排队时产生额外托管对象
    private struct Request
    {
        public string Title;
        public string Subtitle;
        public float Duration;
    }

    private const float FadeInDuration = 0.25f;   // 淡入时长
    private const float FadeOutDuration = 0.4f;   // 淡出时长
    private const int CanvasSortingOrder = 100;   // 压在 HUD 之上、死亡遮罩之下

    private static WaveBannerView instance;
    private static Font cachedFont;

    private readonly Queue<Request> queue = new Queue<Request>(4);

    private CanvasGroup canvasGroup;
    private Text titleText;
    private Text subtitleText;
    private bool running; // 协程是否正在处理队列

    public static WaveBannerView Instance
    {
        get
        {
            if (instance == null)
            {
                // 跟随当前场景创建，不做 DontDestroyOnLoad；重开场景时横幅一起销毁
                var go = new GameObject("WaveBannerCanvas");
                instance = go.AddComponent<WaveBannerView>();
            }
            return instance;
        }
    }

    // 供 WaveBanner 调用：排入队列；空闲时立即开始播放
    public void Enqueue(string title, string subtitle, float duration)
    {
        queue.Enqueue(new Request
        {
            Title = title,
            Subtitle = subtitle,
            Duration = Mathf.Max(0f, duration)
        });

        // 已在播放时不打断，等当前横幅走完自动接上下一条
        if (!running) StartCoroutine(PlayQueue());
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject); // 保证同一时间只有一套横幅 Canvas
            return;
        }
        instance = this;
        BuildUI();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // 懒加载整套 UI：Canvas(Overlay) + 顶部居中的主/副标题
    private void BuildUI()
    {
        var rect = gameObject.GetComponent<RectTransform>();
        if (rect == null) rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        // 按 1920x1080 参考分辨率等比缩放，适配不同窗口尺寸
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var font = ResolveFont();

        // 主标题：顶部居中，锚点 top-center，y 约 -80
        titleText = CreateText("Title", font, 64, new Vector2(1400f, 100f), new Vector2(0f, -80f),
            FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f, 1f));
        // 副标题：主标题正下方，字号略小、略暗
        subtitleText = CreateText("Subtitle", font, 30, new Vector2(1400f, 50f), new Vector2(0f, -180f),
            FontStyle.Normal, new Color(0.86f, 0.83f, 0.78f, 0.9f));
    }

    private Text CreateText(string name, Font font, int fontSize, Vector2 size, Vector2 anchoredPos,
        FontStyle style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(transform, false);

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
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        // 描边 + 投影，深色背景上也能看清
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

    // 队列播放：每条横幅完整走完淡入/停留/淡出后，再播放下一条
    private IEnumerator PlayQueue()
    {
        running = true;

        while (queue.Count > 0)
        {
            var request = queue.Dequeue();
            titleText.text = request.Title;
            subtitleText.text = request.Subtitle ?? string.Empty;

            // 淡入
            float t = 0f;
            while (t < FadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Smooth(Mathf.Clamp01(t / FadeInDuration));
                yield return null;
            }
            canvasGroup.alpha = 1f;

            // 停留（duration 为 unscaled 秒数）
            t = 0f;
            while (t < request.Duration)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // 淡出
            t = 0f;
            while (t < FadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Smooth(Mathf.Clamp01(t / FadeOutDuration));
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        running = false;
    }

    // smoothstep 缓动，和 TitleScreenController / YouDiedAnimator 保持一致
    private static float Smooth(float p)
    {
        return p * p * (3f - 2f * p);
    }
}
