using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 角色选择界面：运行时自建 Canvas（ScreenSpaceOverlay，sortingOrder 310），
// 不往场景里放任何对象，也不依赖 prefab。
// 角色列表来自 CharacterSelection.All，选择结果由 CharacterSelection.Select 持久化。
// 开关由 GameManager 的标题页输入统一调度（C 打开，C / ESC 关闭），并与永久商店互斥。
public class CharacterSelectScreen : MonoBehaviour
{
    private const int CanvasSortingOrder = 310;   // 高于永久商店(300)，选角时压在最上层
    private const float PanelWidth = 1040f;
    private const float MaxPanelHeight = 940f;    // 1080 参考高度下留出上下边距
    private const float HeaderHeight = 130f;
    private const float FooterHeight = 90f;
    private const float RowWidth = 960f;
    private const float DesiredRowHeight = 168f;
    private const float MinRowHeight = 96f;

    private static CharacterSelectScreen instance;
    private static Font cachedFont;
    private static Sprite cachedWhiteSprite;

    // 选角界面是否打开：GameManager 用它判断是否吞掉「任意键开始」
    public static bool IsOpen { get; private set; }

    // 一张角色卡片的 UI 引用；数据只读，刷新时重新向 CharacterSelection 取选中状态
    private sealed class CharacterRow
    {
        public CharacterData data;
        public Image background;
        public Image accent;
        public Text nameText;
        public Button selectButton;
        public Text selectLabel;
    }

    private readonly List<CharacterRow> rows = new List<CharacterRow>();
    private CharacterData[] characters = new CharacterData[0];
    private Text currentText;
    private bool built;

