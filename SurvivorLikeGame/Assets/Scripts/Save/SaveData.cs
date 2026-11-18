using System;

namespace RpgGame
{
    [Serializable]
    public class SaveData
    {
        public int schemaVersion = 1;
        public long savedAtUnix;
        public RpgCharacter character = new RpgCharacter();
        public WorldState world = new WorldState();
    }
}
