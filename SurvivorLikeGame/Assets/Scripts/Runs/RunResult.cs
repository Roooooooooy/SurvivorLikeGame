using System;
using System.Collections.Generic;

namespace RpgGame
{
    // Run 结束时交回 RPG 层的唯一凭据。
    [Serializable]
    public class RunResult
    {
        public bool success;
        public float duration;
        public int kills;
        public int coins;
        public int exp;
        public List<string> itemIds = new List<string>();
    }
}
