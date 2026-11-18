using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RpgGame;

namespace RpgGameEditor
{
    // 哥布林战士接入工具：导入设置、预制体、放入当前场景。
    // 资源已经由流水线归一化到 Resources/Enemies/GoblinWarrior。
    public static class GoblinWarriorSetup
    {
        const string ResourceRoot = "Assets/Resources/Enemies/GoblinWarrior";
        const string PrefabPath = "Assets/Prefabs/Enemy_GoblinWarrior.prefab";

        // 世界方向索引（0=东…7=东南）；已按美术方向镜像映射。
        static readonly float[] DirectionGroundY = { 67f, 66f, 66f, 64f, 67f, 65f, 65f, 63f };

        [MenuItem("工具/角色扮演/哥布林战士/1. 重新导入精灵表")]
        public static void Reimport()
        {
            AssetDatabase.ImportAsset(ResourceRoot,
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Debug.Log("[Goblin] 已重新导入哥布林精灵表");
        }

        [MenuItem("工具/角色扮演/哥布林战士/2. 生成预制体")]
        public static void CreatePrefab()
        {
            EnsureFolder("Assets/Prefabs");

            var go = new GameObject("Enemy_GoblinWarrior");

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.mass = 20f;
            body.linearDamping = 10f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.42f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;

            var view = go.AddComponent<DirectionalEnemyView>();
            view.resourceRoot = "Enemies/GoblinWarrior";
            view.frameWidth = 96;
            view.frameHeight = 96;
            view.pixelsPerUnit = 24f;
            view.idleFps = 6f;
            view.runFps = 10f;
            view.hitFps = 16f;
            view.deathFps = 10f;
            view.deathFadeDelay = 0.7f;
            view.deathFadeDuration = 0.9f;
            view.directionGroundY = DirectionGroundY;
            view.groundShadow = true;
            view.shadowDiameter = 1.15f;
            view.shadowDepth = 0.60f;
            view.shadowOpacity = 0.45f;
            view.litAmbient = 0.70f;

            var lift = go.AddComponent<Hd2dProxyLift>();
            lift.castShadow = true;
            lift.receiveShadows = true;

            var enemy = go.AddComponent<ActionEnemy>();
            enemy.maxHealth = 80;
            enemy.knockbackResistance = 0.75f;
            enemy.deathDelay = 0.35f;
            enemy.deathLinger = 0.6f;
            enemy.expReward = 20;

            var brain = go.AddComponent<EnemyBrain>();
            brain.moveSpeed = 2.2f;
            brain.aggroRange = 8f;
            brain.loseTargetRange = 14f;
            brain.attackRange = 1.2f;
            brain.attackWindup = 0.45f;
            brain.attackRecover = 0.35f;
            brain.attackDamage = 14;
            brain.minHitStun = 0.15f;

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Goblin] 预制体已生成 " + PrefabPath);
        }

        [MenuItem("工具/角色扮演/哥布林战士/3. 添加到当前场景")]
        public static void AddToActiveScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Goblin] 找不到预制体，请先执行“2. 生成预制体”");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            AddInstance(prefab, "Enemy_GoblinWarrior_A", new Vector3(6.5f, 3.5f, 0f));
            AddInstance(prefab, "Enemy_GoblinWarrior_B", new Vector3(-7f, -3.5f, 0f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Goblin] 已把哥布林战士加入 " + scene.name);
        }

        static void AddInstance(GameObject prefab, string name, Vector3 position)
        {
            if (GameObject.Find(name) != null)
            {
                Debug.Log("[Goblin] 已存在 " + name + "，跳过");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.position = position;
            Undo.RegisterCreatedObjectUndo(go, "添加哥布林战士");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
