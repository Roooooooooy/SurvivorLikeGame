using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 生成 1000 敌人 HD-2D 压测场景：
// 复制 HD2D_Proto（保留移轴/Bloom/调色/地面/灯光），加入 EnemySimulation、
// 一个只提供位置的 PlayerController，以及 EnemyPerfStress 驱动。
public static class EnemyPerfSetup
{
    const string SourceScene = "Assets/Scenes/HD2D_Proto.unity";
    const string PerfScene = "Assets/Scenes/HD2D_EnemyPerf.unity";

    [MenuItem("Tools/HD2D/5. Build 1000 Enemy Perf Scene")]
    public static void BuildPerfScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
        {
            Debug.LogError("[Perf] 找不到源场景: " + SourceScene);
            return;
        }

        EditorSceneManager.SaveOpenScenes();

        AssetDatabase.DeleteAsset(PerfScene);
        if (!AssetDatabase.CopyAsset(SourceScene, PerfScene))
        {
            Debug.LogError("[Perf] 复制场景失败");
            return;
        }
        AssetDatabase.Refresh();

        var scene = EditorSceneManager.OpenScene(PerfScene, OpenSceneMode.Single);

        // 相机后拉 + 加大 FOV，让 1000 个敌人尽量落在画面内
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.SetPositionAndRotation(new Vector3(0f, 12f, -20f), Quaternion.Euler(30f, 0f, 0f));
            cam.fieldOfView = 40f;
        }
        else
        {
            Debug.LogWarning("[Perf] 场景里没有 Main Camera");
        }

        // 敌人仿真器（EnemySimulation.Awake 会自动从 Resources/EnemyTypes 初始化）
        var sim = Object.FindFirstObjectByType<EnemySimulation>();
        if (sim == null)
        {
            var simGo = new GameObject("EnemySimulation");
            sim = simGo.AddComponent<EnemySimulation>();
        }
        sim.hd2dRendering = true; // 只在这个验证场景启用 XZ 映射 + URP alpha clip

        // 只承担“玩家位置”的载体：EnemySimulation 用 PlayerController.Instance 取目标点。
        // 无输入、速度 0，敌人会向原点汇聚；不参与实际玩法。
        if (Object.FindFirstObjectByType<PlayerController>() == null)
        {
            var playerGo = new GameObject("PerfPlayer");
            var rb = playerGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            playerGo.AddComponent<SpriteRenderer>(); // PlayerController.HandleMovement 会访问它
            var pc = playerGo.AddComponent<PlayerController>();
            pc.moveSpeed = 0f;
            pc.maxHealth = 999999;
            pc.invincibleDuration = 9999f; // 只吃一次伤害，避免压测时反复触发音效/协程
        }

        if (Object.FindFirstObjectByType<EnemyPerfStress>() == null)
        {
            var perfGo = new GameObject("PerfStress");
            var stress = perfGo.AddComponent<EnemyPerfStress>();
            stress.enemyCount = 1000;
        }

        // FrameTimingManager 需要开启 Frame Timing Stats，否则 CPU/GPU 帧时间读数为 0
        PlayerSettings.enableFrameTimingStats = true;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, PerfScene);
        AssetDatabase.Refresh();

        Debug.Log("[Perf] 压测场景已生成: " + PerfScene);
    }
}
