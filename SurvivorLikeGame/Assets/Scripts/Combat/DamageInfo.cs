using UnityEngine;

namespace RpgGame
{
    public struct DamageInfo
    {
        public int amount;
        public Vector2 hitPoint;
        public Vector2 sourcePosition;
        public Vector2 knockback;
        public bool crit;
        public GameObject source;
    }
}
