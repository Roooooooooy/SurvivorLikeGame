using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RpgGame
{
    // 宝箱：打开后写入区域状态，重新进入区域保持已开。
    // 碰撞体是实体（非 Trigger），玩家不能穿过；交互由 WorldInteractor 的 Overlap 检测。
    public class Chest : Interactable
    {
        public string chestId = "chest_01";
        public int gold = 10;
        public int exp = 20;
        public ChestView view;

        [Header("装备掉落")]
        [Tooltip("从 EquipDatabase 里按 id 取装备；留空表示只给金币/经验")]
        public string[] lootItemIds = new string[0];
        [Range(0f, 1f)] public float dropChance = 1f;

        bool opened;

        void Awake()
        {
            // 旧场景保存时还没有 lootItemIds 字段，Unity 不会自动套用字段初始化器。
            // Phase A 只在测试宝箱 chest_01 上兜底掉落巫师帽；正式内容请在 Inspector 配置。
            if ((lootItemIds == null || lootItemIds.Length == 0) && chestId == "chest_01")
                lootItemIds = new[] { "gear_wizard_hat" };
        }

        public override bool CanInteract => !opened;
        public override string Prompt => opened ? "Opened" : "Open Chest";

        void Start()
        {
            bool alreadyOpened = false;

            if (SaveSystem.Current != null)
            {
                var world = SaveSystem.Current.world;
                var root = FindFirstObjectByType<RegionRoot>();
                string regionId = root != null ? root.regionId : world.regionId;
                alreadyOpened = world.GetRegion(regionId).openedChests.Contains(chestId);
            }

            opened = alreadyOpened;

            if (view != null)
            {
                if (alreadyOpened) view.SetOpened(true);
                else view.Close();
            }
        }

        public override void Interact()
        {
            if (opened) return;

            var granted = new List<EquipItemData>();
            int duplicateGold = 0;

            if (SaveSystem.Current != null)
            {
                var world = SaveSystem.Current.world;
                var root = FindFirstObjectByType<RegionRoot>();
                string regionId = root != null ? root.regionId : world.regionId;
                var state = world.GetRegion(regionId);

                if (!state.openedChests.Contains(chestId))
                    state.openedChests.Add(chestId);

                var character = SaveSystem.Current.character;
                if (character != null)
                {
                    character.gold += Mathf.Max(0, gold);
                    character.AddExp(exp);
                    CollectLoot(granted, ref duplicateGold);
                    character.gold += duplicateGold;
                }

                SaveSystem.Save();
            }

            opened = true;
            if (view != null) view.Open();

            StartCoroutine(PlayLootCollect(granted));

            Debug.Log("[Chest] 打开 " + chestId + "，获得 " + gold + " 金币 / " + exp + " 经验" +
                      (granted.Count > 0 ? " / 装备 " + granted.Count + " 件" : ""));
        }

        // 先写入背包，保证中途离开区域也不会丢掉落；表现上再播收集动画。
        void CollectLoot(List<EquipItemData> granted, ref int duplicateGold)
        {
            if (lootItemIds == null || lootItemIds.Length == 0) return;

            for (int i = 0; i < lootItemIds.Length; i++)
            {
                string itemId = lootItemIds[i];
                if (string.IsNullOrEmpty(itemId)) continue;
                if (dropChance < 1f && Random.value > dropChance) continue;

                var item = EquipDatabase.Find(itemId);
                if (EquipmentService.Grant(itemId, false))
                {
                    if (item != null) granted.Add(item);
                }
                else if (item != null)
                {
                    // 已拥有同一件装备时折算金币，避免宝箱白开。
                    duplicateGold += Mathf.Max(1, item.goldValue);
                }
            }
        }

        IEnumerator PlayLootCollect(List<EquipItemData> granted)
        {
            if (granted == null || granted.Count == 0) yield break;

            Vector3 origin = transform.position + Vector3.up * 0.9f;

            for (int i = 0; i < granted.Count; i++)
            {
                float offset = (i - (granted.Count - 1) * 0.5f) * 0.35f;
                LootCollectEffect.Spawn(granted[i], origin + new Vector3(offset, 0f, 0f));
                yield return new WaitForSeconds(0.16f);
            }

            // 让消散动画先播完，再把结果提示出来。
            yield return new WaitForSeconds(0.5f);

            var names = new List<string>();
            for (int i = 0; i < granted.Count; i++)
                names.Add(granted[i].displayName);

            Toast.Show("获得装备：" + string.Join("、", names) + "\n按 I 打开装备面板");
        }

        // 调试用：把本区域宝箱恢复成未打开，方便反复测试开箱表现。
        public void ResetOpened()
        {
            opened = false;
            if (view != null) view.Close();
        }

        public static void ResetAllOpened()
        {
            var chests = FindObjectsByType<Chest>(FindObjectsSortMode.None);
            for (int i = 0; i < chests.Length; i++)
                chests[i].ResetOpened();

            if (SaveSystem.Current != null)
            {
                var world = SaveSystem.Current.world;
                var root = FindFirstObjectByType<RegionRoot>();
                string regionId = root != null ? root.regionId : world.regionId;
                world.GetRegion(regionId).openedChests.Clear();
                SaveSystem.Save();
            }

            Debug.Log("[Chest] 已重置本区域宝箱（调试）");
        }
    }
}
