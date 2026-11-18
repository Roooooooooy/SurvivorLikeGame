using UnityEngine;
using UnityEngine.UI;

// 标题页改版（Hades 式竖排菜单 + 魂系字标 + 吸血鬼幸存者的局外状态摘要）：
// - 完全不透明地盖住游戏画面，背景换成光晕/烟雾/余烬组成的动态暗色场景
// - Kenney CC0 的九宫格边框与分隔线统一边框语言，大字号 Cinzel 标题居中
// - 开始游戏 / 永久商店 / 选择角色 / 选择难度 四项竖排菜单，上下选择 + Enter 确认
// - 菜单下方显示当前角色、难度与累计金币；底部保留版本号
// 全部运行时搭建，不修改场景；场景重载由 SceneUISkin 重新调用，天然可重入。
public static class TitleScreenV2
{
    public static bool Active { get; private set; }

    const float MenuWidth = 620f;
    const float ItemHeight = 56f;
    const float ItemGap = 8f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = false;
    }

    // 返回 false 表示场景里没有标题节点，调用方应回退到旧的金线标题布局
    public static bool Apply(Transform titleScreen, UITheme theme)
    {
        if (titleScreen == null || theme == null) return false;
        Active = true;

        // 重复调用（同一次加载里 SceneUISkin 可能被触发多次）直接复用已有结构
        if (titleScreen.Find("__TitleV2") != null) return true;

        var screen = titleScreen as RectTransform;

        // 完全遮住后面的游戏画面：标题页只保留自己的动态背景
        var backdrop = titleScreen.GetComponent<Image>();
        if (backdrop != null)
            backdrop.color = theme.ink;

        var backdropFx = AddBackdrop(titleScreen, theme, screen);
        AddOuterFrame(titleScreen, theme);

        // 以 1920x1080 为设计基准的等比容器：小窗口下菜单不会溢出
        var root = new GameObject("__TitleV2", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(titleScreen, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        root.anchoredPosition = Vector2.zero;
        float scale = screen != null && screen.rect.height > 1f ? screen.rect.height / 1080f : 1f;
        root.localScale = Vector3.one * Mathf.Clamp(scale, 0.6f, 1.5f);

        // 复用场景里的标题/副标题：CanvasGroup 淡入与 TitleScreenController 的引用都保持有效
        var titleTransform = titleScreen.Find("TitleText");
        var subTransform = titleScreen.Find("SubText");
        if (titleTransform != null) titleTransform.SetParent(root, false);
        if (subTransform != null) subTransform.SetParent(root, false);

        var titleText = titleTransform != null ? titleTransform.GetComponent<Text>() : null;
        if (titleText != null)
        {
            titleText.fontSize = 96;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = theme.goldHi;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;
            Place((RectTransform)titleTransform, new Vector2(0f, 222f), new Vector2(1400f, 140f));
            AddTitleDepth(titleText);

            var pulse = titleTransform.gameObject.GetComponent<TitleGlowPulse>();
            if (pulse == null) pulse = titleTransform.gameObject.AddComponent<TitleGlowPulse>();
            pulse.title = titleText;
            pulse.baseColor = theme.goldHi;
            pulse.peakColor = Color.Lerp(theme.goldHi, Color.white, 0.45f);
            pulse.speed = 1.1f;
        }

        var eyebrow = UIFactory.CreateText(root, "Eyebrow",
            "S U R V I V O R - L I K E   ·   H D - 2 D", 15,
            theme.WithAlpha(theme.gold, 0.62f), TextAnchor.MiddleCenter, true);
        Place(eyebrow.rectTransform, new Vector2(0f, 312f), new Vector2(900f, 24f));

        AddDivider(root, theme);

        var tagline = UIFactory.CreateText(root, "Tagline", "在黑暗中活到最后", 20,
            theme.WithAlpha(theme.parchment, 0.78f), TextAnchor.MiddleCenter);
        Place(tagline.rectTransform, new Vector2(0f, 84f), new Vector2(900f, 30f));

        // ---------- 菜单底板：Kenney 双线边框 + 半透明深色填充 ----------
        var card = new GameObject("MenuCard", typeof(RectTransform)).GetComponent<RectTransform>();
        card.SetParent(root, false);
        Place(card, new Vector2(0f, -135f), new Vector2(760f, 350f));

        var cardFill = UIFactory.CreateImage(card, "Fill", null,
            theme.WithAlpha(theme.panel, 0.55f), false, false);
        UIFactory.Stretch(cardFill.rectTransform);

        var cardFrame = Resources.Load<Sprite>("TitleUI/frame_double");
        if (cardFrame != null)
        {
            var cardBorder = UIFactory.CreateImage(card, "Border", cardFrame,
                theme.WithAlpha(theme.gold, 0.42f), true, false);
            UIFactory.Stretch(cardBorder.rectTransform);
        }

        // ---------- 主菜单 ----------
        var menuGo = new GameObject("Menu", typeof(RectTransform));
        var menuRoot = menuGo.GetComponent<RectTransform>();
        menuRoot.SetParent(root, false);
        menuRoot.anchorMin = menuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        menuRoot.pivot = new Vector2(0.5f, 0.5f);
        menuRoot.sizeDelta = Vector2.zero;
        menuRoot.anchoredPosition = Vector2.zero;

        var menu = root.gameObject.AddComponent<TitleMenu>();
        menu.backdrop = backdropFx;
        var definitions = new[]
        {
            new TitleMenu.Definition
            {
                label = "开始游戏", icon = "ic_w_projectile", action = StartRun
            },
            new TitleMenu.Definition
            {
                label = "永久商店", icon = "ic_coin", action = MetaShopScreen.Show
            },
            new TitleMenu.Definition
            {
                label = "选择角色", icon = "ic_health", action = CharacterSelectScreen.Show
            },
            new TitleMenu.Definition
            {
                label = "选择难度", icon = "ic_diff_normal", action = DifficultySelectScreen.Show
            },
        };

        var entries = new TitleMenu.Entry[definitions.Length];
        for (int i = 0; i < definitions.Length; i++)
        {
            float y = -10f - i * (ItemHeight + ItemGap);
            entries[i] = BuildEntry(menuRoot, theme, i, definitions[i], new Vector2(0f, y), menu);
        }
        menu.entries = entries;
        for (int i = 0; i < entries.Length; i++)
            menu.BindPointerHover(i);

        // 菜单下方：当前角色 / 难度 / 金币。
        // 先用中文占位创建，UIFactory 会据此选 CJK 字体；RefreshSummary 之后只改文本不改字体。
        var summary = UIFactory.CreateText(root, "Summary", "角色  —      难度  —      金币  0", 18,
            theme.WithAlpha(theme.parchment, 0.8f), TextAnchor.MiddleCenter);
        Place(summary.rectTransform, new Vector2(0f, -272f), new Vector2(1200f, 28f));
        menu.summaryText = summary;

        // 副标题改成操作提示；TitleScreenController 会继续让它呼吸
        if (subTransform != null)
        {
            var hint = subTransform.GetComponent<Text>();
            if (hint != null)
            {
                hint.text = "上下选择      ENTER  确认";
                hint.font = theme.ResolveFont(hint.text, false);
                hint.fontSize = 18;
                hint.fontStyle = FontStyle.Normal;
                hint.color = theme.gold;
                hint.alignment = TextAnchor.MiddleCenter;
                Place((RectTransform)subTransform, new Vector2(0f, -330f), new Vector2(900f, 30f));
            }
        }

        menu.ResetSelection();

        AddFooter(titleScreen, theme);
        return true;
    }

    static void StartRun()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartGame();
    }

    static TitleMenu.Entry BuildEntry(Transform parent, UITheme theme, int index,
        TitleMenu.Definition definition, Vector2 position, TitleMenu menu)
    {
        var go = new GameObject("Entry_" + index,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(MenuWidth, ItemHeight);
        rt.anchoredPosition = position;

        // 选中底的九宫格面板：默认全透明，由 TitleMenu 做淡入淡出
        var background = go.GetComponent<Image>();
        background.sprite = theme.panelSprite != null ? theme.panelSprite : UIFactory.WhiteSprite;
        background.type = theme.panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = theme.WithAlpha(theme.panel2, 0f);
        background.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None; // 颜色由 TitleMenu 统一驱动
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => menu.Activate(index));

        var accent = UIFactory.CreateImage(rt, "Accent", null,
            theme.WithAlpha(theme.gold, 0f), false, false);
        Place(accent.rectTransform, new Vector2(-MenuWidth * 0.5f + 2f, 0f), new Vector2(4f, 34f));

        var icon = UIFactory.CreateImage(rt, "Icon", UpgradeIcons.Load(definition.icon),
            theme.WithAlpha(theme.parchment, 0.52f), false, false);
        icon.preserveAspect = true;
        Place(icon.rectTransform, new Vector2(-72f, 0f), new Vector2(26f, 26f));

        var label = UIFactory.CreateText(rt, "Label", definition.label, 28,
            theme.WithAlpha(theme.parchment, 0.82f), TextAnchor.MiddleLeft, false, FontStyle.Bold);
        var labelRt = label.rectTransform;
        labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.sizeDelta = new Vector2(300f, 44f);
        labelRt.anchoredPosition = new Vector2(-40f, 0f);

        return new TitleMenu.Entry
        {
            button = button,
            background = background,
            accent = accent,
            icon = icon,
            labelText = label,
            action = definition.action,
        };
    }

    // 标题分隔线：同一 Kenney 素材左右镜像，中间保留一枚血色菱形
    static void AddDivider(Transform root, UITheme theme)
    {
        var sprite = Resources.Load<Sprite>("TitleUI/divider_fade");
        if (sprite != null)
        {
            var left = UIFactory.CreateImage(root, "DividerLeft", sprite,
                theme.WithAlpha(theme.gold, 0.8f), false, false);
            Place(left.rectTransform, new Vector2(-174f, 134f), new Vector2(288f, 54f));
            left.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            var right = UIFactory.CreateImage(root, "DividerRight", sprite,
                theme.WithAlpha(theme.gold, 0.8f), false, false);
            Place(right.rectTransform, new Vector2(174f, 134f), new Vector2(288f, 54f));
        }
        else
        {
            AddLine(root, "OrnamentLeft", new Vector2(-170f, 134f), new Vector2(260f, 1f),
                theme.WithAlpha(theme.gold, 0.55f));
            AddLine(root, "OrnamentRight", new Vector2(170f, 134f), new Vector2(260f, 1f),
                theme.WithAlpha(theme.gold, 0.55f));
        }

        var diamond = UIFactory.CreateImage(root, "OrnamentDiamond", null, theme.bloodHi, false, false);
        var rt = diamond.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 134f);
        rt.sizeDelta = new Vector2(10f, 10f);
        rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    static void AddLine(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var line = UIFactory.CreateImage(parent, name, null, color, false, false);
        var rt = line.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    // 动态背景：光晕、烟雾、余烬与暗角
    static TitleBackdrop AddBackdrop(Transform parent, UITheme theme, RectTransform screen)
    {
        var go = new GameObject("__TitleBackdrop", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        float width = screen != null && screen.rect.width > 1f ? screen.rect.width : 1920f;
        float height = screen != null && screen.rect.height > 1f ? screen.rect.height : 1080f;
        var fx = go.AddComponent<TitleBackdrop>();
        fx.Build(theme, width, height);
        return fx;
    }

    // 屏幕内缩的金色九宫格外框（Kenney border_010）；资源缺失时退回四条细线
    static void AddOuterFrame(Transform parent, UITheme theme)
    {
        if (theme.frameSprite != null)
        {
            var frame = UIFactory.CreateImage(parent, "Frame", theme.frameSprite,
                theme.WithAlpha(theme.gold, 0.34f), true, false);
            var rt = frame.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(38f, 38f);
            rt.offsetMax = new Vector2(-38f, -38f);
            return;
        }

        Color color = theme.WithAlpha(theme.gold, 0.16f);
        AddEdge(parent, "FrameTop", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(-120f, 1f), color);
        AddEdge(parent, "FrameBottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(-120f, 1f), color);
        AddEdge(parent, "FrameLeft", new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(1f, -120f), color);
        AddEdge(parent, "FrameRight", new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(1f, 0.5f), new Vector2(-46f, 0f), new Vector2(1f, -120f), color);
    }

    static void AddEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 position, Vector2 size, Color color)
    {
        var line = UIFactory.CreateImage(parent, name, null, color, false, false);
        var rt = line.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    // 底部只保留版本号，菜单操作统一由中间的呼吸提示表达
    static void AddFooter(Transform parent, UITheme theme)
    {
        var version = UIFactory.CreateText(parent, "FooterVersion",
            "SLIME SURVIVOR  ·  v" + Application.version, 13,
            theme.WithAlpha(theme.muted, 0.75f), TextAnchor.MiddleLeft, true);
        var vrt = version.rectTransform;
        vrt.anchorMin = vrt.anchorMax = new Vector2(0f, 0f);
        vrt.pivot = new Vector2(0f, 0f);
        vrt.anchoredPosition = new Vector2(62f, 34f);
        vrt.sizeDelta = new Vector2(520f, 24f);
    }

    static void AddTitleDepth(Text title)
    {
        if (title.GetComponent<Outline>() == null)
        {
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;
        }
        if (title.GetComponent<Shadow>() == null)
        {
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(0f, -7f);
            shadow.useGraphicAlpha = false;
        }
    }

    static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }
}
