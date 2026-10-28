using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 永久商店：运行时自建一套 Canvas（ScreenSpaceOverlay，sortingOrder 300），
// 不往场景里放任何对象，也不依赖任意 prefab。
// 升级条目来自 Resources/MetaUpgrades 下的 MetaUpgradeData 资产，按 displayName/id/资产名稳定排序。
// 数据读写一律走 MetaProgress 契约：TotalCoins / GetUpgradeLevel / GetUpgradeCost / TryPurchase。
public class MetaShopScreen : MonoBehaviour
{
    private const int CanvasSortingOrder = 300;   // 高于结算界面(200)，保证标题页上始终压在最上层
    private const float PanelWidth = 940f;
    private const float MaxPanelHeight = 940f;    // 1080 参考高度下留出上下边距
    private const float HeaderHeight = 130f;
    private const float FooterHeight = 90f;
    private const float RowWidth = 860f;
    private const float DesiredRowHeight = 86f;
    private const float MinRowHeight = 52f;

    private static MetaShopScreen instance;
    private static Font cachedFont;
    private static Sprite cachedWhiteSprite;

    // 商店是否打开：GameManager 用它判断是否吞掉「任意键开始」
    public static bool IsOpen { get; private set; }

    // 一行升级的 UI 引用；数据只读，刷新时再向 MetaProgress 取值
    private sealed class UpgradeRow
    {
        public MetaUpgradeData data;
        public Text levelText;
        public Text priceText;
        public Button buyButton;
        public Text buyLabel;
    }

