using System;
using System.Collections.Generic;

namespace RpgGame
{
    // 单个区域的持久化状态。所有 id 必须由内容手工指定，禁止用对象名或下标。
    [Serializable]
    public class RegionState
    {
        public bool visited;
        public List<string> openedChests = new List<string>();
        public List<string> defeatedEnemies = new List<string>();
        public List<string> flags = new List<string>();

        public bool HasFlag(string flag)
            => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag) && !flags.Contains(flag))
                flags.Add(flag);
        }
    }

    // JsonUtility 不支持 Dictionary，区域状态用列表存储。
    [Serializable]
    public class RegionStateEntry
    {
        public string regionId;
        public RegionState state = new RegionState();
    }

    [Serializable]
    public class WorldState
    {
        public string chapterId = "chapter_01";
        public string regionId = "region_test";
        public string spawnPointId = "default";
        public List<RegionStateEntry> regions = new List<RegionStateEntry>();
        public List<string> flags = new List<string>();

        public RegionState GetRegion(string id)
        {
            if (string.IsNullOrEmpty(id)) id = regionId;

            foreach (var entry in regions)
            {
                if (entry == null || entry.regionId != id) continue;
                if (entry.state == null) entry.state = new RegionState();
                return entry.state;
            }

            var created = new RegionStateEntry { regionId = id, state = new RegionState() };
            regions.Add(created);
            return created.state;
        }

        public bool HasFlag(string flag)
            => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag) && !flags.Contains(flag))
                flags.Add(flag);
        }
    }
}
