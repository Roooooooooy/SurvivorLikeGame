using System.Collections.Generic;

namespace RpgGame
{
    // 角色最终战斗属性：基础值 + 所有已装备件。
    // 所有系统都从这里取值，避免在各处分别叠加装备数值。
    public sealed class StatBlock
    {
        public int attack;
        public int defense;
        public int maxHealth;
        public float moveSpeed;
    }

    public static class CharacterStats
    {
        public static StatBlock Compute(RpgCharacter character)
        {
            var stats = new StatBlock();
            if (character == null) return stats;

            stats.attack = character.baseAttack;
            stats.defense = character.baseDefense;
            stats.maxHealth = character.baseMaxHealth;
            stats.moveSpeed = character.baseMoveSpeed;

            if (character.equipped == null || character.equipped.Count == 0)
                return stats;

            // 读档或手改存档可能留下重复 id，这里再去重一次。
            var seen = new HashSet<string>();
            for (int i = 0; i < character.equipped.Count; i++)
            {
                string itemId = character.equipped[i];
                if (string.IsNullOrEmpty(itemId) || !seen.Add(itemId)) continue;

                var item = EquipDatabase.Find(itemId);
                if (item == null) continue;

                stats.attack += item.attack;
                stats.defense += item.defense;
                stats.maxHealth += item.maxHealth;
                stats.moveSpeed += item.moveSpeed;
            }

            return stats;
        }

        public static StatBlock Current
        {
            get
            {
                var save = SaveSystem.Current;
                return Compute(save != null ? save.character : null);
            }
        }
    }
}
