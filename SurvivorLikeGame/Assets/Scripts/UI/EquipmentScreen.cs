using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RpgGame
{
    // 装备与背包面板：I 开关，Esc 关闭。
    // 运行时自建 Canvas（常驻 DontDestroyOnLoad），不往场景里放节点。
    // 视觉统一走 UITheme / UIFactory；数据只通过 EquipmentService 读写。
    public class EquipmentScreen : MonoBehaviour
    {
        const int CanvasSortingOrder = 310;
        const float PanelWidth = 880f;
        const float PanelHeight = 660f;
        const int MaxInventoryRows = 7;

        public static bool IsOpen { get; private set; }

        static EquipmentScreen instance;

        GameObject canvasRoot;
        RectTransform slotRoot;
        RectTransform inventoryRoot;
        Text statsText;
        EventSystem ownedEventSystem;
        float previousTimeScale = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            IsOpen = false;
        }

        // 场景加载前就把常驻监听器建好，这样 I 键在任何区域都能打开。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;

            var go = new GameObject("[EquipmentScreen]");
            go.AddComponent<EquipmentScreen>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            BuildUI();
            canvasRoot.SetActive(false);
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                if (IsOpen) RestoreTimeScale();
                IsOpen = false;
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 切区域/进出幸存者局时强制收起，避免 UI 和时间缩放残留。
            // 临时 EventSystem 随旧场景一起销毁，这里只清引用。
            ownedEventSystem = null;
            if (IsOpen) Close();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.iKey.wasPressedThisFrame && CanOpen())
            {
                Toggle();
                return;
            }

            if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
                Close();
        }

        static bool CanOpen()
            => RegionRuntime.IsAction || RegionRuntime.IsSurvivor;

        public static void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        public static void Show()
        {
            if (!Application.isPlaying || !CanOpen()) return;
            if (instance == null) Bootstrap();
            if (instance != null) instance.Open();
        }

        public static void Hide()
        {
            if (instance != null) instance.Close();
            else IsOpen = false;
        }

        void Open()
        {
            if (IsOpen) return;

            EnsureEventSystem();

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            canvasRoot.SetActive(true);
            IsOpen = true;
            Refresh();
        }

        void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            canvasRoot.SetActive(false);
            ReleaseEventSystem();
            RestoreTimeScale();
        }

        // 动作区场景没有 EventSystem，按钮会点不动。打开面板时按需补一个，
        // 关闭时释放；场景已有（例如 SampleScene / MainMenu）则直接复用。
        void EnsureEventSystem()
        {
            if (ownedEventSystem != null) return;
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ownedEventSystem = go.GetComponent<EventSystem>();
        }

        void ReleaseEventSystem()
        {
            if (ownedEventSystem != null)
                Destroy(ownedEventSystem.gameObject);
            ownedEventSystem = null;
        }

        void RestoreTimeScale()
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        }

        // ---------------- UI 搭建 ----------------

        void BuildUI()
        {
            canvasRoot = new GameObject("Canvas", typeof(RectTransform));
            canvasRoot.transform.SetParent(transform, false);

            var canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            var scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasRoot.AddComponent<GraphicRaycaster>();

            var theme = UITheme.Current;
            Color gold = theme != null ? theme.gold : new Color(0.79f, 0.66f, 0.42f, 1f);
            Color goldHi = theme != null ? theme.goldHi : new Color(0.91f, 0.83f, 0.63f, 1f);
            Color parchment = theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f);
            Color muted = theme != null ? theme.muted : new Color(0.56f, 0.55f, 0.50f, 1f);
            Color borderColor = theme != null
                ? theme.WithAlpha(theme.gold, 0.65f)
                : new Color(0.30f, 0.25f, 0.20f, 0.92f);

            var scrim = UIFactory.CreateImage(canvasRoot.transform, "Scrim", null,
                new Color(0f, 0f, 0f, 0.55f), false, true);
            UIFactory.Stretch(scrim.rectTransform);

            var border = UIFactory.CreateImage(canvasRoot.transform, "PanelBorder",
                theme != null ? theme.frameSprite : null, borderColor, true, false);
            SetCentered(border.rectTransform, PanelWidth + 30f, PanelHeight + 30f, Vector2.zero);

            var panel = UIFactory.CreatePanel(border.transform, "Panel");
            SetCentered(panel.rectTransform, PanelWidth, PanelHeight, Vector2.zero);
            panel.raycastTarget = true;

            var title = UIFactory.CreateText(panel.transform, "Title", "装备与背包", 44,
                goldHi, TextAnchor.MiddleLeft, true, FontStyle.Bold);
            SetCentered(title.rectTransform, 500f, 60f,
                new Vector2(-PanelWidth * 0.5f + 40f + 250f, PanelHeight * 0.5f - 56f));

            var close = UIFactory.CreateButton(panel.transform, "Close", "关闭 (I)",
                new Vector2(160f, 48f), Close);
            SetCentered(close.GetComponent<RectTransform>(), 160f, 48f,
                new Vector2(PanelWidth * 0.5f - 40f - 80f, PanelHeight * 0.5f - 56f));

            var divider = UIFactory.CreateImage(panel.transform, "Divider", null,
                theme != null ? theme.WithAlpha(gold, 0.55f) : new Color(0.42f, 0.36f, 0.28f, 0.9f),
                false, false);
            SetCentered(divider.rectTransform, PanelWidth - 80f, 2f,
                new Vector2(0f, PanelHeight * 0.5f - 100f));

            var slotTitle = UIFactory.CreateText(panel.transform, "SlotTitle", "已装备", 26,
                gold, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            SetCentered(slotTitle.rectTransform, 300f, 36f,
                new Vector2(-PanelWidth * 0.5f + 40f + 150f, PanelHeight * 0.5f - 138f));

            slotRoot = CreateEmptyRect(panel.transform, "Slots",
                new Vector2(320f, 400f), new Vector2(-250f, -10f));

            var inventoryTitle = UIFactory.CreateText(panel.transform, "InventoryTitle", "背包", 26,
                gold, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            SetCentered(inventoryTitle.rectTransform, 460f, 36f,
                new Vector2(150f, PanelHeight * 0.5f - 138f));

            inventoryRoot = CreateEmptyRect(panel.transform, "Inventory",
                new Vector2(520f, 460f), new Vector2(150f, -10f));

            statsText = UIFactory.CreateText(panel.transform, "Stats", string.Empty, 24,
                parchment, TextAnchor.MiddleLeft, false, FontStyle.Normal);
            SetCentered(statsText.rectTransform, 560f, 40f,
                new Vector2(-PanelWidth * 0.5f + 40f + 280f, -PanelHeight * 0.5f + 44f));

            var hint = UIFactory.CreateText(panel.transform, "Hint", "I / ESC 关闭", 22,
                muted, TextAnchor.MiddleRight, false, FontStyle.Normal);
            SetCentered(hint.rectTransform, 320f, 40f,
                new Vector2(PanelWidth * 0.5f - 40f - 160f, -PanelHeight * 0.5f + 44f));
        }

        static RectTransform CreateEmptyRect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        static void SetCentered(RectTransform rect, float width, float height, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = position;
        }

        // ---------------- 刷新 ----------------

        void Refresh()
        {
            RefreshSlots();
            RefreshInventory();
            RefreshStats();
        }

        void RefreshSlots()
        {
            ClearChildren(slotRoot);

            var slots = (EquipSlot[])System.Enum.GetValues(typeof(EquipSlot));
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var item = EquipmentService.GetEquipped(slot);
                string label = EquipSlotText.Of(slot) + "\n" + (item != null ? item.displayName : "（空）");

                var captured = slot;
                var button = UIFactory.CreateButton(slotRoot, "Slot_" + slot, label,
                    new Vector2(300f, 64f), () => Unequip(captured));

                SetCentered(button.GetComponent<RectTransform>(), 300f, 64f,
                    new Vector2(0f, 150f - i * 78f));

                var text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.fontSize = 24;
                    text.alignment = TextAnchor.MiddleLeft;
                    text.rectTransform.offsetMin = new Vector2(18f, 0f);
                    text.rectTransform.offsetMax = new Vector2(-12f, 0f);
                }
            }
        }

        void RefreshInventory()
        {
            ClearChildren(inventoryRoot);

            var items = EquipmentService.OwnedItems();
            if (items.Count == 0)
            {
                var empty = UIFactory.CreateText(inventoryRoot, "Empty",
                    "背包是空的\n打开宝箱可以获得装备", 24,
                    UITheme.Current != null ? UITheme.Current.muted : new Color(0.56f, 0.55f, 0.50f, 1f),
                    TextAnchor.MiddleCenter, false, FontStyle.Normal);
                SetCentered(empty.rectTransform, 460f, 100f, Vector2.zero);
                return;
            }

            int count = Mathf.Min(items.Count, MaxInventoryRows);
            for (int i = 0; i < count; i++)
            {
                var item = items[i];
                bool equipped = EquipmentService.IsEquipped(item.id);
                string label = (equipped ? "● " : "") + item.displayName +
                               "  [" + EquipSlotText.Of(item.slot) + "]  " +
                               item.BuildStatSummary();

                var captured = item;
                var button = UIFactory.CreateButton(inventoryRoot, "Item_" + item.id, label,
                    new Vector2(500f, 54f), () => Equip(captured));

                SetCentered(button.GetComponent<RectTransform>(), 500f, 54f,
                    new Vector2(0f, 190f - i * 62f));

                var text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.fontSize = 21;
                    text.alignment = TextAnchor.MiddleLeft;
                    text.rectTransform.offsetMin = new Vector2(18f, 0f);
                    text.rectTransform.offsetMax = new Vector2(-12f, 0f);
                }
            }

            if (items.Count > MaxInventoryRows)
            {
                var more = UIFactory.CreateText(inventoryRoot, "More",
                    "还有 " + (items.Count - MaxInventoryRows) + " 件装备", 20,
                    UITheme.Current != null ? UITheme.Current.muted : new Color(0.56f, 0.55f, 0.50f, 1f),
                    TextAnchor.MiddleCenter, false, FontStyle.Normal);
                SetCentered(more.rectTransform, 460f, 32f,
                    new Vector2(0f, 190f - MaxInventoryRows * 62f));
            }
        }

        void RefreshStats()
        {
            var stats = CharacterStats.Current;
            statsText.text = "攻击 " + stats.attack +
                             "    防御 " + stats.defense +
                             "    生命 " + stats.maxHealth +
                             "    移速 " + stats.moveSpeed.ToString("0.0");
        }

        void Equip(EquipItemData item)
        {
            if (item == null) return;
            EquipmentService.Equip(item.id);
            Refresh();
        }

        void Unequip(EquipSlot slot)
        {
            EquipmentService.Unequip(slot);
            Refresh();
        }

        static void ClearChildren(RectTransform root)
        {
            if (root == null) return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
    }
}
