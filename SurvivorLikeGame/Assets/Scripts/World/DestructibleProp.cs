using UnityEngine;

namespace RpgGame
{
    // 完整物体参与现有 2D 命中与阻挡，裂片只在 3D 画面中播放。
    [DisallowMultipleComponent, RequireComponent(typeof(Collider2D))]
    public sealed class DestructibleProp : MonoBehaviour, IDamageable
    {
        public string propId;
        public int maxHealth = 20;
        public GameObject intactVisual;
        public GameObject greyboxVisual;
        public GameObject fracturedPrefab;
        public bool modulesVisible = true;

        public int Health { get; private set; }
        public bool IsDead { get; private set; }
        public PropFractureEffect FractureEffect { get; private set; }

        string regionId;

        void Awake()
        {
            Health = Mathf.Max(1, maxHealth);
        }

        void Start()
        {
            var root = FindFirstObjectByType<RegionRoot>();
            regionId = root != null ? root.regionId : null;
            if (!string.IsNullOrEmpty(regionId) && !string.IsNullOrEmpty(propId) &&
                SaveSystem.Current.world.GetRegion(regionId).HasFlag(StateFlag))
            {
                IsDead = true; Health = 0;
                HideAndUnblock();
            }
        }

        string StateFlag => "destroyed_prop/" + propId;

        public void SetPreviewMode(bool showModules)
        {
            modulesVisible = showModules;
            if (intactVisual != null) intactVisual.SetActive(showModules && !IsDead && FractureEffect == null);
            if (greyboxVisual != null) greyboxVisual.SetActive(!showModules && !IsDead);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || info.amount <= 0) return;
            Health = Mathf.Max(0, Health - info.amount);
            Vector2 direction = (Vector2)transform.position - info.sourcePosition;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            EnsureFractureEffect();
            if (Health > 0)
            {
                if (FractureEffect != null) FractureEffect.ShowCracks(1f - Health / (float)maxHealth, direction.normalized);
                return;
            }
            IsDead = true;
            HideAndUnblock();
            if (FractureEffect != null) FractureEffect.BreakApart(direction.normalized);
            if (!string.IsNullOrEmpty(regionId) && !string.IsNullOrEmpty(propId))
            {
                SaveSystem.Current.world.GetRegion(regionId).SetFlag(StateFlag);
                SaveSystem.Save();
            }
            Debug.Log("[DestructibleProp] 已破坏 " + propId);
        }

        void EnsureFractureEffect()
        {
            if (FractureEffect != null || fracturedPrefab == null || intactVisual == null || !modulesVisible) return;
            var source = intactVisual.transform;
            var root = Instantiate(fracturedPrefab, source.position, source.rotation);
            root.transform.localScale = source.lossyScale;
            FractureEffect = root.GetComponent<PropFractureEffect>();
            intactVisual.SetActive(false);
        }

        void HideAndUnblock()
        {
            if (intactVisual != null) intactVisual.SetActive(false);
            if (greyboxVisual != null) greyboxVisual.SetActive(false);
            foreach (var blocker in GetComponentsInChildren<Collider2D>(true)) blocker.enabled = false;
        }

        void OnDestroy()
        {
            // 场景重载会自动清理碎片；单个完整物体被删除时也不遗留裂片对象。
            if (FractureEffect != null && !IsDead) Destroy(FractureEffect.gameObject);
        }
    }
}
