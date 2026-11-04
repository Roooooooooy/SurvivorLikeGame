using UnityEngine;
using UnityEngine.UI;

// 文本内容变化时做一次轻微放大回弹（击杀数、金币数等）。
public class HudTextPunch : MonoBehaviour
{
    public float amount = 0.16f;
    public float duration = 0.18f;

    Text text;
    Vector3 baseScale = Vector3.one;
    string last;
    float timer;

    void Awake()
    {
        text = GetComponent<Text>();
        baseScale = transform.localScale;
        if (text != null) last = text.text;
    }

    void Update()
    {
        if (text == null) return;

        if (text.text != last)
        {
            last = text.text;
            timer = duration;
        }

        if (timer > 0f)
        {
            timer -= Time.unscaledDeltaTime;
            float k = duration > 0f ? Mathf.Clamp01(timer / duration) : 0f;
            transform.localScale = baseScale * (1f + amount * k);
        }
        else
        {
            transform.localScale = baseScale;
        }
    }
}
