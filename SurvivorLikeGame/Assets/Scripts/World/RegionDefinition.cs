using System.Collections.Generic;
using UnityEngine;

namespace RpgGame
{
    // 一个区域 = 一个场景 + 一份配置。章节通过 RegionDefinition 列表组织区域。
    [CreateAssetMenu(fileName = "Region", menuName = "角色扮演/区域")]
    public class RegionDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public string sceneName;
        public RegionMode mode = RegionMode.ActionRPG;
        public Bounds bounds = new Bounds(Vector3.zero, new Vector3(40f, 22f, 0f));
        public string defaultSpawnPointId = "default";
        public List<string> tags = new List<string>();
    }
}
