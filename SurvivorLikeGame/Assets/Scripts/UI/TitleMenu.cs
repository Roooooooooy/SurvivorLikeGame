using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 标题页主菜单：W/S 或上下方向键选择，Enter / 手柄 A 确认；鼠标悬停与点击同样可用。
// Time.timeScale = 0 时全程使用 unscaled 时间；打开商店/角色/难度浮层时不接收输入。
public class TitleMenu : MonoBehaviour
{
    public sealed class Definition
    {
        public string label;
        public string icon;
        public Action action;
    }

    public sealed class Entry
    {
        public Button button;
        public Image background;
        public Image accent;
        public Image icon;
        public Text labelText;
        public Action action;
        [NonSerialized] public float bgAlpha;
    }

    public Entry[] entries;
    public Text summaryText;
    public TitleBackdrop backdrop;

    int selected;
    int lastCoins = int.MinValue;
    string lastCharacter = "";
    string lastDifficulty = "";
    float summaryTimer;

    // 构建完成后由 TitleScreenV2 调用：选中第一项并立即套用最终视觉，避免首帧闪烁
    public void ResetSelection()
    {
        selected = 0;
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
                ApplyImmediate(entries[i], i == selected);
        }
        if (backdrop != null) backdrop.SetMood(selected);
        RefreshSummary(true);
    }

    void Update()
    {
        if (!CanInteract()) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
            else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Activate(selected);
        }

        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.dpad.down.wasPressedThisFrame) Move(1);
            else if (gp.dpad.up.wasPressedThisFrame) Move(-1);
            else if (gp.buttonSouth.wasPressedThisFrame) Activate(selected);
        }

        Animate();
        // PlayerPrefs 读取有一定开销，状态行每 0.25 秒刷新一次即可
        summaryTimer -= Time.unscaledDeltaTime;
        if (summaryTimer <= 0f)
        {
            summaryTimer = 0.25f;
            RefreshSummary(false);
        }
    }

    // 只在标题页且没有浮层时接管输入，避免和 GameManager 的快捷键互相打架
    bool CanInteract()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.titleScreen == null || !gm.titleScreen.activeInHierarchy) return false;
        return !(CharacterSelectScreen.IsOpen || MetaShopScreen.IsOpen || DifficultySelectScreen.IsOpen);
    }

    public void SetSelected(int index)
    {
        if (entries == null || index < 0 || index >= entries.Length) return;
        ChangeSelection(index);
    }

    public void Move(int delta)
    {
        if (entries == null || entries.Length == 0) return;
        ChangeSelection((selected + delta + entries.Length) % entries.Length);
    }

    public void Activate(int index)
    {
        if (entries == null || index < 0 || index >= entries.Length) return;
        ChangeSelection(index);

        var action = entries[index].action;
        if (action != null) action();
    }

    // 选中变化时同步切换动态背景的情绪（颜色/速度/特效权重）
    void ChangeSelection(int index)
    {
        if (index == selected) return;
        selected = index;
        if (backdrop != null) backdrop.SetMood(selected);
    }

    void Animate()
    {
        var theme = UITheme.Current;
        if (theme == null || entries == null) return;

        float t = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            bool sel = i == selected;

            e.bgAlpha = Mathf.Lerp(e.bgAlpha, sel ? 1f : 0f, t);
            SetAlpha(e.background, e.bgAlpha * 0.92f);

            e.labelText.color = Color.Lerp(e.labelText.color,
                sel ? theme.goldHi : theme.WithAlpha(theme.parchment, 0.82f), t);
            e.accent.color = Color.Lerp(e.accent.color,
                theme.WithAlpha(theme.gold, sel ? 1f : 0f), t);
            e.icon.color = Color.Lerp(e.icon.color,
                sel ? theme.goldHi : theme.WithAlpha(theme.parchment, 0.52f), t);

            float scale = Mathf.Lerp(e.labelText.rectTransform.localScale.x, sel ? 1.04f : 1f, t);
            e.labelText.rectTransform.localScale = Vector3.one * scale;
        }
    }

    void ApplyImmediate(Entry e, bool sel)
    {
        var theme = UITheme.Current;
        if (e == null || theme == null) return;

        e.bgAlpha = sel ? 1f : 0f;
        SetAlpha(e.background, e.bgAlpha * 0.92f);
        e.labelText.color = sel ? theme.goldHi : theme.WithAlpha(theme.parchment, 0.82f);
        e.accent.color = theme.WithAlpha(theme.gold, sel ? 1f : 0f);
        e.icon.color = sel ? theme.goldHi : theme.WithAlpha(theme.parchment, 0.52f);
        e.labelText.rectTransform.localScale = Vector3.one * (sel ? 1.04f : 1f);
    }

    static void SetAlpha(Image image, float alpha)
    {
        if (image == null) return;
        var c = image.color;
        c.a = alpha;
        image.color = c;
    }

    // 菜单下方的一行局外状态：角色 / 难度 / 金币；值变化时才重建字符串
    void RefreshSummary(bool force)
    {
        if (summaryText == null) return;

        int coins = MetaProgress.TotalCoins;
        var character = CharacterSelection.Selected;
        var difficulty = DifficultySelection.Selected;
        string characterName = character != null && !string.IsNullOrEmpty(character.displayName)
            ? character.displayName : "—";
        string difficultyName = difficulty != null && !string.IsNullOrEmpty(difficulty.displayName)
            ? difficulty.displayName : "—";

        if (!force && coins == lastCoins && characterName == lastCharacter &&
            difficultyName == lastDifficulty) return;

        lastCoins = coins;
        lastCharacter = characterName;
        lastDifficulty = difficultyName;
        summaryText.text = string.Format("角色  {0}      难度  {1}      金币  {2}",
            characterName, difficultyName, coins);
    }

    // 供 TitleScreenV2 绑定鼠标悬停
    public void BindPointerHover(int index)
    {
        var e = entries != null && index >= 0 && index < entries.Length ? entries[index] : null;
        if (e == null || e.button == null) return;

        var trigger = e.button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = e.button.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => SetSelected(index));
        trigger.triggers.Add(entry);
    }
}
