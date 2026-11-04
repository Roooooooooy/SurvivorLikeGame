using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 把主场景 SampleScene 的渲染切到 HD-2D，同时保留原有 XY 玩法与 2D 碰撞：
//   - 相机：正交 → 透视俯角；CameraFollow 切 hd2d 模式
//   - 背景：关闭 2D 背景，换成跟随玩家的 3D 地面（HD2D/Ground）
//   - 精灵：Hd2dSpriteBridge 在运行时把 2D SpriteRenderer 镜像到 XZ billboard
//   - 敌人：场景内放一个 hd2dRendering=true 的 EnemySimulation
//   - 后处理：Global Volume + URP 相机后处理（TiltShift 已在 HD2D_Renderer 上）
// 可重复执行：已有对象会原地更新。
public static class SampleSceneHd2dSetup
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string GroundMatPath = "Assets/Settings/HD2D_Ground.mat";
    const string GroundGameMatPath = "Assets/Settings/HD2D_GroundGame.mat";
    const string ProfilePath = "Assets/Settings/HD2D_Profile.asset";
    const string TuningPath = "Assets/Settings/HD2D_GameTuning.asset";
    const string PropLibraryPath = "Assets/Resources/HD2D/PropLibrary.asset";

    [MenuItem("Tools/HD2D/7. Migrate SampleScene to HD-2D")]
    public static void Migrate()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            Debug.LogError("[HD2D] 找不到 " + ScenePath);
            return;
        }

        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        SetupCamera();
        SetupCanvas();
        SetupGround();
        SetupEnemySimulation();
        SetupSpriteBridge();
        SetupPropLibrary();
        SetupSpinePlayer();
        SetupVolume();
        SetupVolumeTuning();
        SetupLights();
        SetupShaders();
        DisableLegacyBackground();
        ResetPlayerStart();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[HD2D] SampleScene 已迁移到 HD-2D 渲染（逻辑仍为 XY）");
    }

    static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[HD2D] 找不到 Main Camera");
            return;
        }

        cam.orthographic = false;
        cam.fieldOfView = 35f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 150f;
        cam.allowHDR = true;
        cam.transform.SetPositionAndRotation(
            new Vector3(0f, 9f, -14f), Quaternion.Euler(30f, 0f, 0f));

        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.renderShadows = true;

        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.hd2d = true;
        follow.hd2dOffset = new Vector3(0f, 9f, -14f);
        follow.hd2dEuler = new Vector3(30f, 0f, 0f);
    }

    static void SetupCanvas()
    {
        // HUD 用 Screen Space Overlay：不受 3D 相机/移轴后处理影响，始终清晰盖在最上层
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[HD2D] 找不到 Canvas");
            return;
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    static void SetupGround()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMatPath);
        if (mat == null)
        {
            Debug.LogError("[HD2D] 找不到 " + GroundMatPath + "，先运行 Tools/HD2D/3");
            return;
        }

        var existing = GameObject.Find("HD2D_Ground");
        GameObject go = existing;
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "HD2D_Ground";
            var col = go.GetComponent<MeshCollider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        // 主场景单独一份更亮的地面材质，避免把 HD2D 原型场景一起调亮
        var gameMat = AssetDatabase.LoadAssetAtPath<Material>(GroundGameMatPath);
        if (gameMat == null)
        {
            gameMat = new Material(mat);
            AssetDatabase.CreateAsset(gameMat, GroundGameMatPath);
        }
        else
        {
            gameMat.shader = mat.shader;
            gameMat.CopyPropertiesFromMaterial(mat);
        }
        gameMat.SetColor("_BaseColor", new Color(1.25f, 1.22f, 1.28f));
        gameMat.SetFloat("_Ambient", 0.55f);
        EditorUtility.SetDirty(gameMat);

        go.transform.localScale = new Vector3(20f, 1f, 20f); // 200 x 200
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = gameMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;

        var follow = go.GetComponent<Hd2dGroundFollow>();
        if (follow == null) follow = go.AddComponent<Hd2dGroundFollow>();
        follow.target = null;

        // 跟随玩家的暖色点光：让标题页和战斗都有可读的光池
        var torchT = go.transform.Find("GroundTorch");
        GameObject torch = torchT != null ? torchT.gameObject : new GameObject("GroundTorch");
        torch.transform.SetParent(go.transform, false);
        torch.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        var light = torch.GetComponent<Light>();
        if (light == null) light = torch.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.62f, 0.30f);
        light.intensity = 1.1f;
        light.range = 7f;
        light.shadows = LightShadows.None;
    }

    static void SetupEnemySimulation()
    {
        var sim = Object.FindFirstObjectByType<EnemySimulation>();
        if (sim == null)
        {
            var go = new GameObject("EnemySimulation");
            sim = go.AddComponent<EnemySimulation>();
        }
        sim.hd2dRendering = true;
    }

    static void SetupSpriteBridge()
    {
        var existing = GameObject.Find("HD2D_Bridge");
        GameObject go = existing;
        if (go == null)
        {
            go = new GameObject("HD2D_Bridge");
        }
        if (go.GetComponent<Hd2dSpriteBridge>() == null)
            go.AddComponent<Hd2dSpriteBridge>();

        var bridge = go.GetComponent<Hd2dSpriteBridge>();
        bridge.playerAmbient = 0.75f;
        bridge.playerTint = new Color(0.96f, 0.98f, 1f, 1f);
        bridge.playerScale = 1.30f;
    }

    static void SetupPropLibrary()
    {
        HD2DLowPolyKit.Build();

        var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/HD2D_Stone.mat");
        var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/HD2D_DarkStone.mat");
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/HD2D_Wood.mat");

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/HD2D"))
            AssetDatabase.CreateFolder("Assets/Resources", "HD2D");

        var lib = AssetDatabase.LoadAssetAtPath<Hd2dPropLibrary>(PropLibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<Hd2dPropLibrary>();
            AssetDatabase.CreateAsset(lib, PropLibraryPath);
        }

        lib.entries = new[]
        {
            MakeEntry("pillar", "HD2D_Column", dark, new Vector3(1.1f, 0.40f, 1.1f), 1.04f),
            MakeEntry("statue", "HD2D_Column", stone, new Vector3(0.9f, 0.50f, 0.9f), 1.30f),
            MakeEntry("rubble", "HD2D_RubbleA", stone, new Vector3(2.2f, 2.2f, 2.2f), 0.33f),
            MakeEntry("brazier", "HD2D_Brazier", dark, new Vector3(1.2f, 1.2f, 1.2f), 0f),
            MakeEntry("sarcophagus", "HD2D_Sarcophagus", dark, new Vector3(0.8f, 0.8f, 0.8f), 0f),
            MakeEntry("crate", "HD2D_Crate", wood, new Vector3(0.85f, 0.85f, 0.85f), 0.425f),
        };
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();

        var bridgeGo = GameObject.Find("HD2D_Bridge");
        var bridge = bridgeGo != null ? bridgeGo.GetComponent<Hd2dSpriteBridge>() : null;
        if (bridge != null) bridge.propLibrary = lib;
    }

    static Hd2dPropLibrary.Entry MakeEntry(string key, string meshName, Material mat,
        Vector3 scale, float yOffset)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/HD2D/" + meshName + ".asset");
        return new Hd2dPropLibrary.Entry
        {
            key = key,
            mesh = mesh,
            material = mat,
            scale = scale,
            yOffset = yOffset,
            yawRange = 360f,
        };
    }

    static void SetupSpinePlayer()
    {
        // 用 Catalyst 猫 Spine 替换玩家贴图；数据资产由 CatSpineSetup 生成
        var data = CatSpineSetup.Build();
        if (data == null) return;

        var go = GameObject.Find("HD2D_SpinePlayer");
        if (go == null) go = new GameObject("HD2D_SpinePlayer");
        var comp = go.GetComponent<Hd2dSpinePlayer>();
        if (comp == null) comp = go.AddComponent<Hd2dSpinePlayer>();
        comp.skeletonDataAsset = data;
        comp.defaultSkin = "custom";
        comp.equipDemoGear = true;
        comp.viewScale = 1.25f;
        comp.worldYOffset = -0.35f;
        comp.invertFacing = true;
    }

    static void SetupVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            Debug.LogError("[HD2D] 找不到 " + ProfilePath + "，先运行 Tools/HD2D/3");
            return;
        }

        var go = GameObject.Find("HD2D_GlobalVolume");
        if (go == null) go = new GameObject("HD2D_GlobalVolume");
        var vol = go.GetComponent<Volume>();
        if (vol == null) vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;
    }

    static void SetupVolumeTuning()
    {
        // 主场景怪物密度高、颜色亮，沿用原型的 Bloom 1.5 会整体过曝。
        // 用一个高优先级 Volume 只覆盖 Bloom，其余后处理仍用 HD2D_Profile。
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(TuningPath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, TuningPath);
        }

        Bloom bloom = null;
        var components = profile.components;
        for (int i = 0; i < components.Count; i++)
        {
            if (components[i] is Bloom b) { bloom = b; break; }
        }

        if (bloom == null)
        {
            bloom = profile.Add<Bloom>(true);
            bloom.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(bloom, profile);
        }

        bloom.active = true;
        bloom.intensity.Override(0.65f);
        bloom.threshold.Override(0.90f);
        bloom.scatter.Override(0.60f);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var go = GameObject.Find("HD2D_GameTuning");
        if (go == null) go = new GameObject("HD2D_GameTuning");
        var vol = go.GetComponent<Volume>();
        if (vol == null) vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10f;
        vol.sharedProfile = profile;
    }

    static void ResetPlayerStart()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            player.transform.position = Vector3.zero;
    }

    static void SetupLights()
    {
        var sun = Object.FindFirstObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.color = new Color(1f, 0.90f, 0.76f);
            sun.intensity = 2.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.3f;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.12f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.55f);
    }

    static void SetupShaders()
    {
        // 运行时通过 Shader.Find 取 shader；出包前必须放进 Always Included，否则会被裁剪
        string[] names =
        {
            "Survivor/URPInstancedSprite",
            "Survivor/URPInstancedShadow",
            "HD2D/SpriteLit",
            "HD2D/Ground",
            "Spine/SkeletonAlways",
        };

        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning("[HD2D] 找不到 GraphicsSettings.asset");
            return;
        }

        var so = new SerializedObject(assets[0]);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        if (list == null)
        {
            Debug.LogWarning("[HD2D] GraphicsSettings 缺少 m_AlwaysIncludedShaders");
            return;
        }

        for (int n = 0; n < names.Length; n++)
        {
            var shader = Shader.Find(names[n]);
            if (shader == null)
            {
                Debug.LogWarning("[HD2D] 找不到 shader: " + names[n]);
                continue;
            }

            bool found = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
        }

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    static void DisableLegacyBackground()
    {
        string[] names = { "Background", "BackgroundGrunge", "BackgroundDecals" };
        for (int i = 0; i < names.Length; i++)
        {
            var go = GameObject.Find(names[i]);
            if (go != null) go.SetActive(false);
        }
    }
}