    private readonly List<UpgradeRow> rows = new List<UpgradeRow>();
    private MetaUpgradeData[] upgrades = new MetaUpgradeData[0];
    private Text coinsText;
    private Text messageText;
    private Text emptyText;

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
        screen.Open();
    }

    public static void Hide()
    {
        IsOpen = false;
        if (instance != null) instance.gameObject.SetActive(false);
    }

    public static void Toggle()
    {
        if (IsOpen) Hide();
        else Show();
    }

    // 懒加载：跟随当前场景创建（不做 DontDestroyOnLoad），重开场景时商店一起销毁
    private static MetaShopScreen CreateInstance()
    {
        var go = new GameObject("MetaShopCanvas", typeof(RectTransform));
        return go.AddComponent<MetaShopScreen>();
    }

    private void Open()
    {
        messageText.text = string.Empty; // 每次打开清掉上一次的购买提示
        gameObject.SetActive(true);      // 先激活，Refresh 里的显隐判断才有意义
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
            Destroy(gameObject); // 保证同一时间只有一套商店界面
            return;
        }
        instance = this;
        BuildUI();
        gameObject.SetActive(false); // 默认隐藏，Show 时再激活
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

        // 与 SettlementScreen / WaveBanner 一致的 1920x1080 参考分辨率
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // BUY / 关闭按钮需要点击事件（事件系统由场景提供）
        gameObject.AddComponent<GraphicRaycaster>();

        var font = ResolveFont();

        // 读取升级资产并计算面板高度：行数多时自动压缩行高，避免溢出屏幕
        LoadUpgrades();
        int rowCount = upgrades.Length;
        float rowHeight = DesiredRowHeight;
        float needed = HeaderHeight + FooterHeight + rowCount * rowHeight;
        if (needed > MaxPanelHeight)
            rowHeight = Mathf.Max(MinRowHeight, (MaxPanelHeight - HeaderHeight - FooterHeight) / Mathf.Max(1, rowCount));
        float panelHeight = Mathf.Clamp(HeaderHeight + FooterHeight + rowCount * rowHeight, 400f, MaxPanelHeight);
        float halfHeight = panelHeight * 0.5f;

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
        CreateText("Title", panel.transform, font, 46, new Vector2(340f, 64f),
            new Vector2(-260f, halfHeight - 58f), FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f, 1f),
            TextAnchor.MiddleLeft).text = "永久商店";

        // 累计金币（右上，靠内）
        coinsText = CreateText("Coins", panel.transform, font, 26, new Vector2(400f, 48f),
            new Vector2(90f, halfHeight - 58f), FontStyle.Bold, new Color(0.90f, 0.78f, 0.45f, 1f),
            TextAnchor.MiddleRight);

        // 关闭按钮（最右上角）
        CreateCloseButton(panel.transform, font, new Vector2(370f, halfHeight - 58f));

        // 标题下的分隔线
        var divider = CreateImage("Divider", panel.transform, new Color(0.42f, 0.36f, 0.28f, 0.9f));
        SetCentered(divider.rectTransform, PanelWidth - 80f, 2f, new Vector2(0f, halfHeight - 100f));

        // 升级行：从分隔线下方开始逐行排列
        float firstRowY = halfHeight - HeaderHeight - rowHeight * 0.5f;
        for (int i = 0; i < rowCount; i++)
            CreateRow(panel.transform, font, upgrades[i], i, firstRowY - i * rowHeight, rowHeight);

        // 空状态：没有 Assets/Resources/MetaUpgrades 资产时给出明确提示
        emptyText = CreateText("Empty", panel.transform, font, 28, new Vector2(PanelWidth - 120f, 60f),
            Vector2.zero, FontStyle.Normal, new Color(0.72f, 0.69f, 0.64f, 0.85f), TextAnchor.MiddleCenter);
        emptyText.text = "暂无可用升级";
        emptyText.gameObject.SetActive(rowCount == 0);

        // 底部：左侧固定操作提示，右侧显示购买结果
        CreateText("KeyHint", panel.transform, font, 22, new Vector2(420f, 40f),
            new Vector2(-PanelWidth * 0.5f + 40f + 210f, -halfHeight + 38f), FontStyle.Normal,
            new Color(0.86f, 0.83f, 0.78f, 0.7f), TextAnchor.MiddleLeft).text = "TAB / ESC 关闭";

        messageText = CreateText("Message", panel.transform, font, 22, new Vector2(460f, 40f),
            new Vector2(PanelWidth * 0.5f - 40f - 230f, -halfHeight + 38f), FontStyle.Bold,
            new Color(0.86f, 0.83f, 0.78f, 0.9f), TextAnchor.MiddleRight);
    }

    // 读 Resources/MetaUpgrades 下全部升级；排序保证每次打开顺序一致
    private void LoadUpgrades()
    {
        var all = Resources.LoadAll<MetaUpgradeData>("MetaUpgrades");
        var list = new List<MetaUpgradeData>(all != null ? all.Length : 0);
        if (all != null)
        {
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) list.Add(all[i]);
        }
        list.Sort(CompareUpgrades);
        upgrades = list.ToArray();
    }

    // 稳定顺序：displayName → id → 资产名；比较器包含唯一 id，排序结果可复现
    private static int CompareUpgrades(MetaUpgradeData a, MetaUpgradeData b)
    {
        int c = string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
        if (c != 0) return c;
        c = string.Compare(a.id, b.id, StringComparison.Ordinal);
        if (c != 0) return c;
        return string.Compare(a.name, b.name, StringComparison.Ordinal);
    }

    private void CreateRow(Transform parent, Font font, MetaUpgradeData data, int index,
        float y, float rowHeight)
    {
        // 交替底色，便于区分相邻行
        var bg = CreateImage("Row_" + (data != null ? data.id : index.ToString()), parent,
            index % 2 == 0
                ? new Color(0.115f, 0.105f, 0.095f, 0.9f)
                : new Color(0.095f, 0.088f, 0.08f, 0.9f));
        SetCentered(bg.rectTransform, RowWidth, Mathf.Max(40f, rowHeight - 8f), new Vector2(0f, y));

        var row = new UpgradeRow { data = data };

        // 名称 + 描述
        CreateText("Name", bg.transform, font, 26, new Vector2(320f, 34f),
            new Vector2(-270f, 14f), FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f, 1f),
            TextAnchor.MiddleLeft).text = data != null ? data.displayName : "(未命名)";

        var desc = CreateText("Desc", bg.transform, font, 15, new Vector2(320f, 30f),
            new Vector2(-270f, -20f), FontStyle.Normal, new Color(0.72f, 0.69f, 0.64f, 0.85f),
            TextAnchor.MiddleLeft);
        desc.text = data != null ? data.description : string.Empty;
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        desc.verticalOverflow = VerticalWrapMode.Truncate;

        // 等级
        row.levelText = CreateText("Level", bg.transform, font, 24, new Vector2(130f, 40f),
            new Vector2(-20f, 0f), FontStyle.Bold, new Color(0.86f, 0.83f, 0.78f, 0.95f),
            TextAnchor.MiddleCenter);

        // 下一级价格
        row.priceText = CreateText("Price", bg.transform, font, 24, new Vector2(130f, 40f),
            new Vector2(140f, 0f), FontStyle.Normal, new Color(0.90f, 0.78f, 0.45f, 1f),
            TextAnchor.MiddleCenter);

        // BUY 按钮
        row.buyButton = CreateBuyButton(bg.transform, font, new Vector2(350f, 0f), out row.buyLabel);
        var captured = row;
        row.buyButton.onClick.AddListener(() => OnBuyClicked(captured));

        rows.Add(row);
    }

    private Button CreateBuyButton(Transform parent, Font font, Vector2 anchoredPos, out Text label)
    {
        var go = new GameObject("BuyButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        SetCentered(rt, 150f, 54f, anchoredPos);

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
        colors.disabledColor = new Color(0.12f, 0.11f, 0.10f, 0.45f);
        button.colors = colors;

        label = CreateText("Text", rt, font, 22, new Vector2(140f, 48f), Vector2.zero,
            FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f, 1f), TextAnchor.MiddleCenter);
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

    // ---------------- 数据刷新与购买 ----------------

    private void Refresh()
    {
        if (coinsText != null) coinsText.text = "累计金币  " + MetaProgress.TotalCoins;

        int coins = MetaProgress.TotalCoins;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.data == null) continue;

            int maxLevel = Mathf.Max(1, row.data.maxLevel);
            int level = Mathf.Clamp(MetaProgress.GetUpgradeLevel(row.data.id), 0, maxLevel);
            bool maxed = level >= maxLevel;
            int cost = maxed ? 0 : Mathf.Max(0, MetaProgress.GetUpgradeCost(row.data));
            bool affordable = !maxed && coins >= cost;

            row.levelText.text = "Lv " + level + "/" + maxLevel;
            row.priceText.text = maxed ? "满级" : cost + " 金币";
            row.priceText.color = maxed
                ? new Color(0.60f, 0.58f, 0.54f, 0.9f)
                : affordable
                    ? new Color(0.90f, 0.78f, 0.45f, 1f)
                    : new Color(0.78f, 0.42f, 0.36f, 1f);

            row.buyLabel.text = maxed ? "MAX" : "BUY";
            row.buyLabel.color = affordable
                ? new Color(0.96f, 0.93f, 0.86f, 1f)
                : new Color(0.62f, 0.59f, 0.54f, 0.85f);
            row.buyButton.interactable = affordable; // 满级或金币不足时不可点
        }
    }

    private void OnBuyClicked(UpgradeRow row)
    {
        if (row == null || row.data == null) return;

        int maxLevel = Mathf.Max(1, row.data.maxLevel);
        int level = MetaProgress.GetUpgradeLevel(row.data.id);
        if (level >= maxLevel)
        {
            ShowMessage("已达最高等级", false);
            return;
        }

        int cost = Mathf.Max(0, MetaProgress.GetUpgradeCost(row.data));
        if (MetaProgress.TryPurchase(row.data))
        {
            ShowMessage("购买成功：" + row.data.displayName, true);
        }
        else
        {
            // A 的购买逻辑未接入时 TryPurchase 会返回 false，这里只给一行提示，不改任何状态
            ShowMessage(MetaProgress.TotalCoins < cost ? "金币不足" : "购买失败", false);
        }

        Refresh(); // 成功时同步金币/等级；失败时也刷新一次，保持与存档一致
    }

    private void ShowMessage(string text, bool success)
    {
        if (messageText == null) return;
        messageText.text = text;
        messageText.color = success
            ? new Color(0.55f, 0.85f, 0.55f, 1f)
            : new Color(0.90f, 0.45f, 0.40f, 1f);
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

    // 文字样式：与 SettlementScreen / WaveBanner 相同（描边 + 投影），居中锚点
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
        cachedWhiteSprite.name = "MetaShopWhite";
        return cachedWhiteSprite;
    }
}
