using UnityEngine;
using UnityEngine.UI;

// 场景固定 UI（标题 / HUD / 升级 / 死亡）的统一换肤。
// 场景加载后自动执行一次，只改字体、颜色和 Sprite，不动 RectTransform。
// 按精确路径查找，避免影响运行时动态创建的界面；场景重载后会重新应用。
public static class SceneUISkin
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return;

        var theme = UITheme.Current;
        if (theme == null) return;

        var root = canvas.transform;

        // HUD 根是普通 Transform，默认在 Canvas 中心；移到左上角，让血条/经验条归位。
        // 同时挂一个轻量显隐驱动：标题页与死亡期间隐藏 HUD，开局后恢复。
        var canvasRect = canvas.GetComponent<RectTransform>();
        var hud = Find(root, "HUD");
        if (hud != null)
        {
            if (canvasRect != null)
                hud.localPosition = new Vector3(-canvasRect.rect.width * 0.5f,
                    canvasRect.rect.height * 0.5f, 0f);

            var driver = canvas.GetComponent<SceneHudVisibility>();
            if (driver == null) driver = canvas.AddComponent<SceneHudVisibility>();
            driver.hud = hud.gameObject;

            var timer = Find(root, "TimerText");
            var kills = Find(root, "KillCountText");
            driver.timer = timer != null ? timer.gameObject : null;
            driver.kills = kills != null ? kills.gameObject : null;
        }

        // 标题
        SkinText(Find(root, "TitleScreen/TitleText"), theme, true, theme.goldHi);
        SkinText(Find(root, "TitleScreen/SubText"), theme, false, theme.muted);

        // HUD 文字
        SkinText(Find(root, "HUD/bg/HpText"), theme, false, theme.parchment);
        SkinText(Find(root, "HUD/bg (1)/levelText"), theme, false, theme.parchment);
        SkinText(Find(root, "TimerText"), theme, false, theme.parchment);
        SkinText(Find(root, "KillCountText"), theme, false, theme.parchment);

        // 血条 / 经验条：填充改为主题纯白条 + 主题色，后方补九宫格外框
        SkinBar(Find(root, "HUD/bg/hp"), "hp", theme, theme.blood);
        SkinBar(Find(root, "HUD/bg (1)/exp"), "exp", theme, theme.teal);

        // 升级面板
        var upgradePanel = Find(root, "upgrade/bg (2)");
        SkinPanel(upgradePanel, theme);
        AddFrameOverlay(upgradePanel, theme);
        SkinText(Find(upgradePanel, "LevelUpTitle"), theme, true, theme.goldHi);
        SkinText(Find(upgradePanel, "LevelUpHint"), theme, false, theme.muted);
        SkinUpgradeButtons(upgradePanel, theme);

        // 死亡面板
        var gameOver = Find(root, "txt_gameOver");
        SkinText(Find(gameOver, "DeathTitle"), theme, true, theme.goldHi);
        var restart = Find(gameOver, "RestartButton");
        if (restart != null)
        {
            SkinButton(restart.GetComponent<Button>(), theme);
            // 场景原布局把 Restart 放在屏幕正中，会压住 YOU DIED；下移到标题下方
            var restartRt = restart as RectTransform;
            if (restartRt != null) restartRt.anchoredPosition = new Vector2(0f, -150f);
        }
    }

    static Transform Find(Transform parent, string path)
    {
        return parent != null ? parent.Find(path) : null;
    }

    // 只换字体和颜色，保留字号、对齐等排版参数。
    // 字体统一走 ResolveFont：含中文时自动切到 CJK 字体。
    static void SkinText(Transform target, UITheme theme, bool display, Color color)
    {
        if (target == null) return;

        var text = target.GetComponent<Text>();
        if (text == null) return;

        text.font = theme.ResolveFont(text.text, display);
        text.color = color;
    }

    // 在面板上叠一层金色装饰边框（九宫格，透明中心，不挡点击）
    static void AddFrameOverlay(Transform panel, UITheme theme)
    {
        if (panel == null || theme.frameSprite == null) return;
        if (panel.Find("__FrameOverlay") != null) return;

        var go = new GameObject("__FrameOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(panel, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 升级面板带 VerticalLayoutGroup；叠加框必须跳出布局，否则会被排成一行
        var layout = go.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;

        var image = go.GetComponent<Image>();
        image.sprite = theme.frameSprite;
        image.type = Image.Type.Sliced;
        var color = theme.gold;
        color.a = 0.75f;
        image.color = color;
        image.raycastTarget = false;
        go.transform.SetAsLastSibling();
    }

    // 面板：九宫格底图 + panel 底色
    static void SkinPanel(Transform panel, UITheme theme)
    {
        if (panel == null) return;

        var image = panel.GetComponent<Image>();
        if (image == null) return;

        if (theme.panelSprite != null)
        {
            image.sprite = theme.panelSprite;
            image.type = Image.Type.Sliced;
        }
        image.color = theme.panel;
    }

    // 血条/经验条：替换 Filled 填充 Sprite，并在同尺寸位置补一层外框
    static void SkinBar(Transform bar, string id, UITheme theme, Color fillColor)
    {
        if (bar == null) return;

        var fill = bar.GetComponent<Image>();
        if (fill != null && theme.barSprite != null)
        {
            fill.sprite = theme.barSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.color = fillColor;
        }

        AddBarFrame(bar, id + "_BarFrame", theme);
    }

    static void AddBarFrame(Transform bar, string frameName, UITheme theme)
    {
        if (theme.barFrameSprite == null) return;

        var parent = bar.parent;
        if (parent == null) return;

        // 已存在则跳过，避免场景重载/重复调用叠加外框
        if (parent.Find(frameName) != null) return;

        var go = new GameObject(frameName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var source = bar as RectTransform;
        var rect = go.GetComponent<RectTransform>();
        if (source != null) CopyRect(source, rect);

        var image = go.GetComponent<Image>();
        image.sprite = theme.barFrameSprite;
        image.type = Image.Type.Sliced;
        image.color = theme.line;
        image.raycastTarget = false;

        // 放在填充条之前（后方），不改变填充条自身层级与尺寸
        go.transform.SetSiblingIndex(bar.GetSiblingIndex());
    }

    // 复制锚点、位置、尺寸和轴心；两个对象同属一个父节点，可直接复用
    static void CopyRect(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.pivot = source.pivot;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    // 升级面板里的三个按钮是 bg (2) 的直接子节点，逐个换皮
    static void SkinUpgradeButtons(Transform panel, UITheme theme)
    {
        if (panel == null) return;

        for (int i = 0; i < panel.childCount; i++)
        {
            var button = panel.GetChild(i).GetComponent<Button>();
            if (button != null) SkinButton(button, theme);
        }
    }

    static void SkinButton(Button button, UITheme theme)
    {
        if (button == null) return;

        var image = button.targetGraphic as Image;
        if (image == null) image = button.GetComponent<Image>();

        if (image != null)
        {
            if (theme.buttonSprite != null)
            {
                image.sprite = theme.buttonSprite;
                image.type = Image.Type.Sliced;
            }
            image.color = theme.panel2;
        }

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = theme.goldHi;
        colors.selectedColor = Color.white;
        colors.pressedColor = theme.gold;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        button.colors = colors;

        if (theme.buttonPressedSprite != null)
        {
            var state = button.spriteState;
            state.pressedSprite = theme.buttonPressedSprite;
            state.highlightedSprite = theme.buttonSprite;
            state.selectedSprite = theme.buttonSprite;
            button.spriteState = state;
        }

        // 子节点仍是 legacy Text（GameManager 运行时写入文本），只换字体与颜色
        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.font = theme.ResolveFont(label.text, false);
            label.color = theme.goldHi;
        }
    }
}
