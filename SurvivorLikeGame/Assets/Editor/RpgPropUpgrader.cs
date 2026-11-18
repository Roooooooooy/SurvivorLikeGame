using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RpgGameEditor
{
    // 只升级当前打开场景里的宝箱/传送门，不重建整个场景，避免覆盖手工调整。
    public static class RpgPropUpgrader
    {
        [MenuItem("工具/角色扮演/升级当前场景的宝箱与传送门")]
        public static void Upgrade()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[RPG] 请先打开 Region_Test 场景");
                return;
            }

            Vector3 chestPosition = new Vector3(-3f, -1.5f, 0f);
            Vector3 portalPosition = new Vector3(0f, 4.5f, 0f);

            var chestLogic = GameObject.Find("Chest");
            if (chestLogic != null) chestPosition = chestLogic.transform.position;

            var portalLogic = GameObject.Find("Portal_Rift");
            if (portalLogic != null) portalPosition = portalLogic.transform.position;

            DestroyIfExists("Chest_Visual");
            DestroyIfExists("Chest");
            DestroyIfExists("Portal_Visual");
            DestroyIfExists("Portal_Rift");

            Phase0SkeletonBuilder.BuildChest(chestPosition);
            Phase0SkeletonBuilder.BuildRiftPortal(portalPosition);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[RPG] 宝箱/传送门已升级（实体碰撞 + 可开盖 + 新漩涡传送门）");
        }

        static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
