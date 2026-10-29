using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 难度选择界面：运行时自建 Canvas（ScreenSpaceOverlay，sortingOrder 320），
// 不往场景里放任何对象，也不依赖 prefab。
// 难度列表来自 DifficultySelection.All，选择结果由 DifficultySelection.Select 持久化；
// 解锁走 MetaProgress.TrySpendCoins + Unlock("difficulty_<id>")，与角色/商店共用同一套存档。
// 开关由 GameManager 的标题页输入统一调度（D 打开，D / ESC 关闭），并与永久商店、选角界面互斥。
// 视觉统一走 UITheme / UIFactory：金色标题、分隔线、暗色九宫格面板与主题按钮。
public class DifficultySelectScreen : MonoBehaviour
{
    private const int CanvasSortingOrder = 320;        // 高于选角界面(310)，难度界面压在最上层
    private const string UnlockKeyPrefix = "difficulty_";
    private const float PanelWidth = 1040f;
    private const float MaxPanelHeight = 940f;         // 1080 参考高度下留出上下边距
    private const float HeaderHeight = 130f;
    private const float FooterHeight = 90f;
    private const float RowWidth = 960f;
    private const float DesiredRowHeight = 160f;
    private const float MinRowHeight = 104f;
    private const float ActionButtonX = 360f;          // 操作按钮横向位置：行右侧留 30px 内边距

    private static DifficultySelectScreen instance;

    // 难度界面是否打开：GameManager 用它判断是否吞掉「任意键开始」
    public static bool IsOpen { get; private set; }

    // 一条难度记录的 UI 引用；数据只读，刷新时重新向 DifficultySelection / MetaProgress 取值
    private sealed class DifficultyRow
    {
        public DifficultyData data;
        public Image background;
        public Image accent;
        public Text nameText;
        public Text descText;
        public Text infoText;
        public Text statusText;
        public Button actionButton;
        public Text actionLabel;
    }

    private readonly List<DifficultyRow> rows = new List<DifficultyRow>();
    private DifficultyData[] difficulties = new DifficultyData[0];
    private Text coinsText;
    private Text messageText;
    private bool built;

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
        screen.EnsureBuilt();
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

    // 懒构建：首次打开时才读取难度列表，避免 Awake 阶段 A 的数据尚未就绪
    private void EnsureBuilt()
    {
        if (built) return;
        built = true;
        BuildUI();
    }

    // 懒加载：跟随当前场景创建（不做 DontDestroyOnLoad），重开场景时界面一起销毁
    private static DifficultySelectScreen CreateInstance()
    {
        var go = new GameObject("DifficultySelectCanvas", typeof(RectTransform));
        return go.AddComponent<DifficultySelectScreen>();
    }

