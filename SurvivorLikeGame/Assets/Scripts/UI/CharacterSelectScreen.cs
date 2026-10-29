using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 角色选择界面：运行时自建 Canvas（ScreenSpaceOverlay，sortingOrder 310），
// 不往场景里放任何对象，也不依赖 prefab。
// 角色列表来自 CharacterSelection.All，选择结果由 CharacterSelection.Select 持久化。
// 开关由 GameManager 的标题页输入统一调度（C 打开，C / ESC 关闭），并与永久商店互斥。
// 视觉统一走 UITheme / UIFactory：金色标题、分隔线、暗色九宫格面板与主题按钮。
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

    // 选角界面是否打开：GameManager 用它判断是否吞掉「任意键开始」
    public static bool IsOpen { get; private set; }

    // 一张角色卡片的 UI 引用；数据只读，刷新时重新向 CharacterSelection 取选中/解锁状态
    private sealed class CharacterRow
    {
        public CharacterData data;
        public Image background;
        public Image accent;
        public Text nameText;
        public Text descText;
        public Text passiveText;
        public Text lockText;        // 锁定态价格提示
        public Button selectButton;
        public Text selectLabel;
        public Button unlockButton;  // 仅锁定态显示
        public Text unlockLabel;
    }

    private readonly List<CharacterRow> rows = new List<CharacterRow>();
    private CharacterData[] characters = new CharacterData[0];
    private Text currentText;
    private Text coinsText;          // 底部右侧：累计金币余额
    private Text messageText;        // 底部右侧：解锁/失败提示
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

        var theme = UITheme.Current;
        Color gold = theme != null ? theme.gold : new Color(0.79f, 0.66f, 0.42f, 1f);
        Color goldHi = theme != null ? theme.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
        Color parchment = theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
        Color muted = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
        Color borderColor = theme != null ? theme.WithAlpha(theme.gold, 0.65f) : new Color(0.30f, 0.25f, 0.20f, 0.92f);

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
        var title = UIFactory.CreateText(panel.transform, "Title", "选择角色", 46,
            goldHi, TextAnchor.MiddleLeft, true, FontStyle.Bold);
        SetCentered(title.rectTransform, 360f, 64f, new Vector2(-300f, halfHeight - 58f));

        // 当前选中角色（右上，靠内，语义色 gold）
        currentText = UIFactory.CreateText(panel.transform, "Current", string.Empty, 26,
            gold, TextAnchor.MiddleRight, false, FontStyle.Bold);
        SetCentered(currentText.rectTransform, 360f, 48f, new Vector2(150f, halfHeight - 58f));

        // 关闭按钮（最右上角）
        CreateCloseButton(panel.transform, new Vector2(halfWidth - 100f, halfHeight - 58f));

        // 标题下的分隔线
        var divider = UIFactory.CreateImage(panel.transform, "Divider", null,
            theme != null ? theme.WithAlpha(gold, 0.55f) : new Color(0.42f, 0.36f, 0.28f, 0.9f),
            false, false);
        SetCentered(divider.rectTransform, PanelWidth - 80f, 2f, new Vector2(0f, halfHeight - 100f));

        // 角色卡片：从分隔线下方开始逐张排列
        float firstRowY = halfHeight - HeaderHeight - rowHeight * 0.5f;
        for (int i = 0; i < rowCount; i++)
            CreateRow(panel.transform, characters[i], i, firstRowY - i * rowHeight, rowHeight);

        // 空状态：A 的角色数据尚未就绪时给出明确提示，不崩、不留白板
        var emptyTitle = UIFactory.CreateText(panel.transform, "Empty", "暂无可用角色", 30,
            muted, TextAnchor.MiddleCenter, false, FontStyle.Bold);
        SetCentered(emptyTitle.rectTransform, PanelWidth - 160f, 44f, new Vector2(0f, 6f));
        emptyTitle.gameObject.SetActive(rowCount == 0);

        var emptyHint = UIFactory.CreateText(panel.transform, "EmptyHint", "角色数据尚未就绪", 18,
            muted, TextAnchor.MiddleCenter, false, FontStyle.Normal);
        SetCentered(emptyHint.rectTransform, PanelWidth - 160f, 36f, new Vector2(0f, -34f));
        emptyHint.gameObject.SetActive(rowCount == 0);

        // 底部操作提示
        var keyHint = UIFactory.CreateText(panel.transform, "KeyHint", "C / ESC 关闭", 22,
            muted, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        SetCentered(keyHint.rectTransform, 420f, 40f,
            new Vector2(-halfWidth + 40f + 210f, -halfHeight + 38f));

        // 底部右侧：累计金币余额（解锁角色时扣费）与一行操作结果提示
        coinsText = UIFactory.CreateText(panel.transform, "Coins", string.Empty, 22,
            gold, TextAnchor.MiddleRight, false, FontStyle.Bold);
        SetCentered(coinsText.rectTransform, 440f, 36f,
            new Vector2(halfWidth - 40f - 220f, -halfHeight + 66f));

        messageText = UIFactory.CreateText(panel.transform, "Message", string.Empty, 20,
            parchment, TextAnchor.MiddleRight, false, FontStyle.Normal);
        SetCentered(messageText.rectTransform, 440f, 32f,
            new Vector2(halfWidth - 40f - 220f, -halfHeight + 30f));
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

    private void CreateRow(Transform parent, CharacterData data, int index, float y, float rowHeight)
    {
        var theme = UITheme.Current;
        float height = Mathf.Max(60f, rowHeight - 8f);
        Color parchment = theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
        Color muted = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
        Color teal = theme != null ? theme.teal : new Color(0.37f, 0.56f, 0.54f, 1f);
        Color gold = theme != null ? theme.gold : new Color(0.79f, 0.66f, 0.42f, 1f);
        Color bloodHi = theme != null ? theme.bloodHi : new Color(0.79f, 0.32f, 0.34f, 1f);

        // 交替底色，便于区分相邻卡片；选中态在 Refresh 里覆盖
        var bg = UIFactory.CreateImage(parent, "Row_" + (data != null ? data.id : index.ToString()),
            null,
            index % 2 == 0
                ? (theme != null ? theme.panel2 : new Color(0.08f, 0.11f, 0.15f, 1f))
                : (theme != null ? theme.panel : new Color(0.06f, 0.08f, 0.11f, 1f)),
            false, false);
        SetCentered(bg.rectTransform, RowWidth, height, new Vector2(0f, y));

        var row = new CharacterRow { data = data, background = bg };

        // 左侧竖条：选中时变亮金色，作为高亮的第二条视觉线索
        row.accent = UIFactory.CreateImage(bg.transform, "Accent", null,
            theme != null ? theme.line : new Color(0.15f, 0.19f, 0.24f, 1f), false, false);
        SetCentered(row.accent.rectTransform, 6f, height, new Vector2(-RowWidth * 0.5f + 3f, 0f));

        // 左侧图标：以初始武器作为角色标识
        RowIcon.Add(bg.transform, "Icon", RowIcon.ForCharacter(data),
            new Vector2(-RowWidth * 0.5f + 52f, 0f), 64f);

        // 名字（左上）
        row.nameText = UIFactory.CreateText(bg.transform, "Name",
            data != null
                ? (!string.IsNullOrEmpty(data.displayName) ? data.displayName
                    : (!string.IsNullOrEmpty(data.id) ? data.id : "未命名角色"))
                : "(未命名)", 30, parchment, TextAnchor.MiddleLeft, false, FontStyle.Bold);
        SetCentered(row.nameText.rectTransform, 360f, 40f, new Vector2(-190f, height * 0.5f - 38f));

        // 描述（中左）：最多两行，超出截断
        row.descText = UIFactory.CreateText(bg.transform, "Desc",
            data != null && !string.IsNullOrEmpty(data.description) ? data.description : "—",
            16, muted, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        SetCentered(row.descText.rectTransform, 380f, 48f, new Vector2(-190f, 4f));
        row.descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        row.descText.verticalOverflow = VerticalWrapMode.Truncate;

        // 被动（左下）：单行，超出截断；语义色 teal
        row.passiveText = UIFactory.CreateText(bg.transform, "Passive",
            "被动  " + (data != null && !string.IsNullOrEmpty(data.passiveDescription)
                ? data.passiveDescription : "—"),
            16, teal, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        SetCentered(row.passiveText.rectTransform, 380f, 36f, new Vector2(-190f, -42f));
        row.passiveText.horizontalOverflow = HorizontalWrapMode.Wrap;
        row.passiveText.verticalOverflow = VerticalWrapMode.Truncate;

        // 初始武器（右中，语义色 gold）
        var weapon = UIFactory.CreateText(bg.transform, "Weapon",
            "初始武器  " + (data != null ? WeaponName(data.initialWeapon) : "—"),
            18, gold, TextAnchor.MiddleRight, false, FontStyle.Normal);
        SetCentered(weapon.rectTransform, 300f, 36f, new Vector2(215f, height * 0.5f - 38f));

        // 解锁态提示（右中）：仅锁定角色显示，文案含金币价格
        row.lockText = UIFactory.CreateText(bg.transform, "Lock", string.Empty, 18,
            bloodHi, TextAnchor.MiddleRight, false, FontStyle.Bold);
        SetCentered(row.lockText.rectTransform, 300f, 32f, new Vector2(320f, 6f));

        // UNLOCK 按钮（SELECT 左侧）：仅锁定角色显示
        row.unlockButton = CreateUnlockButton(bg.transform, new Vector2(95f, -42f), out row.unlockLabel);

        // SELECT 按钮（右下）
        row.selectButton = CreateSelectButton(bg.transform, new Vector2(280f, -42f), out row.selectLabel);

        var captured = row;
        row.selectButton.onClick.AddListener(() => OnSelectClicked(captured));
        row.unlockButton.onClick.AddListener(() => OnUnlockClicked(captured));

        rows.Add(row);
    }

    private Button CreateSelectButton(Transform parent, Vector2 anchoredPos, out Text label)
    {
        var button = UIFactory.CreateButton(parent, "SelectButton", "SELECT",
            new Vector2(180f, 56f), null);
        SetCentered(button.GetComponent<RectTransform>(), 180f, 56f, anchoredPos);
        StyleButton(button);

        label = button.transform.Find("Label").GetComponent<Text>();
        label.fontSize = 22;
        return button;
    }

    // 解锁按钮：与 SELECT 同尺寸，用暗金色区分「消费金币」的操作
    private Button CreateUnlockButton(Transform parent, Vector2 anchoredPos, out Text label)
    {
        var button = UIFactory.CreateButton(parent, "UnlockButton", "UNLOCK",
            new Vector2(170f, 56f), null);
        SetCentered(button.GetComponent<RectTransform>(), 170f, 56f, anchoredPos);
        StyleButton(button);

        label = button.transform.Find("Label").GetComponent<Text>();
        label.fontSize = 20;
        label.color = UITheme.Current != null ? UITheme.Current.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
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

    // ---------------- 数据刷新与选择 ----------------

    private void Refresh()
    {
        var theme = UITheme.Current;
        Color gold = theme != null ? theme.gold : new Color(0.79f, 0.66f, 0.42f, 1f);
        Color goldHi = theme != null ? theme.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
        Color parchment = theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
        Color muted = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
        Color teal = theme != null ? theme.teal : new Color(0.37f, 0.56f, 0.54f, 1f);
        Color tealHi = theme != null ? theme.tealHi : new Color(0.56f, 0.75f, 0.72f, 1f);
        Color blood = theme != null ? theme.blood : new Color(0.55f, 0.18f, 0.20f, 1f);
        Color bloodHi = theme != null ? theme.bloodHi : new Color(0.79f, 0.32f, 0.34f, 1f);
        Color panel = theme != null ? theme.panel : new Color(0.06f, 0.08f, 0.11f, 1f);
        Color panel2 = theme != null ? theme.panel2 : new Color(0.08f, 0.11f, 0.15f, 1f);

        var selected = CharacterSelection.Selected;
        string selectedId = CurrentSelectedId();
        int coins = MetaProgress.TotalCoins;

        if (currentText != null)
        {
            currentText.text = selected != null ? "当前角色  " + DisplayName(selected) : "当前角色  —";
            ApplyFont(currentText);
        }
        if (coinsText != null)
        {
            coinsText.text = "累计金币  " + coins;
            ApplyFont(coinsText);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.data == null) continue;

            bool unlocked = CharacterSelection.IsUnlocked(row.data);
            // 锁定角色永远不会被高亮为当前选中，即使存档里残留着它的 id
            bool isSelected = unlocked && !string.IsNullOrEmpty(selectedId) && row.data.id == selectedId;

            // 选中卡片：金色基调 + 金色左侧竖条 + 金色名字；锁定卡片竖条转 blood
            row.background.color = isSelected
                ? Color.Lerp(panel2, gold, 0.28f)
                : (i % 2 == 0 ? panel2 : panel);
            row.accent.color = isSelected
                ? gold
                : unlocked
                    ? (theme != null ? theme.line : new Color(0.15f, 0.19f, 0.24f, 1f))
                    : blood;
            row.nameText.color = isSelected ? goldHi : (unlocked ? parchment : muted);
            // 选中行的暖金底色会压低对比度，正文提到 parchment/tealHi
            row.descText.color = isSelected ? parchment : muted;
            row.passiveText.color = isSelected ? tealHi : teal;

            var buttonColors = row.selectButton.colors;
            if (!unlocked)
            {
                int cost = Mathf.Max(0, row.data.unlockCost);
                bool affordable = coins >= cost;

                // 价格提示：余额足够金色，不足转红
                row.lockText.gameObject.SetActive(true);
                row.lockText.text = "未解锁 · 需要 " + cost + " 金币";
                ApplyFont(row.lockText);
                row.lockText.color = affordable ? gold : bloodHi;

                row.unlockButton.gameObject.SetActive(true);
                row.unlockLabel.text = "UNLOCK";
                ApplyFont(row.unlockLabel);
                row.unlockLabel.color = affordable ? goldHi : muted;
                row.unlockButton.interactable = affordable; // 金币不足时不可点

                // 锁定态的 SELECT 保持可见但不可用
                row.selectButton.interactable = false;
                row.selectLabel.text = "未解锁";
                ApplyFont(row.selectLabel);
                row.selectLabel.color = muted;
                buttonColors.disabledColor = Color.Lerp(panel, panel2, 0.6f);
            }
            else
            {
                row.lockText.gameObject.SetActive(false);
                row.unlockButton.gameObject.SetActive(false);

                row.selectLabel.text = isSelected ? "已选择" : "SELECT";
                ApplyFont(row.selectLabel);
                row.selectLabel.color = isSelected ? goldHi : parchment;
                row.selectButton.interactable = !isSelected;
                // 已选中时禁用色用暖金，与锁定态的灰色区分
                buttonColors.disabledColor = Color.Lerp(panel, gold, 0.38f);
            }
            row.selectButton.colors = buttonColors;
        }
    }

    private void OnSelectClicked(CharacterRow row)
    {
        if (row == null || row.data == null) return;
        // 锁定角色拒绝选择：Select 本身也会拦截，这里提前返回避免刷新出误导性状态
        if (!CharacterSelection.IsUnlocked(row.data)) return;

        // 持久化由 CharacterSelection.Select 负责（PlayerPrefs + Save）
        CharacterSelection.Select(row.data.id);
        Refresh();
    }

    // 解锁购买：先扣金币，成功后再写解锁标记；任一步失败都不改变解锁状态
    private void OnUnlockClicked(CharacterRow row)
    {
        if (row == null || row.data == null) return;
        if (CharacterSelection.IsUnlocked(row.data))
        {
            Refresh();
            return;
        }

        int cost = Mathf.Max(0, row.data.unlockCost);
        if (MetaProgress.TrySpendCoins(cost))
        {
            MetaProgress.Unlock(CharacterSelection.UnlockKey(row.data.id));
            ShowMessage("已解锁：" + DisplayName(row.data), true);
        }
        else
        {
            ShowMessage("金币不足，还差 " + (cost - MetaProgress.TotalCoins) + " 金币", false);
        }

        Refresh(); // 成功时同步余额与锁定态；失败时也刷新，保持与存档一致
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