    // 关闭域重载时静态字段会跨 Play 保留，这里回到初始状态，避免下次进入 Play 时残留 IsOpen=true
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        IsOpen = false;
        cachedFont = null;
        cachedWhiteSprite = null;
    }

    // ---------------- 对外入口 ----------------

    public static void Show()
    {
        // 只在运行时创建 UI，避免编辑模式下往场景里塞临时对象
        if (!Application.isPlaying) return;

        var screen = instance != null ? instance : CreateInstance();
        screen.EnsureBuilt();
        screen.Open();
    }

    public static void Hide()
    {
        IsOpen = false;
        if (instance != null) instance.gameObject.SetActive(false);
    }

    // 懒构建：首次打开时才读取角色列表，避免 Awake 阶段数据尚未就绪
    private void EnsureBuilt()
    {
        if (built) return;
        built = true;
        BuildUI();
    }

    // 懒加载：跟随当前场景创建（不做 DontDestroyOnLoad），重开场景时界面一起销毁
    private static CharacterSelectScreen CreateInstance()
    {
        var go = new GameObject("CharacterSelectCanvas", typeof(RectTransform));
        return go.AddComponent<CharacterSelectScreen>();
    }

    private void Open()
    {
        // 与永久商店互斥：同一时间只允许一个浮层
        MetaShopScreen.Hide();
        gameObject.SetActive(true);
        Refresh();
        IsOpen = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            IsOpen = false; // 场景重载时避免静态状态残留
        }
    }

    // ---------------- UI 搭建 ----------------

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject); // 保证同一时间只有一套选角界面
            return;
        }
        instance = this;
    }

    private void BuildUI()
    {
        var rect = GetComponent<RectTransform>();
        if (rect == null) rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        // 与 MetaShopScreen / SettlementScreen / WaveBanner 一致的 1920x1080 参考分辨率
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // SELECT / 关闭按钮需要点击事件（事件系统由场景提供）
        gameObject.AddComponent<GraphicRaycaster>();

        var font = ResolveFont();

        // 读取角色并按行数计算面板高度：角色多时自动压缩行高，避免溢出屏幕
        LoadCharacters();
        int rowCount = characters.Length;
        float rowHeight = DesiredRowHeight;
        float needed = HeaderHeight + FooterHeight + rowCount * rowHeight;
        if (needed > MaxPanelHeight)
            rowHeight = Mathf.Max(MinRowHeight, (MaxPanelHeight - HeaderHeight - FooterHeight) / Mathf.Max(1, rowCount));
        float panelHeight = Mathf.Clamp(HeaderHeight + FooterHeight + rowCount * rowHeight, 400f, MaxPanelHeight);
        float halfHeight = panelHeight * 0.5f;
        float halfWidth = PanelWidth * 0.5f;

        // 全屏深色遮罩：盖住标题页，同时吞掉鼠标点击
        var scrim = CreateImage("Scrim", transform, new Color(0f, 0f, 0f, 0.78f));
        Stretch(scrim.rectTransform);
        scrim.raycastTarget = true;

        // 面板外框 + 内衬，沿用黑魂风的暗色金属边框
        var border = CreateImage("PanelBorder", transform, new Color(0.30f, 0.25f, 0.20f, 0.92f));
        SetCentered(border.rectTransform, PanelWidth + 30f, panelHeight + 30f, Vector2.zero);

        var panel = CreateImage("Panel", border.transform, new Color(0.075f, 0.07f, 0.065f, 0.97f));
        SetCentered(panel.rectTransform, PanelWidth, panelHeight, Vector2.zero);
        panel.raycastTarget = true;

        // 标题（左）
        CreateText("Title", panel.transform, font, 46, new Vector2(360f, 64f),
            new Vector2(-300f, halfHeight - 58f), FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f, 1f),
            TextAnchor.MiddleLeft).text = "选择角色";

        // 当前选中角色（右上，靠内）
        currentText = CreateText("Current", panel.transform, font, 26, new Vector2(360f, 48f),
            new Vector2(150f, halfHeight - 58f), FontStyle.Bold, new Color(0.90f, 0.78f, 0.45f, 1f),
            TextAnchor.MiddleRight);

        // 关闭按钮（最右上角）
        CreateCloseButton(panel.transform, font, new Vector2(halfWidth - 100f, halfHeight - 58f));

        // 标题下的分隔线
        var divider = CreateImage("Divider", panel.transform, new Color(0.42f, 0.36f, 0.28f, 0.9f));
        SetCentered(divider.rectTransform, PanelWidth - 80f, 2f, new Vector2(0f, halfHeight - 100f));

        // 角色卡片：从分隔线下方开始逐张排列
        float firstRowY = halfHeight - HeaderHeight - rowHeight * 0.5f;
        for (int i = 0; i < rowCount; i++)
            CreateRow(panel.transform, font, characters[i], i, firstRowY - i * rowHeight, rowHeight);

        // 空状态：A 的角色数据尚未就绪时给出明确提示，不崩、不留白板
        var emptyTitle = CreateText("Empty", panel.transform, font, 30, new Vector2(PanelWidth - 160f, 44f),
            new Vector2(0f, 6f), FontStyle.Bold, new Color(0.72f, 0.69f, 0.64f, 0.9f), TextAnchor.MiddleCenter);
        emptyTitle.text = "暂无可用角色";
        emptyTitle.gameObject.SetActive(rowCount == 0);

        var emptyHint = CreateText("EmptyHint", panel.transform, font, 18, new Vector2(PanelWidth - 160f, 36f),
            new Vector2(0f, -34f), FontStyle.Normal, new Color(0.60f, 0.57f, 0.53f, 0.85f), TextAnchor.MiddleCenter);
        emptyHint.text = "角色数据尚未就绪";
        emptyHint.gameObject.SetActive(rowCount == 0);

        // 底部操作提示
        CreateText("KeyHint", panel.transform, font, 22, new Vector2(420f, 40f),
            new Vector2(-halfWidth + 40f + 210f, -halfHeight + 38f), FontStyle.Normal,
            new Color(0.86f, 0.83f, 0.78f, 0.7f), TextAnchor.MiddleLeft).text = "C / ESC 关闭";
    }

    // 读 CharacterSelection.All；空数据是合法状态（UI 走空态兜底）
    private void LoadCharacters()
    {
        var all = CharacterSelection.All;
        var list = new List<CharacterData>(all != null ? all.Length : 0);
        if (all != null)
        {
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) list.Add(all[i]);
        }
        characters = list.ToArray();
    }

    private void CreateRow(Transform parent, Font font, CharacterData data, int index, float y, float rowHeight)
    {
        float height = Mathf.Max(60f, rowHeight - 8f);

        // 交替底色，便于区分相邻卡片；选中态在 Refresh 里覆盖
        var bg = CreateImage("Row_" + (data != null ? data.id : index.ToString()), parent,
            index % 2 == 0
                ? new Color(0.115f, 0.105f, 0.095f, 0.9f)
                : new Color(0.095f, 0.088f, 0.08f, 0.9f));
        SetCentered(bg.rectTransform, RowWidth, height, new Vector2(0f, y));

        var row = new CharacterRow { data = data, background = bg };

        // 左侧竖条：选中时变亮金色，作为高亮的第二条视觉线索
        row.accent = CreateImage("Accent", bg.transform, new Color(0.30f, 0.25f, 0.20f, 0.9f));
        SetCentered(row.accent.rectTransform, 6f, height, new Vector2(-RowWidth * 0.5f + 3f, 0f));

        // 名字（左上）
        row.nameText = CreateText("Name", bg.transform, font, 30, new Vector2(420f, 40f),
            new Vector2(-250f, height * 0.5f - 38f), FontStyle.Bold,
            new Color(0.96f, 0.93f, 0.86f, 1f), TextAnchor.MiddleLeft);
        row.nameText.text = data != null
            ? (!string.IsNullOrEmpty(data.displayName) ? data.displayName
                : (!string.IsNullOrEmpty(data.id) ? data.id : "未命名角色"))
            : "(未命名)";

        // 描述（中左）：最多两行，超出截断
        var desc = CreateText("Desc", bg.transform, font, 16, new Vector2(440f, 48f),
            new Vector2(-250f, 4f), FontStyle.Normal, new Color(0.72f, 0.69f, 0.64f, 0.85f),
            TextAnchor.MiddleLeft);
        desc.text = data != null && !string.IsNullOrEmpty(data.description) ? data.description : "—";
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        desc.verticalOverflow = VerticalWrapMode.Truncate;

        // 被动（左下）：单行，超出截断
        var passive = CreateText("Passive", bg.transform, font, 16, new Vector2(440f, 36f),
            new Vector2(-250f, -42f), FontStyle.Normal, new Color(0.62f, 0.72f, 0.62f, 0.95f),
            TextAnchor.MiddleLeft);
        passive.text = "被动  " + (data != null && !string.IsNullOrEmpty(data.passiveDescription)
            ? data.passiveDescription : "—");
        passive.horizontalOverflow = HorizontalWrapMode.Wrap;
        passive.verticalOverflow = VerticalWrapMode.Truncate;

        // 初始武器（右中）
        CreateText("Weapon", bg.transform, font, 18, new Vector2(300f, 36f),
            new Vector2(215f, height * 0.5f - 38f), FontStyle.Normal,
            new Color(0.90f, 0.78f, 0.45f, 1f), TextAnchor.MiddleRight).text =
            "初始武器  " + (data != null ? WeaponName(data.initialWeapon) : "—");

        // SELECT 按钮（右下）
        row.selectButton = CreateSelectButton(bg.transform, font, new Vector2(280f, -42f), out row.selectLabel);
        var captured = row;
        row.selectButton.onClick.AddListener(() => OnSelectClicked(captured));

        rows.Add(row);
    }

    private Button CreateSelectButton(Transform parent, Font font, Vector2 anchoredPos, out Text label)
    {
        var go = new GameObject("SelectButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        SetCentered(rt, 180f, 56f, anchoredPos);

        var image = go.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = new Color(0.20f, 0.17f, 0.12f, 1f);
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(0.20f, 0.17f, 0.12f, 1f);
        colors.highlightedColor = new Color(0.34f, 0.28f, 0.18f, 1f);
        colors.pressedColor = new Color(0.12f, 0.10f, 0.08f, 1f);
        colors.selectedColor = colors.normalColor;
        // 已选中的卡片按钮不可点，用暖金色代替灰掉，视觉上表示「当前选择」
        colors.disabledColor = new Color(0.32f, 0.26f, 0.14f, 0.95f);
        button.colors = colors;

        label = CreateText("Text", rt, font, 22, new Vector2(170f, 50f), Vector2.zero,
            FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f, 1f), TextAnchor.MiddleCenter);
        label.text = "SELECT";
        return button;
    }

    private void CreateCloseButton(Transform parent, Font font, Vector2 anchoredPos)
    {
        var go = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        SetCentered(rt, 120f, 44f, anchoredPos);

        var image = go.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = new Color(0.16f, 0.14f, 0.12f, 1f);
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.14f, 0.12f, 1f);
        colors.highlightedColor = new Color(0.28f, 0.23f, 0.18f, 1f);
        colors.pressedColor = new Color(0.10f, 0.09f, 0.08f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(Hide);

        CreateText("Text", rt, font, 20, new Vector2(110f, 40f), Vector2.zero,
            FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f, 1f), TextAnchor.MiddleCenter).text = "关闭";
    }

    // ---------------- 数据刷新与选择 ----------------

    private void Refresh()
    {
        var selected = CharacterSelection.Selected;
        string selectedId = CurrentSelectedId();

        if (currentText != null)
            currentText.text = selected != null ? "当前角色  " + DisplayName(selected) : "当前角色  —";

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.data == null) continue;

            bool isSelected = !string.IsNullOrEmpty(selectedId) && row.data.id == selectedId;

            // 选中卡片：暖棕底色 + 金色左侧竖条 + 金色名字
            row.background.color = isSelected
                ? new Color(0.24f, 0.19f, 0.11f, 0.95f)
                : (i % 2 == 0
                    ? new Color(0.115f, 0.105f, 0.095f, 0.9f)
                    : new Color(0.095f, 0.088f, 0.08f, 0.9f));
            row.accent.color = isSelected
                ? new Color(0.90f, 0.78f, 0.45f, 1f)
                : new Color(0.30f, 0.25f, 0.20f, 0.9f);
            row.nameText.color = isSelected
                ? new Color(0.96f, 0.90f, 0.72f, 1f)
                : new Color(0.96f, 0.93f, 0.86f, 1f);

            row.selectLabel.text = isSelected ? "已选择" : "SELECT";
            row.selectLabel.color = isSelected
                ? new Color(0.96f, 0.90f, 0.72f, 1f)
                : new Color(0.96f, 0.93f, 0.86f, 1f);
            row.selectButton.interactable = !isSelected;
        }
    }

    private void OnSelectClicked(CharacterRow row)
    {
        if (row == null || row.data == null) return;
        // 持久化由 CharacterSelection.Select 负责（PlayerPrefs + Save）
        CharacterSelection.Select(row.data.id);
        Refresh();
    }

    // SelectedId 在未存档时会回落到第一个角色，正好用于默认高亮
    private static string CurrentSelectedId()
    {
        string id = CharacterSelection.SelectedId;
        if (!string.IsNullOrEmpty(id)) return id;
        var selected = CharacterSelection.Selected;
        return selected != null ? selected.id : "";
    }

    private static string DisplayName(CharacterData data)
    {
        if (data == null) return "—";
        if (!string.IsNullOrEmpty(data.displayName)) return data.displayName;
        return !string.IsNullOrEmpty(data.id) ? data.id : "未命名角色";
    }

    // 初始武器枚举 → 中文展示名
    private static string WeaponName(WeaponTarget weapon)
    {
        switch (weapon)
        {
            case WeaponTarget.Orb: return "法球";
            case WeaponTarget.Projectile: return "飞弹";
            case WeaponTarget.Pierce: return "穿透箭";
            case WeaponTarget.Lightning: return "闪电链";
            case WeaponTarget.Nova: return "新星爆发";
            case WeaponTarget.Boomerang: return "回旋镖";
            case WeaponTarget.All: return "全部";
            default: return weapon.ToString();
        }
    }

    // ---------------- 通用 UI 工具 ----------------

    // 纯色 Image；统一使用运行时生成的 1x1 白色 Sprite
    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // 铺满父节点
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 居中铺在父节点上，size 为宽高；pivot 居中
    private static void SetCentered(RectTransform rt, float width, float height, Vector2 anchoredPos)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = anchoredPos;
    }

    // 文字样式：与 MetaShopScreen / SettlementScreen 相同（描边 + 投影），居中锚点
    private static Text CreateText(string name, Transform parent, Font font, int fontSize, Vector2 size,
        Vector2 anchoredPos, FontStyle style, Color color, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        SetCentered(rt, size.x, size.y, anchoredPos);

        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.useGraphicAlpha = false;

        return text;
    }

    // 字体优先级：内置 LegacyRuntime → 场景中已有 Text 的字体 → 系统中文字体
    private static Font ResolveFont()
    {
        if (cachedFont != null) return cachedFont;

        try
        {
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch (Exception)
        {
            cachedFont = null;
        }

        if (cachedFont == null)
        {
            // 兜底 1：复用场景里现成 Text 的字体，保证一定拿得到字体资源
            var sceneText = FindFirstObjectByType<Text>(FindObjectsInactive.Include);
            if (sceneText != null) cachedFont = sceneText.font;
        }

        if (cachedFont == null)
        {
            // 兜底 2：内置字体不可用时，直接取系统中文字体（仅极端情况下执行）
            string[] candidates = { "PingFang SC", "Heiti SC", "Microsoft YaHei", "Noto Sans CJK SC" };
            string[] installed = Font.GetOSInstalledFontNames();
            for (int i = 0; i < candidates.Length; i++)
            {
                if (Array.IndexOf(installed, candidates[i]) >= 0)
                {
                    cachedFont = Font.CreateDynamicFontFromOSFont(candidates[i], 64);
                    break;
                }
            }
        }

        return cachedFont;
    }

    // 运行时生成 1x1 白色 Sprite 并缓存
    private static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite != null) return cachedWhiteSprite;

        var tex = Texture2D.whiteTexture;
        cachedWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
        cachedWhiteSprite.name = "CharacterSelectWhite";
        return cachedWhiteSprite;
    }
}
