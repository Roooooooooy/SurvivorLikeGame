using System;
using System.Collections.Generic;
using UnityEngine;

namespace RpgGame
{
    [Serializable]
    public class SpawnPoint
    {
        public string id = "default";
        public Transform point;
    }

    // 每个区域场景放一个：提供区域 id、玩法模式、出生点和相机/玩家边界。
    [DisallowMultipleComponent]
    public class RegionRoot : MonoBehaviour
    {
        public string regionId = "region_test";
        public RegionMode mode = RegionMode.ActionRPG;
        public string defaultSpawnPointId = "default";
        public List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        public Bounds bounds = new Bounds(Vector3.zero, new Vector3(40f, 22f, 0f));

        public Transform FindSpawn(string id)
        {
            if (string.IsNullOrEmpty(id)) id = defaultSpawnPointId;

            foreach (var spawn in spawnPoints)
                if (spawn != null && spawn.point != null && spawn.id == id)
                    return spawn.point;

            foreach (var spawn in spawnPoints)
                if (spawn != null && spawn.point != null)
                    return spawn.point;

            return null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(transform.position + bounds.center, bounds.size);
        }
    }
}
