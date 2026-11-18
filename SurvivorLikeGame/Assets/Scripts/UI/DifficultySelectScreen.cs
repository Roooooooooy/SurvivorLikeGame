using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
    private const float CollapsedRowHeight = 84f;      // 未选中：只显示图标 / 名称 / 基础信息
    private const float ExpandedRowHeight = 200f;      // 选中：展开描述与操作按钮
    private const float RowGap = 8f;

    private static DifficultySelectScreen instance;

    // 难度界面是否打开：GameManager 用它判断是否吞掉「任意键开始」
    public static bool IsOpen { get; private set; }

    // 一条难度记录的 UI 引用；数据只读，刷新时重新向 DifficultySelection / MetaProgress 取值
    private sealed class DifficultyRow
    {
        public DifficultyData data;
        public int index;
        public RectTransform rect;
        public Image background;
        public Image accent;
        public Text nameText;
        public Text descText;
        public Text infoText;
        public Text statusText;
        public Button actionButton;
        public Text actionLabel;
        public CanvasGroup detailGroup;
        public float currentHeight;
        public float currentY;
        public float detailAlpha;
    }

    private readonly List<DifficultyRow> rows = new List<DifficultyRow>();
    private DifficultyData[] difficulties = new DifficultyData[0];
    private Text coinsText;
    private Text messageText;
    private bool built;

    // 键盘/鼠标当前聚焦的行（可以是被锁定的难度，用于查看）
    private int focusedIndex;
    private float collapsedHeight = CollapsedRowHeight;
    private float expandedHeight = ExpandedRowHeight;
    private float rowTopY;

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

        // 关闭难度界面后，背景恢复成标题菜单「选择难度」的配色
        if (TitleBackdrop.Instance != null) TitleBackdrop.Instance.ClearDifficultyTint();
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

        // 每次打开都从“当前已选难度”开始聚焦
        focusedIndex = FindSelectedIndex();
        Refresh();
        IsOpen = true;
    }

    private void Update()
    {
        if (!IsOpen || !built) return;

        HandleInput();
        LayoutRows(Time.unscaledDeltaTime, false);
    }

    // 上下键切换聚焦行（锁定的难度也可以查看，只是不能设置）
    private void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) MoveFocus(1);
            else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) MoveFocus(-1);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) ConfirmFocused();
        }

        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.dpad.down.wasPressedThisFrame) MoveFocus(1);
            else if (gp.dpad.up.wasPressedThisFrame) MoveFocus(-1);
            else if (gp.buttonSouth.wasPressedThisFrame) ConfirmFocused();
        }
    }

    private void MoveFocus(int delta)
    {
        if (rows.Count == 0) return;
        SetFocus(Mathf.Clamp(focusedIndex + delta, 0, rows.Count - 1));
    }

    private void SetFocus(int index)
    {
        if (rows.Count == 0) return;
        index = Mathf.Clamp(index, 0, rows.Count - 1);
        if (index == focusedIndex) return;

        focusedIndex = index;
        Refresh();
    }

    // Enter / 手柄 A：只对已解锁难度生效；锁定难度保持展开预览，不写入选择
    private void ConfirmFocused()
    {
        if (focusedIndex < 0 || focusedIndex >= rows.Count) return;

        var row = rows[focusedIndex];
        if (row == null || row.data == null) return;

        if (!IsUnlocked(row.data))
        {
            ShowMessage("该难度尚未解锁", false);
            return;
        }

        DifficultySelection.Select(row.data.id);
        Refresh();
    }

    private int FindSelectedIndex()
    {
        string selectedId = CurrentSelectedId();
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].data != null && rows[i].data.id == selectedId) return i;
        }
        return 0;
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

        // 读取难度数据：面板高度按“一行展开 + 其余收起”的最大占位计算
        LoadDifficulties();
        int rowCount = difficulties.Length;
        collapsedHeight = CollapsedRowHeight;
        expandedHeight = ExpandedRowHeight;

        float rowsHeight = rowCount > 0
            ? collapsedHeight * Mathf.Max(0, rowCount - 1) + expandedHeight + RowGap * Mathf.Max(0, rowCount - 1)
            : 240f;
        float totalHeight = HeaderHeight + FooterHeight + rowsHeight + 16f;
        if (totalHeight > MaxPanelHeight)
        {
            float available = MaxPanelHeight - HeaderHeight - FooterHeight - 16f
                              - RowGap * Mathf.Max(0, rowCount - 1);
            float baseSum = collapsedHeight * Mathf.Max(0, rowCount - 1) + expandedHeight;
            float k = available / Mathf.Max(1f, baseSum);
            collapsedHeight *= k;
            expandedHeight *= k;
            totalHeight = MaxPanelHeight;
        }

        float panelHeight = Mathf.Max(400f, totalHeight);
        float halfHeight = panelHeight * 0.5f;
        float halfWidth = PanelWidth * 0.5f;
        rowTopY = halfHeight - HeaderHeight;

        // 全屏遮罩：压暗得比较轻，让雨雷背景能透出来；仍然吞掉鼠标点击
        var scrim = UIFactory.CreateImage(transform, "Scrim", null, new Color(0f, 0f, 0f, 0.38f),
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

        // 难度行：高度/位置由 LayoutRows 统一驱动，展开时把下面各行挤开
        for (int i = 0; i < rowCount; i++)
            CreateRow(panel.transform, difficulties[i], i);

        focusedIndex = FindSelectedIndex();
        LayoutRows(0f, true);

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
        var keyHint = UIFactory.CreateText(panel.transform, "KeyHint",
            "上下选择      ENTER 确认      D / ESC 关闭", 22,
            muted, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        SetCentered(keyHint.rectTransform, 520f, 40f,
            new Vector2(-halfWidth + 40f + 260f, -halfHeight + 38f));

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

    private void CreateRow(Transform parent, DifficultyData data, int index)
    {
        var theme = UITheme.Current;
        Color parchment = theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
        Color muted = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
        Color teal = theme != null ? theme.teal : new Color(0.37f, 0.56f, 0.54f, 1f);

        // 行底可接收鼠标：进入或点击任意一行都会把焦点切到该行并展开
        var bg = UIFactory.CreateImage(parent, "Row_" + (data != null ? data.id : index.ToString()),
            null,
            index % 2 == 0
                ? (theme != null ? theme.panel2 : new Color(0.08f, 0.11f, 0.15f, 1f))
                : (theme != null ? theme.panel : new Color(0.06f, 0.08f, 0.11f, 1f)),
            false, true);

        var bgRt = bg.rectTransform;
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(RowWidth, collapsedHeight);
        bgRt.anchoredPosition = Vector2.zero;

        var row = new DifficultyRow
        {
            data = data,
            index = index,
            rect = bgRt,
            background = bg,
            currentHeight = collapsedHeight,
            detailAlpha = 0f,
        };

        var hover = bg.gameObject.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => SetFocus(index));
        hover.triggers.Add(enter);
        var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        click.callback.AddListener(_ => SetFocus(index));
        hover.triggers.Add(click);

        // 左侧难度图标：固定贴住行的顶部，展开时与名称保持同一水平线
        CardRow.AddIconChip(bg.transform, RowIcon.ForDifficulty(data), -RowWidth * 0.5f + 56f, 72f);
        AnchorIconChipToTop(bg.transform, 56f, -44f);

        // 左侧竖条：聚焦时变亮金色
        row.accent = UIFactory.CreateImage(bg.transform, "Accent", null,
            theme != null ? theme.line : new Color(0.15f, 0.19f, 0.24f, 1f), false, false);
        SetCentered(row.accent.rectTransform, 4f, collapsedHeight,
            new Vector2(-RowWidth * 0.5f + 3f, 0f));

        // 名称（左上，收起态也显示）
        row.nameText = UIFactory.CreateText(bg.transform, "Name", DisplayName(data), 28,
            parchment, TextAnchor.MiddleLeft, false, FontStyle.Bold);
        AnchorTopLeft(row.nameText.rectTransform, 110f, -14f, 420f, 36f);

        // 基础信息（左上、名称下方，收起态也显示）
        row.infoText = UIFactory.CreateText(bg.transform, "Multipliers", MultiplierLine(data),
            15, teal, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        AnchorTopLeft(row.infoText.rectTransform, 110f, -54f, 560f, 28f);

        // 状态（右上，收起态也显示：价格 / 已解锁 / 当前难度）
        row.statusText = UIFactory.CreateText(bg.transform, "Status", string.Empty, 18,
            parchment, TextAnchor.MiddleRight, false, FontStyle.Bold);
        AnchorTopRight(row.statusText.rectTransform, -24f, -14f, 300f, 34f);

        // ---------- 展开后才出现的详情区 ----------
        var detailGo = new GameObject("Detail", typeof(RectTransform), typeof(CanvasGroup));
        var detailRt = detailGo.GetComponent<RectTransform>();
        detailRt.SetParent(bg.transform, false);
        detailRt.anchorMin = Vector2.zero;
        detailRt.anchorMax = Vector2.one;
        detailRt.offsetMin = Vector2.zero;
        detailRt.offsetMax = Vector2.zero;

        row.detailGroup = detailGo.GetComponent<CanvasGroup>();
        row.detailGroup.alpha = 0f;
        row.detailGroup.blocksRaycasts = false;
        row.detailGroup.interactable = false;

        // 描述（左上，基础信息下方，最多两行）
        row.descText = UIFactory.CreateText(detailGo.transform, "Desc",
            data != null && !string.IsNullOrEmpty(data.description) ? data.description : "—",
            15, muted, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        AnchorTopLeft(row.descText.rectTransform, 110f, -88f, 560f, 44f);
        row.descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        row.descText.verticalOverflow = VerticalWrapMode.Truncate;

        // 操作按钮（右下）：UNLOCK / SELECT / 已选择
        row.actionButton = CreateActionButton(detailGo.transform, new Vector2(-24f, 20f),
            out row.actionLabel);
        var captured = row;
        row.actionButton.onClick.AddListener(() => OnActionClicked(captured));

        rows.Add(row);
    }

    // CardRow 生成的图标底座默认按行中心定位；这里改成贴住行的左上角
    private static void AnchorIconChipToTop(Transform row, float leftX, float topY)
    {
        string[] names = { "__IconChip", "__IconChipBorder", "__Icon" };
        for (int i = 0; i < names.Length; i++)
        {
            var rt = row.Find(names[i]) as RectTransform;
            if (rt == null) continue;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(leftX, topY);
        }
    }

    private static void AnchorTopLeft(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private static void AnchorTopRight(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    // 行高/行位置动画：聚焦行展开，其余收起，下面的行被逐渐挤开
    private void LayoutRows(float dt, bool immediate)
    {
        float t = immediate ? 1f : 1f - Mathf.Exp(-11f * dt);
        float cursor = rowTopY;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            float targetHeight = i == focusedIndex ? expandedHeight : collapsedHeight;
            float targetY = cursor - targetHeight * 0.5f;

            row.currentHeight = immediate ? targetHeight : Mathf.Lerp(row.currentHeight, targetHeight, t);
            row.currentY = immediate ? targetY : Mathf.Lerp(row.currentY, targetY, t);

            row.rect.sizeDelta = new Vector2(RowWidth, row.currentHeight);
            row.rect.anchoredPosition = new Vector2(0f, row.currentY);

            if (row.accent != null)
                row.accent.rectTransform.sizeDelta = new Vector2(4f, Mathf.Max(20f, row.currentHeight - 14f));

            float targetAlpha = i == focusedIndex ? 1f : 0f;
            row.detailAlpha = immediate ? targetAlpha : Mathf.Lerp(row.detailAlpha, targetAlpha, t);
            if (row.detailGroup != null)
            {
                row.detailGroup.alpha = row.detailAlpha;
                bool interactive = row.detailAlpha > 0.85f;
                row.detailGroup.blocksRaycasts = interactive;
                row.detailGroup.interactable = interactive;
            }

            cursor -= targetHeight + RowGap;
        }
    }

    private Button CreateActionButton(Transform parent, Vector2 anchoredPos, out Text label)
    {
        var button = UIFactory.CreateButton(parent, "ActionButton", "SELECT",
            new Vector2(180f, 54f), null);

        var rt = button.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(180f, 54f);

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
            bool isFocused = i == focusedIndex;
            // 当前实际生效的难度（与“聚焦查看”区分开）
            bool isActive = unlocked && !string.IsNullOrEmpty(selectedId) && row.data.id == selectedId;

            // 展开/聚焦的行：金色基调 + 金色左侧竖条 + 金色名字
            row.background.color = isFocused
                ? Color.Lerp(panel2, gold, 0.28f)
                : (i % 2 == 0 ? panel2 : panel);
            row.accent.color = isFocused
                ? gold
                : (theme != null ? theme.line : new Color(0.15f, 0.19f, 0.24f, 1f));
            row.nameText.color = isFocused ? goldHi : parchment;
            // 聚焦行的暖金底色会压低对比度，正文提到 parchment/tealHi
            row.descText.color = isFocused ? parchment : muted;
            row.infoText.color = isFocused ? tealHi : teal;

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
                row.statusText.text = isActive ? "当前难度" : "已解锁";
                ApplyFont(row.statusText);
                row.statusText.color = isActive ? gold : muted;
                row.actionLabel.text = isActive ? "已选择" : "SELECT";
                ApplyFont(row.actionLabel);
                row.actionLabel.color = isActive ? goldHi : parchment;
                row.actionButton.interactable = !isActive;
            }
        }

        // 背景颜色跟随“聚焦查看”的难度，而不是实际选择的难度（锁定项也能预览）
        if (focusedIndex >= 0 && focusedIndex < rows.Count)
            ApplyBackdropTint(rows[focusedIndex].data);
    }

    // 难度 → 标题页雨雷背景的配色与强度。
    // 普通=青蓝小雨；困难=暖金雷雨；噩梦=深红暴雨，雨量/雷频/闪光逐级增强。
    private static void ApplyBackdropTint(DifficultyData data)
    {
        if (data == null || TitleBackdrop.Instance == null) return;

        var theme = UITheme.Current;
        if (theme == null) return;

        Color primary;
        Color secondary;
        Color accent;
        float severity;
        switch (data.id)
        {
            case "normal":
                primary = theme.tealHi;
                secondary = theme.teal;
                accent = theme.goldHi;
                severity = 0.45f;
                break;
            case "hard":
                primary = theme.gold;
                secondary = theme.bloodHi;
                accent = theme.goldHi;
                severity = 0.75f;
                break;
            case "nightmare":
                primary = theme.bloodHi;
                secondary = Color.Lerp(theme.blood, Color.black, 0.25f);
                accent = Color.Lerp(theme.goldHi, theme.bloodHi, 0.35f);
                severity = 1f;
                break;
            default:
                return;
        }

        TitleBackdrop.Instance.SetDifficultyTint(primary, secondary, accent, severity);
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
