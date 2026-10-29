using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 永久商店：运行时自建一套 Canvas（ScreenSpaceOverlay，sortingOrder 300），
// 不往场景里放任何对象，也不依赖任意 prefab。
// 升级条目来自 Resources/MetaUpgrades 下的 MetaUpgradeData 资产，按 displayName/id/资产名稳定排序。
// 数据读写一律走 MetaProgress 契约：TotalCoins / GetUpgradeLevel / GetUpgradeCost / TryPurchase。
// 视觉统一走 UITheme / UIFactory：金色标题、分隔线、暗色九宫格面板与主题按钮。
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

        var theme = UITheme.Current;
        Color gold = theme != null ? theme.gold : new Color(0.79f, 0.66f, 0.42f, 1f);
        Color goldHi = theme != null ? theme.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
        Color parchment = theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
        Color muted = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
        Color bloodHi = theme != null ? theme.bloodHi : new Color(0.79f, 0.32f, 0.34f, 1f);
        Color borderColor = theme != null ? theme.WithAlpha(theme.gold, 0.65f) : new Color(0.30f, 0.25f, 0.20f, 0.92f);

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
        var scrim = UIFactory.CreateImage(transform, "Scrim", null, new Color(0f, 0f, 0f, 0.78f),
            false, true);
        UIFactory.Stretch(scrim.rectTransform);

        // 面板外框（九宫格 border_010）+ 内衬（panel_tan）
        var border = UIFactory.CreateImage(transform, "PanelBorder",
            theme != null ? theme.frameSprite : null, borderColor, true, false);
        SetCentered(border.rectTransform, PanelWidth + 30f, panelHeight + 30f, Vector2.zero);

        var panel = UIFactory.CreatePanel(border.transform, "Panel");
        SetCentered(panel.rectTransform, PanelWidth, panelHeight, Vector2.zero);
        panel.raycastTarget = true;

        // 标题（左，金色）
        var title = UIFactory.CreateText(panel.transform, "Title", "永久商店", 46,
            goldHi, TextAnchor.MiddleLeft, true, FontStyle.Bold);
        SetCentered(title.rectTransform, 340f, 64f, new Vector2(-260f, halfHeight - 58f));

        // 累计金币（右上，靠内，语义色 gold）
        coinsText = UIFactory.CreateText(panel.transform, "Coins", string.Empty, 26,
            gold, TextAnchor.MiddleRight, false, FontStyle.Bold);
        SetCentered(coinsText.rectTransform, 400f, 48f, new Vector2(90f, halfHeight - 58f));

        // 关闭按钮（最右上角）
        CreateCloseButton(panel.transform, new Vector2(370f, halfHeight - 58f));

        // 标题下的分隔线
        var divider = UIFactory.CreateImage(panel.transform, "Divider", null,
            theme != null ? theme.WithAlpha(gold, 0.55f) : new Color(0.42f, 0.36f, 0.28f, 0.9f),
            false, false);
        SetCentered(divider.rectTransform, PanelWidth - 80f, 2f, new Vector2(0f, halfHeight - 100f));

        // 升级行：从分隔线下方开始逐行排列
        float firstRowY = halfHeight - HeaderHeight - rowHeight * 0.5f;
        for (int i = 0; i < rowCount; i++)
            CreateRow(panel.transform, upgrades[i], i, firstRowY - i * rowHeight, rowHeight);

        // 空状态：没有 Assets/Resources/MetaUpgrades 资产时给出明确提示
        emptyText = UIFactory.CreateText(panel.transform, "Empty", "暂无可用升级", 28,
            muted, TextAnchor.MiddleCenter, false, FontStyle.Normal);
        SetCentered(emptyText.rectTransform, PanelWidth - 120f, 60f, Vector2.zero);
        emptyText.gameObject.SetActive(rowCount == 0);

        // 底部：左侧固定操作提示，右侧显示购买结果
        var keyHint = UIFactory.CreateText(panel.transform, "KeyHint", "TAB / ESC 关闭", 22,
            muted, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        SetCentered(keyHint.rectTransform, 420f, 40f,
            new Vector2(-PanelWidth * 0.5f + 40f + 210f, -halfHeight + 38f));

        messageText = UIFactory.CreateText(panel.transform, "Message", string.Empty, 22,
            parchment, TextAnchor.MiddleRight, false, FontStyle.Bold);
        SetCentered(messageText.rectTransform, 460f, 40f,
            new Vector2(PanelWidth * 0.5f - 40f - 230f, -halfHeight + 38f));
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

    private void CreateRow(Transform parent, MetaUpgradeData data, int index, float y, float rowHeight)
    {
        var theme = UITheme.Current;
        Color rowColor = index % 2 == 0
            ? (theme != null ? theme.panel2 : new Color(0.08f, 0.11f, 0.15f, 1f))
            : (theme != null ? theme.panel : new Color(0.06f, 0.08f, 0.11f, 1f));
        Color parchment = theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
        Color muted = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);

        // 交替底色，便于区分相邻行；九宫格内衬贴图
        var bg = UIFactory.CreateImage(parent, "Row_" + (data != null ? data.id : index.ToString()),
            null, rowColor, false, false);
        SetCentered(bg.rectTransform, RowWidth, Mathf.Max(40f, rowHeight - 8f), new Vector2(0f, y));

        var row = new UpgradeRow { data = data };

        // 左侧属性图标；文字整体右移让出图标位
        RowIcon.Add(bg.transform, "Icon", RowIcon.ForMetaUpgrade(data),
            new Vector2(-RowWidth * 0.5f + 46f, 0f));

        // 名称 + 描述
        var nameText = UIFactory.CreateText(bg.transform, "Name",
            data != null ? data.displayName : "(未命名)", 26, parchment,
            TextAnchor.MiddleLeft, false, FontStyle.Bold);
        SetCentered(nameText.rectTransform, 264f, 34f, new Vector2(-200f, 14f));

        var desc = UIFactory.CreateText(bg.transform, "Desc",
            data != null ? data.description : string.Empty, 15, muted,
            TextAnchor.MiddleLeft, false, FontStyle.Normal);
        SetCentered(desc.rectTransform, 264f, 30f, new Vector2(-200f, -20f));
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        desc.verticalOverflow = VerticalWrapMode.Truncate;

        // 等级（纯拉丁/数字，BodyFont）
        row.levelText = UIFactory.CreateText(bg.transform, "Level", string.Empty, 24,
            parchment, TextAnchor.MiddleCenter, false, FontStyle.Bold);
        SetCentered(row.levelText.rectTransform, 130f, 40f, new Vector2(-20f, 0f));

        // 下一级价格（含中文，刷新时由 ResolveFont 切到 CJK 子集）
        row.priceText = UIFactory.CreateText(bg.transform, "Price", string.Empty, 24,
            parchment, TextAnchor.MiddleCenter, false, FontStyle.Normal);
        SetCentered(row.priceText.rectTransform, 130f, 40f, new Vector2(140f, 0f));

        // BUY 按钮
        row.buyButton = CreateBuyButton(bg.transform, new Vector2(350f, 0f), out row.buyLabel);
        var captured = row;
        row.buyButton.onClick.AddListener(() => OnBuyClicked(captured));

        rows.Add(row);
    }

    private Button CreateBuyButton(Transform parent, Vector2 anchoredPos, out Text label)
    {
        var button = UIFactory.CreateButton(parent, "BuyButton", "BUY",
            new Vector2(150f, 54f), null);
        SetCentered(button.GetComponent<RectTransform>(), 150f, 54f, anchoredPos);
        StyleButton(button);

        label = button.transform.Find("Label").GetComponent<Text>();
        label.fontSize = 22;
        return button;
    }

    private void CreateCloseButton(Transform parent, Vector2 anchoredPos)
    {
        var button = UIFactory.CreateButton(parent, "CloseButton", "关闭",
            new Vector2(120f, 44f), Hide);
        SetCentered(button.GetComponent<RectTransform>(), 120f, 44f, anchoredPos);
        StyleButton(button);

        var label = button.transform.Find("Label").GetComponent<Text>();
        label.fontSize = 20;
    }

    // 统一主题交互色：白色常态、金色 hover / pressed
    private static void StyleButton(Button button)
    {
        var theme = UITheme.Current;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = theme != null ? theme.goldHi : new Color(1f, 0.92f, 0.75f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = theme != null ? theme.gold : new Color(0.72f, 0.66f, 0.54f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        button.colors = colors;
    }

    // ---------------- 数据刷新与购买 ----------------

    private void Refresh()
    {
        var theme = UITheme.Current;
        Color gold = theme != null ? theme.gold : new Color(0.79f, 0.66f, 0.42f, 1f);
        Color goldHi = theme != null ? theme.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
        Color muted = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
        Color bloodHi = theme != null ? theme.bloodHi : new Color(0.79f, 0.32f, 0.34f, 1f);

        if (coinsText != null)
        {
            coinsText.text = "累计金币  " + MetaProgress.TotalCoins;
            ApplyFont(coinsText, false);
        }

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
            ApplyFont(row.priceText, false);
            row.priceText.color = maxed ? muted : (affordable ? gold : bloodHi);

            row.buyLabel.text = maxed ? "MAX" : "BUY";
            row.buyLabel.color = affordable ? goldHi : muted;
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
        var theme = UITheme.Current;
        messageText.text = text;
        ApplyFont(messageText, false);
        messageText.color = success
            ? (theme != null ? theme.tealHi : new Color(0.56f, 0.75f, 0.72f, 1f))
            : (theme != null ? theme.bloodHi : new Color(0.79f, 0.32f, 0.34f, 1f));
    }

    // 文案变化后按内容重选字体：中文 → CJK 子集，英文/数字 → BodyFont
    private static void ApplyFont(Text text, bool display)
    {
        var theme = UITheme.Current;
        if (theme != null) text.font = theme.ResolveFont(text.text, display);
    }

    // ---------------- 通用 UI 工具 ----------------

    // 居中铺在父节点上，size 为宽高；pivot 居中
    private static void SetCentered(RectTransform rt, float width, float height, Vector2 anchoredPos)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = anchoredPos;
    }
}
