using UnityEngine;
using UnityEngine.UI;

// 升级选项左侧图标：按需在按钮下补一个 Image，并在刷新时换图。
// 图标是运行时生成的，不改场景/预制体结构，也不影响原有的居中文本排版。
public static class UpgradeIconView
{
    public const string IconChildName = "UpgradeIcon";
    const float IconSize = 48f;
    const float LeftInset = 38f;

    public static void Apply(Button button, Sprite sprite)
    {
        if (button == null) return;

        var image = EnsureIcon(button);
        if (image == null) return;

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;

        // 有图标时文字左对齐并让出图标位；无图标时恢复按钮原有居中排版
        var label = button.GetComponentInChildren<Text>(true);
        if (label == null || label.gameObject == image.gameObject) return;

        var rt = label.rectTransform;
        bool hasIcon = sprite != null;
        rt.offsetMin = new Vector2(hasIcon ? 68f : 0f, rt.offsetMin.y);
        rt.offsetMax = new Vector2(hasIcon ? -12f : 0f, rt.offsetMax.y);
        label.alignment = hasIcon ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
    }

    static Image EnsureIcon(Button button)
    {
        var existing = button.transform.Find(IconChildName) as RectTransform;
        if (existing != null)
            return existing.GetComponent<Image>();

        var go = new GameObject(IconChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(button.transform, false);
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(LeftInset, 0f);
        rt.sizeDelta = new Vector2(IconSize, IconSize);

        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }
}