using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 把 SampleScene 里的 Spine 主角替换成 8 方向像素主角。
// 可重复执行：已有 HD2D_PixelPlayer 时只补组件/参数。
public static class Cat8DirSetup
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/HD2D/10. Setup Cat 8Dir Player")]
    static void Setup()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            active = EditorSceneManager.OpenScene(ScenePath);
        }

        var spine = Object.FindFirstObjectByType<Hd2dSpinePlayer>(FindObjectsInactive.Include);
        GameObject root;
        if (spine != null)
        {
            root = spine.gameObject;
            Object.DestroyImmediate(spine);
            root.name = "HD2D_PixelPlayer";
            root.SetActive(true);
        }
        else
        {
            root = GameObject.Find("HD2D_PixelPlayer");
            if (root == null) root = new GameObject("HD2D_PixelPlayer");
        }

        var pixel = root.GetComponent<Hd2dPixelPlayer>();
        if (pixel == null) pixel = root.AddComponent<Hd2dPixelPlayer>();
        pixel.viewScale = 1.6f;
        pixel.worldYOffset = 0f;
        pixel.startDirection = 6; // 南 / 正面
        pixel.runSpeedReference = 5f;
        pixel.maxRunFps = 16f;
        pixel.dust = true;
        pixel.dustColor = new Color(0.85f, 0.80f, 0.70f, 0.40f);
        pixel.dustSize = 0.45f;
        pixel.dustRate = 24f;
        pixel.turnDust = true;
        pixel.turnDustCount = 3;
        pixel.stopDustCount = 5;
        pixel.turnDustCooldown = 0.12f;
        pixel.shadowDiameter = 0.85f;
        pixel.shadowDepth = 0.50f;
        pixel.shadowOpacity = 0.42f;
        pixel.litAmbient = 0.85f;
        pixel.xrayColor = new Color(0.35f, 0.82f, 1f, 0.45f);

        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);
        Debug.Log("[Cat8Dir] 像素主角已接入 " + active.name);
    }
}