    private void Open()
    {
        // 三个浮层互斥：同一时间只允许一个浮层
        MetaShopScreen.Hide();
        CharacterSelectScreen.Hide();
        if (messageText != null) messageText.text = string.Empty; // 每次打开清掉上一次的解锁提示
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
            Destroy(gameObject); // 保证同一时间只有一套难度界面
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

        // 与 MetaShopScreen / CharacterSelectScreen 一致的 1920x1080 参考分辨率
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // SELECT / UNLOCK / 关闭按钮需要点击事件（事件系统由场景提供）
        gameObject.AddComponent<GraphicRaycaster>();

        var theme = UITheme.Current;
        Color gold = theme != null ? theme.gold : new Color(0.79f, 0.66f, 0.42f, 1f);
        Color goldHi = theme != null ? theme.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
        Color parchment = theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
        Color muted = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
        Color borderColor = theme != null ? theme.WithAlpha(theme.gold, 0.65f) : new Color(0.30f, 0.25f, 0.20f, 0.92f);

        // 读取难度数据并按行数计算面板高度：条目多时自动压缩行高，避免溢出屏幕
        LoadDifficulties();
        int rowCount = difficulties.Length;
        float rowHeight = DesiredRowHeight;
        float needed = HeaderHeight + FooterHeight + rowCount * rowHeight;
        if (needed > MaxPanelHeight)
            rowHeight = Mathf.Max(MinRowHeight, (MaxPanelHeight - HeaderHeight - FooterHeight) / Mathf.Max(1, rowCount));
        float panelHeight = Mathf.Clamp(HeaderHeight + FooterHeight + rowCount * rowHeight, 400f, MaxPanelHeight);
        float halfHeight = panelHeight * 0.5f;
        float halfWidth = PanelWidth * 0.5f;

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
        var title = UIFactory.CreateText(panel.transform, "Title", "选择难度", 46,
            goldHi, TextAnchor.MiddleLeft, true, FontStyle.Bold);
        SetCentered(title.rectTransform, 360f, 64f, new Vector2(-300f, halfHeight - 58f));

        // 累计金币（右上，靠内，语义色 gold）
        coinsText = UIFactory.CreateText(panel.transform, "Coins", string.Empty, 26,
            gold, TextAnchor.MiddleRight, false, FontStyle.Bold);
        SetCentered(coinsText.rectTransform, 360f, 48f, new Vector2(120f, halfHeight - 58f));

        // 关闭按钮（最右上角）
        CreateCloseButton(panel.transform, new Vector2(halfWidth - 100f, halfHeight - 58f));

        // 标题下的分隔线
        var divider = UIFactory.CreateImage(panel.transform, "Divider", null,
            theme != null ? theme.WithAlpha(gold, 0.55f) : new Color(0.42f, 0.36f, 0.28f, 0.9f),
            false, false);
        SetCentered(divider.rectTransform, PanelWidth - 80f, 2f, new Vector2(0f, halfHeight - 100f));

        // 难度行：从分隔线下方开始逐行排列
        float firstRowY = halfHeight - HeaderHeight - rowHeight * 0.5f;
        for (int i = 0; i < rowCount; i++)
            CreateRow(panel.transform, difficulties[i], i, firstRowY - i * rowHeight, rowHeight);

        // 空状态：A 的难度数据尚未就绪时给出明确提示，不崩、不留白板
        var emptyTitle = UIFactory.CreateText(panel.transform, "Empty", "暂无可用难度", 30,
            muted, TextAnchor.MiddleCenter, false, FontStyle.Bold);
        SetCentered(emptyTitle.rectTransform, PanelWidth - 160f, 44f, new Vector2(0f, 6f));
        emptyTitle.gameObject.SetActive(rowCount == 0);

        var emptyHint = UIFactory.CreateText(panel.transform, "EmptyHint", "难度数据尚未就绪", 18,
            muted, TextAnchor.MiddleCenter, false, FontStyle.Normal);
        SetCentered(emptyHint.rectTransform, PanelWidth - 160f, 36f, new Vector2(0f, -34f));
        emptyHint.gameObject.SetActive(rowCount == 0);

        // 底部：左侧固定操作提示，右侧显示解锁结果
        var keyHint = UIFactory.CreateText(panel.transform, "KeyHint", "D / ESC 关闭", 22,
            muted, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        SetCentered(keyHint.rectTransform, 420f, 40f,
            new Vector2(-halfWidth + 40f + 210f, -halfHeight + 38f));

        messageText = UIFactory.CreateText(panel.transform, "Message", string.Empty, 22,
            parchment, TextAnchor.MiddleRight, false, FontStyle.Bold);
        SetCentered(messageText.rectTransform, 460f, 40f,
            new Vector2(halfWidth - 40f - 230f, -halfHeight + 38f));
    }

    // 读 DifficultySelection.All；空数据是合法状态（UI 走空态兜底）
    private void LoadDifficulties()
    {
        var all = DifficultySelection.All;
        var list = new List<DifficultyData>(all != null ? all.Length : 0);
        if (all != null)
        {
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) list.Add(all[i]);
        }
        difficulties = list.ToArray();
    }

