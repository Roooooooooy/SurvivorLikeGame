using UnityEngine;

namespace RpgGame
{
    // 宝箱表现：开盖、内部金光、金币。逻辑状态由 Chest 负责，这里只做视觉。
    public class ChestView : MonoBehaviour
    {
        public Transform lidHinge;
        public float openAngle = 105f;
        public float openDuration = 0.42f;

        public Light glowLight;
        public float closedLightIntensity;
        public float openLightIntensity = 1.6f;

        public GameObject lootRoot;

        bool open;
        float progress;

        void Awake()
        {
            Apply();
        }

        public void SetOpened(bool instant)
        {
            open = true;
            if (instant) progress = 1f;
            Apply();
        }

        public void Open()
        {
            open = true;
        }

        public void Close()
        {
            open = false;
            progress = 0f;
            Apply();
        }

        void Update()
        {
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(progress, target)) return;

            float speed = 1f / Mathf.Max(0.05f, openDuration);
            progress = Mathf.MoveTowards(progress, target, speed * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));

            if (lidHinge != null)
                lidHinge.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, openAngle, e));

            if (glowLight != null)
                glowLight.intensity = Mathf.Lerp(closedLightIntensity, openLightIntensity, e);

            if (lootRoot != null)
                lootRoot.SetActive(progress > 0.35f);
        }
    }
}
