using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// 游戏首页：标题缓慢淡入（黑魂风），副标题呼吸闪烁。
// 开始时 timeScale=0，全程使用 unscaled 时间。
public class TitleScreenController : MonoBehaviour
{
    public float fadeInDuration = 1.2f;
    public float blinkSpeed = 1.0f;      // 副标题呼吸频率
    public Text subtitle;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            StartCoroutine(FadeIn());
        }
    }

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / fadeInDuration);
            canvasGroup.alpha = p * p * (3f - 2f * p);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    void Update()
    {
        // 副标题呼吸闪烁（"PRESS ANY KEY"）
        if (subtitle != null)
        {
            var c = subtitle.color;
            c.a = 0.5f + 0.5f * Mathf.PingPong(Time.unscaledTime * blinkSpeed, 1f);
            subtitle.color = c;
        }
    }
}
