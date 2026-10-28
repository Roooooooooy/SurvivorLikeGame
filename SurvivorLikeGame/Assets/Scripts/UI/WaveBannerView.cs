using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 波次横幅的运行时实现：懒加载一套 Canvas + 主/副标题 Text + CanvasGroup，
// 按 FIFO 队列依次播放「淡入 → 停留 → 淡出」，全程使用 unscaled 时间，
// 因此升级暂停（timeScale = 0）或首页停留时也能正常播放。
// 视觉统一走 UITheme / UIFactory：标题用 DisplayFont，副标题用 BodyFont，
// 中文文案由 UITheme.ResolveFont 自动切到随包的 CJK 子集字体。
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

    // 关闭域重载时清掉静态缓存，避免跨 Play 残留
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
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

        var theme = UITheme.Current;
        Color titleColor = theme != null ? theme.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
        Color subtitleColor = theme != null ? theme.tealHi : new Color(0.56f, 0.75f, 0.72f, 1f);

        // 主标题：顶部居中，锚点 top-center，y 约 -80
        titleText = CreateBannerText("Title", 64, new Vector2(1400f, 100f), new Vector2(0f, -170f),
            titleColor, true, FontStyle.Bold);
        // 副标题：主标题正下方，字号略小、语义色 teal
        subtitleText = CreateBannerText("Subtitle", 30, new Vector2(1400f, 50f), new Vector2(0f, -260f),
            subtitleColor, false, FontStyle.Normal);
    }

    private Text CreateBannerText(string name, int size, Vector2 rectSize, Vector2 anchoredPos,
        Color color, bool display, FontStyle style)
    {
        var text = UIFactory.CreateText(transform, name, string.Empty, size, color,
            TextAnchor.MiddleCenter, display, style);
        UIFactory.SetRect(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            rectSize, anchoredPos);
        return text;
    }

    // 队列播放：每条横幅完整走完淡入/停留/淡出后，再播放下一条
    private IEnumerator PlayQueue()
    {
        running = true;

        while (queue.Count > 0)
        {
            var request = queue.Dequeue();
            SetBannerText(titleText, request.Title, true);
            SetBannerText(subtitleText, request.Subtitle ?? string.Empty, false);

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

    // 写入文案并交给主题按内容选字体（英文用 Cinzel/VT323，中文用 CJK 子集）
    private static void SetBannerText(Text text, string value, bool display)
    {
        text.text = value;
        var theme = UITheme.Current;
        if (theme != null) text.font = theme.ResolveFont(value, display);
    }

    // smoothstep 缓动，和 TitleScreenController / YouDiedAnimator 保持一致
    private static float Smooth(float p)
    {
        return p * p * (3f - 2f * p);
    }
}
