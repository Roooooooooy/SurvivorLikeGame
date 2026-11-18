using UnityEngine;
using UnityEngine.UI;

// 标题字标的缓慢呼吸：亮度与尺寸轻微起伏，和背景法阵的脉冲形成呼应。
public class TitleGlowPulse : MonoBehaviour
{
    public Text title;
    public Color baseColor = Color.white;
    public Color peakColor = Color.white;
    public float speed = 1.1f;
    public float scaleAmount = 0.012f;

    void Update()
    {
        if (title == null) return;

        float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
        title.color = Color.Lerp(baseColor, peakColor, p * 0.7f);
        transform.localScale = Vector3.one * (1f + scaleAmount * p);
    }
}
