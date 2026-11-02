using UnityEngine;
using UnityEngine.UI;

// 升级面板的卡牌化改造。
// 参考 Hades 的祝福卡：大图标、名称、描述、顶部稀有度色条、热键角标；
// 三张卡横向排列，替代原来的三行纯文字按钮。
public static class LevelUpCards
{
    public const float CardWidth = 260f;
    public const float CardHeight = 300f;
    const float PanelWidth = 900f;
    const float PanelHeight = 460f;

    static readonly Color EvolutionColor = new Color(0.906f, 0.827f, 0.627f, 1f); // goldHi
    static readonly Color WeaponColor = new Color(0.561f, 0.753f, 0.722f, 1f);    // tealHi
    static readonly Color StatColor = new Color(0.851f, 0.824f, 0.761f, 1f);      // parchment

    // 面板只重排一次：竖向布局改成横向、放大到能放下三张卡
    public static void EnsureLayout(Transform root, int optionCount)
    {
        if (root == null) return;

        // GameManager 传进来的是 upgrade（整屏遮罩），真正带布局组的是子物体 bg (2)
        var panel = root.Find("bg (2)") ?? root;

        var rt = panel as RectTransform;
        if (rt != null) rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        // 不能同时挂两个 LayoutGroup（基类互斥），直接停用竖向布局，
        // 由下面手动摆放三张卡的位置；childControl 本来就关闭，停用后完全可控。
        var vertical = panel.GetComponent<VerticalLayoutGroup>();
        if (vertical != null) vertical.enabled = false;

        // 标题和提示不参与卡片排版
        DetachFromLayout(panel, "LevelUpTitle");
        DetachFromLayout(panel, "LevelUpHint");
        LayoutTitle(panel);

        // 先把三张卡横向摆好，避免首帧按旧竖向位置闪一下；
        // 原先被竖向布局排版的装饰（分隔线等）现在没有归属，直接隐藏
        // 按本次实际选项数居中；不足 3 个时多余按钮随后会被隐藏
        int cardCount = Mathf.Max(1, optionCount);

        int index = 0;
        for (int i = 0; i < panel.childCount; i++)
        {
            var child = panel.GetChild(i) as RectTransform;
            if (child == null) continue;

            var name = child.name;
            if (name == "LevelUpTitle" || name == "LevelUpHint" || name.StartsWith("__")) continue;

            if (child.GetComponent<Button>() != null)
            {
                PlaceCard(child, index++, cardCount);
            }
            else if (child.GetComponent<Image>() != null)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    static void PlaceCard(RectTransform card, int index, int count)
    {
        if (card == null) return;
        int n = Mathf.Max(1, count);
        float center = (n - 1) * 0.5f;
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = new Vector2((index - center) * (CardWidth + 22f), -18f);
        card.sizeDelta = new Vector2(CardWidth, CardHeight);
    }

    static void DetachFromLayout(Transform panel, string name)
    {
        var child = panel.Find(name);
        if (child == null) return;
        var element = child.GetComponent<LayoutElement>();
        if (element == null) element = child.gameObject.AddComponent<LayoutElement>();
        element.ignoreLayout = true;
    }

    static void LayoutTitle(Transform panel)
    {
        var title = panel.Find("LevelUpTitle") as RectTransform;
        if (title != null)
        {
            title.anchorMin = new Vector2(0.5f, 1f);
            title.anchorMax = new Vector2(0.5f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.anchoredPosition = new Vector2(0f, -26f);
            title.sizeDelta = new Vector2(600f, 56f);
        }

        var hint = panel.Find("LevelUpHint") as RectTransform;
        if (hint != null)
        {
            hint.anchorMin = new Vector2(0.5f, 0f);
            hint.anchorMax = new Vector2(0.5f, 0f);
            hint.pivot = new Vector2(0.5f, 0f);
            hint.anchoredPosition = new Vector2(0f, 16f);
            hint.sizeDelta = new Vector2(600f, 32f);
        }
    }

    public static void Apply(Button button, UpgradeOption option, int index)
    {
        if (button == null) return;

        var rt = button.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(CardWidth, CardHeight);

        // 按钮自身成为卡片底色
        var background = button.targetGraphic as Image;
        if (background == null) background = button.GetComponent<Image>();
        if (background != null) background.color = new Color(0.082f, 0.114f, 0.153f, 1f); // 比面板亮一档，读作卡片

        Color accent = AccentFor(option);

        // 顶部稀有度色条
        var bar = EnsureChild(button.transform, "CardAccent", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -8f), new Vector2(CardWidth - 24f, 5f));
        var barImage = bar.GetComponent<Image>();
        barImage.color = accent;

        // 大图标
        var iconRt = EnsureChild(button.transform, "CardIcon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -34f), new Vector2(72f, 72f));
        var icon = iconRt.GetComponent<Image>();
        icon.sprite = UpgradeIcons.ForOption(option);
        icon.preserveAspect = true;
        icon.enabled = icon.sprite != null;

        // 名称：复用按钮原有的 Text
        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0.5f, 1f);
            labelRt.anchorMax = new Vector2(0.5f, 1f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.anchoredPosition = new Vector2(0f, -118f);
            labelRt.sizeDelta = new Vector2(CardWidth - 32f, 58f);
            label.alignment = TextAnchor.UpperCenter;
            label.fontSize = 23;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.color = accent;
        }

        // 描述（底板透明，只用来定位文字）
        var descRt = EnsureChild(button.transform, "CardDesc", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -184f), new Vector2(CardWidth - 36f, 76f));
        var descBg = descRt.GetComponent<Image>();
        if (descBg != null) descBg.color = new Color(0f, 0f, 0f, 0f);
        var desc = descRt.GetComponentInChildren<Text>(true);
        if (desc == null)
        {
            desc = UIFactory.CreateText(descRt, "Text", string.Empty, 15,
                new Color(0.72f, 0.70f, 0.66f, 1f), TextAnchor.UpperCenter);
            var inner = desc.rectTransform;
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = Vector2.zero;
            inner.offsetMax = Vector2.zero;
        }
        desc.text = option != null && !string.IsNullOrEmpty(option.description)
            ? option.description
            : string.Empty;
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        desc.verticalOverflow = VerticalWrapMode.Truncate;

        // 热键角标
        var keyRt = EnsureChild(button.transform, "CardKey", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 20f), new Vector2(44f, 32f));
        var keyImage = keyRt.GetComponent<Image>();
        keyImage.color = new Color(accent.r, accent.g, accent.b, 0.18f);

        var keyText = keyRt.GetComponentInChildren<Text>(true);
        if (keyText == null)
        {
            keyText = UIFactory.CreateText(keyRt, "Text", string.Empty, 18, accent, TextAnchor.MiddleCenter);
            var inner = keyText.rectTransform;
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = Vector2.zero;
            inner.offsetMax = Vector2.zero;
        }
        keyText.text = (index + 1).ToString();
        keyText.color = accent;
    }

    static Color AccentFor(UpgradeOption option)
    {
        if (option == null) return StatColor;
        if (option.IsEvolution) return EvolutionColor;

        var data = option.upgrade;
        if (data != null && data.effects != null)
        {
            foreach (var e in data.effects)
            {
                if (e != null && e.stat == UpgradeStat.WeaponGrant) return WeaponColor;
            }
        }
        return StatColor;
    }

    static RectTransform EnsureChild(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null)
        {
            existing.anchorMin = anchorMin;
            existing.anchorMax = anchorMax;
            existing.pivot = new Vector2(0.5f, anchorMax.y);
            existing.anchoredPosition = anchoredPos;
            existing.sizeDelta = size;
            return existing;
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, anchorMax.y);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return rt;
    }
}