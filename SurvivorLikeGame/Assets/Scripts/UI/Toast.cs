using UnityEngine;
using UnityEngine.UI;

namespace RpgGame
{
    // 屏幕底部的轻量提示：获得装备、拾取等。
    // 运行时自建 Canvas，不依赖场景节点；使用 unscaledTime，暂停时也能正常淡出。
    public class Toast : MonoBehaviour
    {
        const int CanvasSortingOrder = 400;

        static Toast instance;

        CanvasGroup group;
        Text label;
        float remaining;
        float duration;

        public static void Show(string message, float seconds = 2.6f)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(message)) return;

            if (instance == null)
            {
                var go = new GameObject("ToastCanvas", typeof(RectTransform));
                instance = go.AddComponent<Toast>();
                instance.Build();
            }

            instance.Present(message, seconds);
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var panel = UIFactory.CreatePanel(transform, "ToastPanel");
            panel.raycastTarget = false;
            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(660f, 76f);
            rect.anchoredPosition = new Vector2(0f, 110f);

            var theme = UITheme.Current;
            label = UIFactory.CreateText(panel.transform, "Label", string.Empty, 26,
                theme != null ? theme.parchment : new Color(0.85f, 0.82f, 0.76f, 1f),
                TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(20f, 8f);
            label.rectTransform.offsetMax = new Vector2(-20f, -8f);
        }

        void Present(string message, float seconds)
        {
            label.text = message;
            duration = Mathf.Max(0.6f, seconds);
            remaining = duration;
            group.alpha = 1f;
        }

        void Update()
        {
            if (remaining <= 0f) return;

            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                group.alpha = 0f;
                return;
            }

            // 最后 0.4 秒淡出。
            group.alpha = Mathf.Clamp01(remaining / 0.4f);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }
    }
}
