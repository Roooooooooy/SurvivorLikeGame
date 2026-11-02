using UnityEngine;
using UnityEngine.UI;

// 让图标贴在某个 Text 的左侧：按 Text 的实际渲染宽度定位，
// 这样击杀数从 0 涨到 10000 时图标不会和数字重叠。
[DisallowMultipleComponent]
public class TextIconFollower : MonoBehaviour
{
    public Text target;
    public float gap = 8f;

    RectTransform rt;

    void Awake()
    {
        rt = transform as RectTransform;
    }

    void LateUpdate()
    {
        if (target == null || rt == null) return;

        float w = target.preferredWidth;
        if (w <= 0f) w = target.rectTransform.rect.width;

        // 文本可能左对齐、居中或右对齐，按对齐方式算出内容左边缘
        float cx = target.rectTransform.rect.width;
        float left;
        switch (target.alignment)
        {
            case TextAnchor.MiddleLeft:
            case TextAnchor.UpperLeft:
            case TextAnchor.LowerLeft:
                left = 0f;
                break;
            case TextAnchor.MiddleRight:
            case TextAnchor.UpperRight:
            case TextAnchor.LowerRight:
                left = cx - w;
                break;
            default:
                left = (cx - w) * 0.5f;
                break;
        }

        // anchoredPosition 以锚点（默认中心）为原点，需要减去半个矩形宽度
        float x = left - gap - rt.sizeDelta.x * 0.5f - cx * 0.5f;
        rt.anchoredPosition = new Vector2(x, 0f);
    }
}