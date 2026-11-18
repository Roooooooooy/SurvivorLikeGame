using System;
using System.Collections.Generic;
using UnityEngine;

namespace RpgGame
{
    // RPG 永久角色数据。Phase 0 只提供基础属性，装备/技能在 Phase 1 接入。
    [Serializable]
    public class RpgCharacter
    {
        public string id = "cat";
        public string displayName = "Cat";

        public int level = 1;
        public int exp;
        public int expToNext = 100;

        public int gold;

        public int baseMaxHealth = 100;
        public float baseMoveSpeed = 5f;
        public int baseAttack = 10;
        public int baseDefense;

        public List<string> inventory = new List<string>();
        public List<string> equipped = new List<string>();

        public void AddExp(int amount)
        {
            if (amount <= 0) return;

            exp += amount;
            while (exp >= expToNext)
            {
                exp -= expToNext;
                level++;
                expToNext = Mathf.Max(1, Mathf.RoundToInt(expToNext * 1.25f));
                baseMaxHealth += 5;
                baseAttack += 1;
            }
        }
    }
}
