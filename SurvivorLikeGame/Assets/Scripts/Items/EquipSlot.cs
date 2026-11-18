namespace RpgGame
{
    // 装备槽位。一件装备只占一个槽，穿新装备会替换同槽旧件。
    public enum EquipSlot
    {
        Head = 0,
        Body = 1,
        Back = 2,
        Weapon = 3,
    }

    public static class EquipSlotText
    {
        public static string Of(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Head: return "头部";
                case EquipSlot.Body: return "身体";
                case EquipSlot.Back: return "背部";
                case EquipSlot.Weapon: return "武器";
                default: return slot.ToString();
            }
        }
    }
}
