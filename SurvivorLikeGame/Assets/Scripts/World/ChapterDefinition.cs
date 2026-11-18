using System.Collections.Generic;
using UnityEngine;

namespace RpgGame
{
    [CreateAssetMenu(fileName = "Chapter", menuName = "角色扮演/章节")]
    public class ChapterDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public int order;
        public string unlockFlag;
        public List<RegionDefinition> regions = new List<RegionDefinition>();
    }
}
