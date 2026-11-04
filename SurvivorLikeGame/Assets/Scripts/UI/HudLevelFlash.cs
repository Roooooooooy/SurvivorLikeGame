using UnityEngine;
using UnityEngine.UI;

// 升级时让等级徽章闪一下金色并轻微放大，给升级一个 HUD 层面的确认。
public class HudLevelFlash : MonoBehaviour
{
    public Image badge;
    public Color baseColor = Color.white;
    public Color flashColor = Color.white;
    public float duration = 0.45f;

    int lastLevel = -1;
    float timer;

    void Update()
    {
        var player = PlayerController.Instance;
        if (player == null) return;

        if (lastLevel < 0)
        {
            lastLevel = player.level;
        }
        else if (player.level != lastLevel)
        {
            lastLevel = player.level;
            timer = duration;
        }

        if (badge == null) return;

        if (timer > 0f)
        {
            timer -= Time.unscaledDeltaTime;
            float k = duration > 0f ? Mathf.Clamp01(timer / duration) : 0f;
            badge.color = Color.Lerp(baseColor, flashColor, k);
            badge.rectTransform.localScale = Vector3.one * (1f + 0.10f * k);
        }
        else
        {
            badge.color = baseColor;
            badge.rectTransform.localScale = Vector3.one;
        }
    }
}
