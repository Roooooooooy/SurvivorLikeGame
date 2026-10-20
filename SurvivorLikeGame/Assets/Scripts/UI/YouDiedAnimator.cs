using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// 黑魂风死亡演出：低沉钟声 → "YOU DIED" 从黑暗中缓慢浮现 → 重开按钮延迟出现
// → 停留片刻自动回主页。死亡期间保持模拟运行，全程 unscaled 时间。
[RequireComponent(typeof(Image))]
public class YouDiedAnimator : MonoBehaviour
{
    public AudioClip deathSound;
    public float fadeInDuration = 2.5f;  // 文字淡入时长
    public float scrimMaxAlpha = 0.85f;
    public float buttonDelay = 2.0f;     // 重开按钮延迟出现
    public float holdDuration = 2.0f;    // 淡入完成后停留时长
    public bool autoReturnToTitle = true;

    private Image scrim;
    [SerializeField] private Text title;
    private GameObject restartButton;
    private Color titleColor;

    void Awake()
    {
        scrim = GetComponent<Image>();
        if (title == null)
        {
            var titleTransform = transform.Find("DeathTitle");
            if (titleTransform != null) title = titleTransform.GetComponent<Text>();
        }
        var t = transform.Find("RestartButton");
        if (t != null) restartButton = t.gameObject;
    }

    void OnEnable()
    {
        Debug.Log("[YouDiedAnimator] 死亡演出开始");
        if (scrim == null || title == null)
        {
            Debug.LogError("[YouDiedAnimator] 组件缺失: scrim=" + (scrim != null) + " title=" + (title != null));
            return;
        }

        // 低沉钟声
        if (deathSound != null)
            AudioSource.PlayClipAtPoint(deathSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero);

        titleColor = title.color;
        scrim.color = new Color(0f, 0f, 0f, 0f);
        var c = titleColor; c.a = 0f; title.color = c;
        if (restartButton != null) restartButton.SetActive(false);

        StartCoroutine(PlayFadeIn());
    }

    IEnumerator PlayFadeIn()
    {
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / fadeInDuration);
            float ease = p * p * (3f - 2f * p); // smoothstep：缓入缓出

            scrim.color = new Color(0f, 0f, 0f, scrimMaxAlpha * ease);
            var c = titleColor; c.a = ease; title.color = c;

            if (restartButton != null && t >= buttonDelay && !restartButton.activeSelf)
                restartButton.SetActive(true);

            yield return null;
        }

        // 确保终态精确
        scrim.color = new Color(0f, 0f, 0f, scrimMaxAlpha);
        title.color = titleColor;
        if (restartButton != null) restartButton.SetActive(true);

        // 停留片刻后自动回主页（场景重载会显示首页）
        if (autoReturnToTitle)
        {
            float hold = 0f;
            while (hold < holdDuration)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }
            if (GameManager.Instance != null) GameManager.Instance.RestartGame();
        }
    }
}
