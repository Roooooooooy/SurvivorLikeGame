using System;
using System.Collections.Generic;
using UnityEngine;

namespace RpgGame
{
    // 全部装备资产的只读索引。第一次访问时从 Resources/Equipment 加载。
    // 排序固定为 槽位 → id，保证背包和面板每次打开顺序一致。
    public static class EquipDatabase
    {
        const string ResourceRoot = "Equipment";

        static EquipItemData[] all;
        static Dictionary<string, EquipItemData> byId;

        public static EquipItemData[] All
        {
            get
            {
                EnsureLoaded();
                return all;
            }
        }

        public static EquipItemData Find(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            EnsureLoaded();
            return byId.TryGetValue(itemId, out var item) ? item : null;
        }

        // 编辑器里增删资源后调用，避免一直用旧缓存。
        public static void Reload()
        {
            all = null;
            byId = null;
        }

        static void EnsureLoaded()
        {
            if (all != null) return;

            var loaded = Resources.LoadAll<EquipItemData>(ResourceRoot);
            var list = new List<EquipItemData>(loaded != null ? loaded.Length : 0);
            byId = new Dictionary<string, EquipItemData>(StringComparer.Ordinal);

            if (loaded != null)
            {
                for (int i = 0; i < loaded.Length; i++)
                {
                    var item = loaded[i];
                    if (item == null || string.IsNullOrEmpty(item.id)) continue;

                    if (byId.ContainsKey(item.id))
                    {
                        Debug.LogWarning("[Equip] 重复的装备 id，已忽略后一个：" + item.id);
                        continue;
                    }

                    byId.Add(item.id, item);
                    list.Add(item);
                }
            }

            list.Sort(Compare);
            all = list.ToArray();
        }

        static int Compare(EquipItemData a, EquipItemData b)
        {
            int c = a.slot.CompareTo(b.slot);
            if (c != 0) return c;
            return string.CompareOrdinal(a.id, b.id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            all = null;
            byId = null;
        }
    }
}
