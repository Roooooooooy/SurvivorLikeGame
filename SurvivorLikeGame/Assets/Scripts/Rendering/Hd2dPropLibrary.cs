using UnityEngine;

// 障碍物 → 低模地牢模块的映射表。迁移脚本会生成 Resources/HD2D/PropLibrary.asset。
[CreateAssetMenu(fileName = "PropLibrary", menuName = "HD2D/Prop Library")]
public class Hd2dPropLibrary : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string key;
        public Mesh mesh;
        public Material material;
        public Vector3 scale = Vector3.one;
        public float yOffset;
        public float yawRange = 360f;
    }

    public Entry[] entries;

    public Entry Find(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName) || entries == null) return null;
        string n = spriteName.ToLowerInvariant();
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e != null && !string.IsNullOrEmpty(e.key) && n.Contains(e.key))
                return e;
        }
        return null;
    }
}