    private void CreateRow(Transform parent, DifficultyData data, int index, float y, float rowHeight)
    {
        var theme = UITheme.Current;
        float height = Mathf.Max(64f, rowHeight - 8f);
        Color parchment = theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
        Color muted = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
        Color teal = theme != null ? theme.teal : new Color(0.37f, 0.56f, 0.54f, 1f);

        // 交替底色，便于区分相邻行；选中态在 Refresh 里覆盖
        var bg = UIFactory.CreateImage(parent, "Row_" + (data != null ? data.id : index.ToString()),
            null,
            index % 2 == 0
                ? (theme != null ? theme.panel2 : new Color(0.08f, 0.11f, 0.15f, 1f))
                : (theme != null ? theme.panel : new Color(0.06f, 0.08f, 0.11f, 1f)),
            false, false);
        SetCentered(bg.rectTransform, RowWidth, height, new Vector2(0f, y));

        var row = new DifficultyRow { data = data, background = bg };

        // 左侧难度图标
        RowIcon.Add(bg.transform, "Icon", RowIcon.ForDifficulty(data),
            new Vector2(-RowWidth * 0.5f + 52f, 0f), 56f);

        // 左侧竖条：选中时变亮金色，作为高亮的第二条视觉线索
        row.accent = UIFactory.CreateImage(bg.transform, "Accent", null,
            theme != null ? theme.line : new Color(0.15f, 0.19f, 0.24f, 1f), false, false);
        SetCentered(row.accent.rectTransform, 6f, height, new Vector2(-RowWidth * 0.5f + 3f, 0f));

        // 名字（左上）
        row.nameText = UIFactory.CreateText(bg.transform, "Name", DisplayName(data), 28,
            parchment, TextAnchor.MiddleLeft, false, FontStyle.Bold);
        SetCentered(row.nameText.rectTransform, 380f, 38f, new Vector2(-160f, height * 0.5f - 30f));

        // 描述（中左）：最多两行，超出截断
        row.descText = UIFactory.CreateText(bg.transform, "Desc",
            data != null && !string.IsNullOrEmpty(data.description) ? data.description : "—",
            15, muted, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        SetCentered(row.descText.rectTransform, 400f, 40f, new Vector2(-160f, 6f));
        row.descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        row.descText.verticalOverflow = VerticalWrapMode.Truncate;

        // 倍率（左下）：敌人 HP / 伤害 / 速度 / 金币，单行展示；语义色 teal
        row.infoText = UIFactory.CreateText(bg.transform, "Multipliers", MultiplierLine(data),
            15, teal, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        SetCentered(row.infoText.rectTransform, 440f, 32f,
            new Vector2(-160f, -height * 0.5f + 22f));

        // 状态（右上）：未解锁显示价格，已解锁显示当前状态
        row.statusText = UIFactory.CreateText(bg.transform, "Status", string.Empty, 18,
            parchment, TextAnchor.MiddleCenter, false, FontStyle.Bold);
        SetCentered(row.statusText.rectTransform, 260f, 34f,
            new Vector2(265f, height * 0.5f - 30f));

        // 操作按钮（右下）：UNLOCK / SELECT / 已选择
        row.actionButton = CreateActionButton(bg.transform,
            new Vector2(ActionButtonX, -height * 0.5f + 38f), out row.actionLabel);
        var captured = row;
        row.actionButton.onClick.AddListener(() => OnActionClicked(captured));

        rows.Add(row);
    }

    private Button CreateActionButton(Transform parent, Vector2 anchoredPos, out Text label)
    {
        var button = UIFactory.CreateButton(parent, "ActionButton", "SELECT",
            new Vector2(180f, 54f), null);
        SetCentered(button.GetComponent<RectTransform>(), 180f, 54f, anchoredPos);
        StyleButton(button);

        label = button.transform.Find("Label").GetComponent<Text>();
        label.fontSize = 21;
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
        label.color = UITheme.Current != null ? UITheme.Current.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
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

    // ---------------- 数据刷新、解锁与选择 ----------------

    private void Refresh()
    {
        var theme = UITheme.Current;
        Color gold = theme != null ? theme.gold : new Color(0.79f, 0.66f, 0.42f, 1f);
        Color goldHi = theme != null ? theme.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
        Color parchment = theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
        Color muted = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
        Color teal = theme != null ? theme.teal : new Color(0.37f, 0.56f, 0.54f, 1f);
        Color tealHi = theme != null ? theme.tealHi : new Color(0.56f, 0.75f, 0.72f, 1f);
        Color bloodHi = theme != null ? theme.bloodHi : new Color(0.79f, 0.32f, 0.34f, 1f);
        Color panel = theme != null ? theme.panel : new Color(0.06f, 0.08f, 0.11f, 1f);
        Color panel2 = theme != null ? theme.panel2 : new Color(0.08f, 0.11f, 0.15f, 1f);

        if (coinsText != null)
        {
            coinsText.text = "累计金币  " + MetaProgress.TotalCoins;
            ApplyFont(coinsText);
        }

        int coins = MetaProgress.TotalCoins;
        string selectedId = CurrentSelectedId();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.data == null) continue;

            bool unlocked = IsUnlocked(row.data);
            int cost = Mathf.Max(0, row.data.unlockCost);
            bool affordable = coins >= cost;
            // 只有已解锁的难度才可能被选中，避免 A 的兜底 Selected 指向锁定项时误高亮
            bool isSelected = unlocked && !string.IsNullOrEmpty(selectedId) && row.data.id == selectedId;

            // 选中卡片：金色基调 + 金色左侧竖条 + 金色名字
            row.background.color = isSelected
                ? Color.Lerp(panel2, gold, 0.28f)
                : (i % 2 == 0 ? panel2 : panel);
            row.accent.color = isSelected
                ? gold
                : (theme != null ? theme.line : new Color(0.15f, 0.19f, 0.24f, 1f));
            row.nameText.color = isSelected ? goldHi : parchment;
            // 选中行的暖金底色会压低对比度，正文提到 parchment/tealHi
            row.descText.color = isSelected ? parchment : muted;
            row.infoText.color = isSelected ? tealHi : teal;

            // 已选中时按钮不可点，用暖金色代替灰掉，视觉上表示「当前选择」
            var buttonColors = row.actionButton.colors;
            buttonColors.disabledColor = Color.Lerp(panel, gold, 0.38f);
            row.actionButton.colors = buttonColors;

            if (!unlocked)
            {
                row.statusText.text = "需要 " + cost + " 金币";
                ApplyFont(row.statusText);
                row.statusText.color = affordable ? gold : bloodHi;
                row.actionLabel.text = "UNLOCK";
                ApplyFont(row.actionLabel);
                row.actionLabel.color = affordable ? goldHi : muted;
                row.actionButton.interactable = true; // 金币不足也允许点击，由提示条反馈原因
            }
            else
            {
                row.statusText.text = isSelected ? "当前难度" : "已解锁";
                ApplyFont(row.statusText);
                row.statusText.color = isSelected ? gold : muted;
                row.actionLabel.text = isSelected ? "已选择" : "SELECT";
                ApplyFont(row.actionLabel);
                row.actionLabel.color = isSelected ? goldHi : parchment;
                row.actionButton.interactable = !isSelected;
            }
        }
    }

    private void OnActionClicked(DifficultyRow row)
    {
        if (row == null || row.data == null) return;
        if (string.IsNullOrEmpty(row.data.id))
        {
            ShowMessage("难度数据缺失", false);
            return;
        }

        if (!IsUnlocked(row.data))
        {
            Unlock(row.data);
            return;
        }

        // 持久化由 DifficultySelection.Select 负责（PlayerPrefs + Save）
        DifficultySelection.Select(row.data.id);
        Refresh();
    }

    private void Unlock(DifficultyData data)
    {
        int cost = Mathf.Max(0, data.unlockCost);
        // 先扣金币再写解锁键：扣款失败时不产生任何副作用
        if (!MetaProgress.TrySpendCoins(cost))
        {
            ShowMessage("金币不足", false);
            Refresh(); // 同步一次金币显示，避免与存档不一致
            return;
        }

        MetaProgress.Unlock(UnlockKeyPrefix + data.id);
        ShowMessage("解锁成功：" + DisplayName(data), true);
        Refresh();
    }

    private void ShowMessage(string text, bool success)
    {
        if (messageText == null) return;
        var theme = UITheme.Current;
        messageText.text = text;
        ApplyFont(messageText);
        messageText.color = success
            ? (theme != null ? theme.tealHi : new Color(0.56f, 0.75f, 0.72f, 1f))
            : (theme != null ? theme.bloodHi : new Color(0.79f, 0.32f, 0.34f, 1f));
    }

    // 文案变化后按内容重选字体：中文 → CJK 子集，英文/数字 → BodyFont
    private static void ApplyFont(Text text)
    {
        var theme = UITheme.Current;
        if (theme != null) text.font = theme.ResolveFont(text.text, false);
    }

    // 已解锁判定统一走契约；数据为空视为未解锁，避免空引用
    private static bool IsUnlocked(DifficultyData data)
        => data != null && DifficultySelection.IsUnlocked(data);

    // SelectedId 在未存档时可能回落到第一项，正好用于默认高亮；仍为空再退回 Selected.id
    private static string CurrentSelectedId()
    {
        string id = DifficultySelection.SelectedId;
        if (!string.IsNullOrEmpty(id)) return id;
        var selected = DifficultySelection.Selected;
        return selected != null ? selected.id : "";
    }

    private static string DisplayName(DifficultyData data)
    {
        if (data == null) return "(未命名)";
        if (!string.IsNullOrEmpty(data.displayName)) return data.displayName;
        return !string.IsNullOrEmpty(data.id) ? data.id : "未命名难度";
    }

    // 倍率行：保留两位小数并去掉多余的 0，读起来更干净
    private static string MultiplierLine(DifficultyData data)
    {
        if (data == null) return "—";
        return "敌人 HP ×" + FormatMultiplier(data.enemyHealthMultiplier)
            + "   伤害 ×" + FormatMultiplier(data.enemyDamageMultiplier)
            + "   速度 ×" + FormatMultiplier(data.enemySpeedMultiplier)
            + "   金币 ×" + FormatMultiplier(data.coinMultiplier);
    }

    private static string FormatMultiplier(float value)
        => value.ToString("0.##");

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
