using UnityEngine;
using UnityEngine.UI;

// 低血量时血条/心形图标做呼吸脉冲；回到安全血量后恢复主题色。
// 由 HudV2 挂在血条上并注入填充/心形引用。
public class HudHealthPulse : MonoBehaviour
{
    public Image fill;
    public Image heart;
    public Color baseColor = Color.white;
    public Color hiColor = Color.white;
    [Range(0f, 1f)] public float threshold = 0.3f;
    public float speed = 8f;

    float heartScale = 1f;

    void Start()
    {
        if (heart != null) heartScale = heart.rectTransform.localScale.x;
    }

    void Update()
    {
        if (fill == null) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        float hp = player.GetHealthPercent();
        if (hp > 0f && hp <= threshold)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
            fill.color = Color.Lerp(baseColor, hiColor, pulse * 0.7f);
            if (heart != null)
                heart.rectTransform.localScale = Vector3.one * heartScale * (1f + 0.12f * pulse);
        }
        else
        {
            fill.color = baseColor;
            if (heart != null)
                heart.rectTransform.localScale = Vector3.one * heartScale;
        }
    }
}
