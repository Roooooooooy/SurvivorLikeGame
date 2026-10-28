using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 难度选择界面：运行时自建 Canvas（ScreenSpaceOverlay，sortingOrder 320），
// 不往场景里放任何对象，也不依赖 prefab。
// 难度列表来自 DifficultySelection.All，选择结果由 DifficultySelection.Select 持久化；
// 解锁走 MetaProgress.TrySpendCoins + Unlock("difficulty_<id>")，与角色/商店共用同一套存档。
// 开关由 GameManager 的标题页输入统一调度（D 打开，D / ESC 关闭），并与永久商店、选角界面互斥。
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
    private static Font cachedFont;
    private static Sprite cachedWhiteSprite;

    // 难度界面是否打开：GameManager 用它判断是否吞掉「任意键开始」
    public static bool IsOpen { get; private set; }

    // 一条难度记录的 UI 引用；数据只读，刷新时重新向 DifficultySelection / MetaProgress 取值
    private sealed class DifficultyRow
    {
        public DifficultyData data;
        public Image background;
        public Image accent;
        public Text nameText;
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

        var font = ResolveFont();

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
            TextAnchor.MiddleLeft).text = "选择难度";

        // 累计金币（右上，靠内）
        coinsText = CreateText("Coins", panel.transform, font, 26, new Vector2(360f, 48f),
            new Vector2(120f, halfHeight - 58f), FontStyle.Bold, new Color(0.90f, 0.78f, 0.45f, 1f),
            TextAnchor.MiddleRight);

        // 关闭按钮（最右上角）
        CreateCloseButton(panel.transform, font, new Vector2(halfWidth - 100f, halfHeight - 58f));

        // 标题下的分隔线
        var divider = CreateImage("Divider", panel.transform, new Color(0.42f, 0.36f, 0.28f, 0.9f));
        SetCentered(divider.rectTransform, PanelWidth - 80f, 2f, new Vector2(0f, halfHeight - 100f));

        // 难度行：从分隔线下方开始逐行排列
        float firstRowY = halfHeight - HeaderHeight - rowHeight * 0.5f;
        for (int i = 0; i < rowCount; i++)
            CreateRow(panel.transform, font, difficulties[i], i, firstRowY - i * rowHeight, rowHeight);

        // 空状态：A 的难度数据尚未就绪时给出明确提示，不崩、不留白板
        var emptyTitle = CreateText("Empty", panel.transform, font, 30, new Vector2(PanelWidth - 160f, 44f),
            new Vector2(0f, 6f), FontStyle.Bold, new Color(0.72f, 0.69f, 0.64f, 0.9f), TextAnchor.MiddleCenter);
        emptyTitle.text = "暂无可用难度";
        emptyTitle.gameObject.SetActive(rowCount == 0);

        var emptyHint = CreateText("EmptyHint", panel.transform, font, 18, new Vector2(PanelWidth - 160f, 36f),
            new Vector2(0f, -34f), FontStyle.Normal, new Color(0.60f, 0.57f, 0.53f, 0.85f), TextAnchor.MiddleCenter);
        emptyHint.text = "难度数据尚未就绪";
        emptyHint.gameObject.SetActive(rowCount == 0);

        // 底部：左侧固定操作提示，右侧显示解锁结果
        CreateText("KeyHint", panel.transform, font, 22, new Vector2(420f, 40f),
            new Vector2(-halfWidth + 40f + 210f, -halfHeight + 38f), FontStyle.Normal,
            new Color(0.86f, 0.83f, 0.78f, 0.7f), TextAnchor.MiddleLeft).text = "D / ESC 关闭";

        messageText = CreateText("Message", panel.transform, font, 22, new Vector2(460f, 40f),
            new Vector2(halfWidth - 40f - 230f, -halfHeight + 38f), FontStyle.Bold,
            new Color(0.86f, 0.83f, 0.78f, 0.9f), TextAnchor.MiddleRight);
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

    private void CreateRow(Transform parent, Font font, DifficultyData data, int index, float y, float rowHeight)
    {
        float height = Mathf.Max(64f, rowHeight - 8f);

        // 交替底色，便于区分相邻行；选中态在 Refresh 里覆盖
        var bg = CreateImage("Row_" + (data != null ? data.id : index.ToString()), parent,
            index % 2 == 0
                ? new Color(0.115f, 0.105f, 0.095f, 0.9f)
                : new Color(0.095f, 0.088f, 0.08f, 0.9f));
        SetCentered(bg.rectTransform, RowWidth, height, new Vector2(0f, y));

        var row = new DifficultyRow { data = data, background = bg };

        // 左侧竖条：选中时变亮金色，作为高亮的第二条视觉线索
        row.accent = CreateImage("Accent", bg.transform, new Color(0.30f, 0.25f, 0.20f, 0.9f));
        SetCentered(row.accent.rectTransform, 6f, height, new Vector2(-RowWidth * 0.5f + 3f, 0f));

        // 名字（左上）
        row.nameText = CreateText("Name", bg.transform, font, 28, new Vector2(460f, 38f),
            new Vector2(-210f, height * 0.5f - 30f), FontStyle.Bold,
            new Color(0.96f, 0.93f, 0.86f, 1f), TextAnchor.MiddleLeft);
        row.nameText.text = DisplayName(data);

        // 描述（中左）：最多两行，超出截断
        var desc = CreateText("Desc", bg.transform, font, 15, new Vector2(470f, 40f),
            new Vector2(-210f, 6f), FontStyle.Normal, new Color(0.72f, 0.69f, 0.64f, 0.85f),
            TextAnchor.MiddleLeft);
        desc.text = data != null && !string.IsNullOrEmpty(data.description) ? data.description : "—";
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        desc.verticalOverflow = VerticalWrapMode.Truncate;

        // 倍率（左下）：敌人 HP / 伤害 / 速度 / 金币，单行展示
        CreateText("Multipliers", bg.transform, font, 15, new Vector2(520f, 32f),
            new Vector2(-210f, -height * 0.5f + 22f), FontStyle.Normal,
            new Color(0.62f, 0.72f, 0.62f, 0.95f), TextAnchor.MiddleLeft).text = MultiplierLine(data);

        // 状态（右上）：未解锁显示价格，已解锁显示当前状态
        row.statusText = CreateText("Status", bg.transform, font, 18, new Vector2(260f, 34f),
            new Vector2(265f, height * 0.5f - 30f), FontStyle.Bold,
            new Color(0.86f, 0.83f, 0.78f, 0.95f), TextAnchor.MiddleCenter);

        // 操作按钮（右下）：UNLOCK / SELECT / 已选择
        row.actionButton = CreateActionButton(bg.transform, font, new Vector2(ActionButtonX, -height * 0.5f + 38f), out row.actionLabel);
        var captured = row;
        row.actionButton.onClick.AddListener(() => OnActionClicked(captured));

        rows.Add(row);
    }

    private Button CreateActionButton(Transform parent, Font font, Vector2 anchoredPos, out Text label)
    {
        var go = new GameObject("ActionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        SetCentered(rt, 180f, 54f, anchoredPos);

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
        // 已选中的难度按钮不可点，用暖金色代替灰掉，视觉上表示「当前选择」
        colors.disabledColor = new Color(0.32f, 0.26f, 0.14f, 0.95f);
        button.colors = colors;

        label = CreateText("Text", rt, font, 21, new Vector2(170f, 48f), Vector2.zero,
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

    // ---------------- 数据刷新、解锁与选择 ----------------

    private void Refresh()
    {
        if (coinsText != null) coinsText.text = "累计金币  " + MetaProgress.TotalCoins;

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

            if (!unlocked)
            {
                row.statusText.text = "需要 " + cost + " 金币";
                row.statusText.color = affordable
                    ? new Color(0.90f, 0.78f, 0.45f, 1f)
                    : new Color(0.78f, 0.42f, 0.36f, 1f);
                row.actionLabel.text = "UNLOCK";
                row.actionLabel.color = affordable
                    ? new Color(0.96f, 0.93f, 0.86f, 1f)
                    : new Color(0.62f, 0.59f, 0.54f, 0.85f);
                row.actionButton.interactable = true; // 金币不足也允许点击，由提示条反馈原因
            }
            else
            {
                row.statusText.text = isSelected ? "当前难度" : "已解锁";
                row.statusText.color = isSelected
                    ? new Color(0.90f, 0.78f, 0.45f, 1f)
                    : new Color(0.60f, 0.58f, 0.54f, 0.9f);
                row.actionLabel.text = isSelected ? "已选择" : "SELECT";
                row.actionLabel.color = isSelected
                    ? new Color(0.96f, 0.90f, 0.72f, 1f)
                    : new Color(0.96f, 0.93f, 0.86f, 1f);
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
        messageText.text = text;
        messageText.color = success
            ? new Color(0.55f, 0.85f, 0.55f, 1f)
            : new Color(0.90f, 0.45f, 0.40f, 1f);
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

    // 文字样式：与 MetaShopScreen / CharacterSelectScreen 相同（描边 + 投影），居中锚点
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
        cachedWhiteSprite.name = "DifficultySelectWhite";
        return cachedWhiteSprite;
    }
}
