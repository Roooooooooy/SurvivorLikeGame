using System;
using System.Collections.Generic;
using UnityEngine;

namespace RpgGame
{
    // 装备/背包的唯一写入口。
    // 存档契约：inventory 存“拥有的装备 id”，equipped 存“当前穿着的装备 id”。
    // 槽位由 EquipItemData.slot 决定，因此 equipped 不需要额外的槽位结构，旧存档天然兼容。
    public static class EquipmentService
    {
        // 穿脱或获得装备后触发；属性组件订阅它重新计算。
        public static event Action Changed;

        static RpgCharacter Character
            => SaveSystem.Current != null ? SaveSystem.Current.character : null;

        public static bool Owns(string itemId)
        {
            var character = Character;
            return character != null && character.inventory != null &&
                   !string.IsNullOrEmpty(itemId) && character.inventory.Contains(itemId);
        }

        public static bool IsEquipped(string itemId)
        {
            var character = Character;
            return character != null && character.equipped != null &&
                   !string.IsNullOrEmpty(itemId) && character.equipped.Contains(itemId);
        }

        public static EquipItemData GetEquipped(EquipSlot slot)
        {
            var character = Character;
            if (character == null || character.equipped == null) return null;

            for (int i = 0; i < character.equipped.Count; i++)
            {
                var item = EquipDatabase.Find(character.equipped[i]);
                if (item != null && item.slot == slot) return item;
            }

            return null;
        }

        // 获得一件装备。已经拥有时返回 false，调用方可以折算金币。
        public static bool Grant(string itemId, bool save = true)
        {
            var item = EquipDatabase.Find(itemId);
            var character = Character;
            if (item == null || character == null) return false;
            if (character.inventory == null) character.inventory = new List<string>();
            if (character.inventory.Contains(item.id)) return false;

            character.inventory.Add(item.id);
            Notify(save);
            return true;
        }

        // 穿装备。要求背包里已经拥有；同槽旧件会被替换。
        public static bool Equip(string itemId, bool save = true)
        {
            var item = EquipDatabase.Find(itemId);
            var character = Character;
            if (item == null || character == null || !Owns(item.id)) return false;

            if (character.equipped == null) character.equipped = new List<string>();

            // 先移掉所有同槽装备，再放入新件，保证一槽最多一件。
            for (int i = character.equipped.Count - 1; i >= 0; i--)
            {
                var current = EquipDatabase.Find(character.equipped[i]);
                if (current == null || current.slot == item.slot)
                    character.equipped.RemoveAt(i);
            }

            character.equipped.Add(item.id);
            Notify(save);
            return true;
        }

        public static bool Unequip(EquipSlot slot, bool save = true)
        {
            var character = Character;
            if (character == null || character.equipped == null) return false;

            bool removed = false;
            for (int i = character.equipped.Count - 1; i >= 0; i--)
            {
                var item = EquipDatabase.Find(character.equipped[i]);
                if (item == null || item.slot == slot)
                {
                    character.equipped.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed) Notify(save);
            return removed;
        }

        // 背包里的有效装备，已去重并按数据库顺序返回。
        public static List<EquipItemData> OwnedItems()
        {
            var result = new List<EquipItemData>();
            var all = EquipDatabase.All;
            for (int i = 0; i < all.Length; i++)
            {
                if (Owns(all[i].id)) result.Add(all[i]);
            }
            return result;
        }

        static void Notify(bool save)
        {
            if (save) SaveSystem.Save();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
        }
    }
}
