using UnityEngine;

// 通用精灵爆发特效：按时间从 startScale 放大到 endScale 并淡出。
// 新星光环、闪电落点火花共用；由武器生成后调用 Configure 覆盖参数。
// 支持对象池复用：每次激活都重置播放进度与颜色，播放结束归还池。
public class SpriteBurstEffect : MonoBehaviour
{
    public float duration = 0.3f;
    public float startScale = 0.2f;
    public float endScale = 1f;

    private SpriteRenderer sr;
    private Color baseColor;
    private float elapsed;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
    }

    void OnEnable()
    {
        // 池化复用：重置播放进度、缩放与淡出前的原色
        elapsed = 0f;
        transform.localScale = Vector3.one * startScale;
        if (sr != null) sr.color = baseColor;
    }

    public void Configure(float targetScale, float life)
    {
        endScale = targetScale;
        duration = life;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float k = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, k);

        if (sr != null)
        {
            Color c = baseColor;
            c.a = baseColor.a * (1f - k);
            sr.color = c;
        }

        if (k >= 1f) SmallObjectPool.Release(gameObject); // 池化归还；非池化对象退回 Destroy
    }
}
