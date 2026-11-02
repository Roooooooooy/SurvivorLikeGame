using UnityEngine;
using UnityEngine.UI;

// 列表行的统一卡片化装饰：左侧强调条 + 图标底座。
// 商店 / 选角 / 难度共用，让这几屏和升级卡牌用同一套视觉语言。
public static class CardRow
{
    public const float ChipSize = 64f;

    // 左侧竖条：颜色区分类别，高度略微内缩
    public static Image AddAccent(Transform row, Color color, float rowWidth, float rowHeight)
    {
        if (row == null) return null;
        var image = Ensure(row, "__Accent", color);
        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-rowWidth * 0.5f + 6f, 0f);
        rt.sizeDelta = new Vector2(4f, Mathf.Max(20f, rowHeight - 14f));
        return image;
    }

    // 图标底座：深色圆角块 + 细描边，图标居中放在上面
    public static Image AddIconChip(Transform row, Sprite icon, float x, float size = ChipSize)
    {
        if (row == null) return null;
        var theme = UITheme.Current;
        Color chip = theme != null ? theme.ink : new Color(0.02f, 0.03f, 0.04f, 1f);
        chip.a = 0.85f;

        var image = Ensure(row, "__IconChip", chip);
        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(size, size);

        if (theme != null && theme.panelSprite != null)
        {
            image.sprite = theme.panelSprite;
            image.type = Image.Type.Sliced;
        }

        // 细金边
        var border = Ensure(row, "__IconChipBorder", new Color(0f, 0f, 0f, 0f));
        var brt = border.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(x, 0f);
        brt.sizeDelta = new Vector2(size + 2f, size + 2f);
        if (theme != null && theme.frameSprite != null)
        {
            border.sprite = theme.frameSprite;
            border.type = Image.Type.Sliced;
            border.color = theme.WithAlpha(theme.gold, 0.35f);
        }
        brt.SetAsFirstSibling();

        // 图标本体
        var iconImage = Ensure(row, "__Icon", Color.white);
        var irt = iconImage.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = new Vector2(x, 0f);
        irt.sizeDelta = new Vector2(size - 18f, size - 18f);
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
        iconImage.enabled = icon != null;
        return iconImage;
    }

    // 状态徽章：已选择 / 已解锁 / 价格
    public static Text AddStateBadge(Transform row, Vector2 anchoredPos, Color color)
    {
        if (row == null) return null;
        var theme = UITheme.Current;
        var bg = Ensure(row, "__StateBadge", new Color(color.r, color.g, color.b, 0.12f));
        var rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(120f, 30f);
        if (theme != null && theme.panelSprite != null)
        {
            bg.sprite = theme.panelSprite;
            bg.type = Image.Type.Sliced;
        }

        var text = bg.GetComponentInChildren<Text>(true);
        if (text == null)
        {
            text = UIFactory.CreateText(bg.transform, "Text", string.Empty, 16, color, TextAnchor.MiddleCenter);
            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
        }
        text.color = color;
        return text;
    }

    static Image Ensure(Transform parent, string name, Color color)
    {
        var existing = parent.Find(name) as RectTransform;
        Image image;
        if (existing != null)
        {
            image = existing.GetComponent<Image>();
        }
        else
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            image = go.GetComponent<Image>();
        }
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}