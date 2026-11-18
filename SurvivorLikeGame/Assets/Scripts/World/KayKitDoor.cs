using UnityEngine;

namespace RpgGame
{
    // 侧室门沿用附近 E 交互；打开后保持可通行，状态只写入本区域。
    public sealed class KayKitDoor : Interactable
    {
        public string doorId = "store_room_door";
        public Transform doorLeaf;
        public BoxCollider2D passageBlocker;
        public float openAngle = -100f;
        public float openDuration = 0.45f;

        public bool IsOpen { get; private set; }
        public override bool CanInteract => !IsOpen;
        public override string Prompt => "打开侧室门";

        Quaternion closedRotation;
        float progress;

        void Start()
        {
            closedRotation = doorLeaf != null ? doorLeaf.localRotation : Quaternion.identity;
            var root = FindFirstObjectByType<RegionRoot>();
            if (root != null)
                IsOpen = SaveSystem.Current.world.GetRegion(root.regionId).HasFlag(doorId);
            progress = IsOpen ? 1f : 0f;
            Apply();
        }

        public override void Interact()
        {
            if (IsOpen) return;
            IsOpen = true;
            var root = FindFirstObjectByType<RegionRoot>();
            if (root != null)
            {
                SaveSystem.Current.world.GetRegion(root.regionId).SetFlag(doorId);
                SaveSystem.Save();
            }
            // 门板尚在动画中时仍阻挡；动画结束后再允许通过。
            Debug.Log("[KayKit] 侧室门已打开");
        }

        void Update()
        {
            if (!IsOpen || progress >= 1f) return;
            progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime / Mathf.Max(0.05f, openDuration));
            Apply();
        }

        void Apply()
        {
            if (doorLeaf != null)
                doorLeaf.localRotation = closedRotation * Quaternion.Euler(0f, openAngle * Mathf.SmoothStep(0f, 1f, progress), 0f);
            if (passageBlocker != null) passageBlocker.enabled = progress < 1f;
        }
    }
}
