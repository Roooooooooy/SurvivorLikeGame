using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 运行时 UI 的共用构建器：所有面板/按钮/文字/血条都从这里出，
// 保证字体、颜色、九宫格 Sprite 一致。主题缺失时退化为安全默认值。
public static class UIFactory
{
    static Sprite whiteSprite;

    public static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
                whiteSprite.name = "UIWhite";
            }
            return whiteSprite;
        }
    }

    static Color PanelColor => UITheme.Current != null ? UITheme.Current.panel : new Color(0.06f, 0.08f, 0.11f, 1f);
    static Color Panel2Color => UITheme.Current != null ? UITheme.Current.panel2 : new Color(0.08f, 0.11f, 0.15f, 1f);
    static Color Gold => UITheme.Current != null ? UITheme.Current.gold : new Color(0.79f, 0.66f, 0.42f, 1f);
    static Color GoldHi => UITheme.Current != null ? UITheme.Current.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
    static Color Parchment => UITheme.Current != null ? UITheme.Current.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
    static Color Muted => UITheme.Current != null ? UITheme.Current.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
    static Color Line => UITheme.Current != null ? UITheme.Current.line : new Color(0.15f, 0.19f, 0.24f, 1f);
    static Color Blood => UITheme.Current != null ? UITheme.Current.blood : new Color(0.55f, 0.18f, 0.20f, 1f);
    static Color Teal => UITheme.Current != null ? UITheme.Current.teal : new Color(0.37f, 0.56f, 0.54f, 1f);

    public static Image CreatePanel(Transform parent, string name)
    {
        var theme = UITheme.Current;
        return CreateImage(parent, name, theme != null ? theme.panelSprite : null, PanelColor, true, false);
    }

    public static Image CreateImage(Transform parent, string name, Sprite sprite, Color color,
        bool sliced, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.sprite = sprite != null ? sprite : WhiteSprite;
        image.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    public static Text CreateText(Transform parent, string name, string text, int size, Color color,
        TextAnchor align, bool display = false, FontStyle style = FontStyle.Normal)
    {
        var theme = UITheme.Current;
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        var txt = go.GetComponent<Text>();
        // 含中文时自动切到 CJK 字体，否则用 Display/Body（Cinzel / VT323）
        txt.font = theme != null ? theme.ResolveFont(text, display) : null;
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text = text;
        txt.fontSize = size;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = align;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;
        AddOutline(txt);
        return txt;
    }

    public static void AddOutline(Text txt)
    {
        var outline = txt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;

        var shadow = txt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.useGraphicAlpha = false;
    }

    public static Button CreateButton(Transform parent, string name, string label, Vector2 size,
        UnityAction onClick)
    {
        var theme = UITheme.Current;
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.sprite = theme != null && theme.buttonSprite != null ? theme.buttonSprite : WhiteSprite;
        image.type = theme != null && theme.buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Panel2Color;
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.75f, 1f);
        colors.pressedColor = new Color(0.72f, 0.66f, 0.54f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        button.colors = colors;

        if (theme != null && theme.buttonPressedSprite != null)
        {
            var state = button.spriteState;
            state.pressedSprite = theme.buttonPressedSprite;
            state.highlightedSprite = theme.buttonSprite;
            state.selectedSprite = theme.buttonSprite;
            button.spriteState = state;
        }

        if (onClick != null) button.onClick.AddListener(onClick);

        var text = CreateText(go.transform, "Label", label, 28, GoldHi, TextAnchor.MiddleCenter,
            false, FontStyle.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    // 血条：返回外框，fill 为内部填充（Filled/Horizontal）
    public static Image CreateBar(Transform parent, string name, Color fillColor, float fillAmount,
        out Image fill)
    {
        var theme = UITheme.Current;
        var frame = CreateImage(parent, name,
            theme != null ? theme.barFrameSprite : null, Line, true, false);

        fill = CreateImage(frame.transform, "Fill",
            theme != null ? theme.barSprite : null, fillColor, false, false);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = fillAmount;

        var rt = fill.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(3f, 3f);
        rt.offsetMax = new Vector2(-3f, -3f);
        return frame;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
