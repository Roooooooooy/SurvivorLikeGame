using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// HD-2D 最小验证场景：3D 地面 + billboard 精灵 + 主光/点光 + Volume 后处理。
// 复用 Assets/Resources/Proto25D 下的 3/4 视角素材。
public static class HD2DSceneSetup
{
    const string ScenePath = "Assets/Scenes/HD2D_Proto.unity";
    const string SettingsDir = "Assets/Settings";
    const string CatPath = "Assets/Resources/Proto25D/cat_25d.png";
    const string GoblinPath = "Assets/Resources/Proto25D/goblin_25d.png";
    const string PillarPath = "Assets/Resources/Proto25D/pillar_25d.png";

    [MenuItem("Tools/HD2D/3. Setup HD-2D Scene")]
    public static void SetupScene()
    {
        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        EnsureRepeat("Assets/Textures/Background/bg_stone_floor.png");

        CreateCamera();
        var groundMat = CreateGroundMaterial();
        CreateGround(groundMat);
        CreateLights();
        CreateEnvironment();

        var catMat = CreateSpriteMaterial(CatPath, "HD2D_Cat");
        var goblinMat = CreateSpriteMaterial(GoblinPath, "HD2D_Goblin");
        var pillarMat = CreateSpriteMaterial(PillarPath, "HD2D_Pillar");

        var cat = LoadSprite(CatPath);
        var goblin = LoadSprite(GoblinPath);
        var pillar = LoadSprite(PillarPath);

        CreateBillboard(catMat, cat, new Vector3(0f, 0f, 0f), "PlayerCat");
        CreateBillboard(goblinMat, goblin, new Vector3(3f, 0f, 1f), "GoblinA");
        CreateBillboard(goblinMat, goblin, new Vector3(-3f, 0f, 1.5f), "GoblinB");
        CreateBillboard(goblinMat, goblin, new Vector3(2f, 0f, 3.5f), "GoblinC");
        CreateBillboard(goblinMat, goblin, new Vector3(-2f, 0f, 4f), "GoblinD");
        CreateBillboard(goblinMat, goblin, new Vector3(5.5f, 0f, -1f), "GoblinE");

        CreateBillboard(pillarMat, pillar, new Vector3(-5f, 0f, -2f), "PillarA", false);
        CreateBillboard(pillarMat, pillar, new Vector3(5f, 0f, -3f), "PillarB", false);
        CreateBillboard(pillarMat, pillar, new Vector3(-6f, 0f, 3f), "PillarC", false);

        CreateVolume();
        EnsureTiltShiftFeature();
        SetupAtmosphere();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[HD2D] 场景已生成: " + ScenePath);
    }

    [MenuItem("Tools/HD2D/4. Capture HD-2D Preview")]
    public static void Capture()
    {
        // Play 模式下必须截真实渲染帧：手动 cam.Render() 不会执行 Volume 和 Renderer Feature。
        if (Application.isPlaying)
        {
            ScreenCapture.CaptureScreenshot("/tmp/hd2d.png");
            Debug.Log("[HD2D] Play 模式截图已请求: /tmp/hd2d.png");
            return;
        }

        var cam = Camera.main;
        if (cam == null) { Debug.LogError("[HD2D] 找不到 Main Camera"); return; }

        const int w = 1280, h = 720;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;

        System.IO.File.WriteAllBytes("/tmp/hd2d.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        Debug.Log("[HD2D] 截图已保存: /tmp/hd2d.png");
    }

    static void CreateCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 35f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 150f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
        cam.allowHDR = true;
        cam.allowMSAA = true;
        // 俯角 30°，距离约 16
        go.transform.SetPositionAndRotation(new Vector3(0f, 9f, -14f), Quaternion.Euler(30f, 0f, 0f));
        go.AddComponent<AudioListener>();

        // URP 相机默认不开后处理；不打开的话 Volume 里的 DOF/Bloom 全部不生效
        var camData = go.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    }

    static void CreateGround(Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "Ground";
        go.transform.localScale = new Vector3(6f, 1f, 6f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        var col = go.GetComponent<MeshCollider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    static void CreateLights()
    {
        var sunGo = new GameObject("Directional Light");
        sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.90f, 0.76f);
        sun.intensity = 2.6f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sun.shadowBias = 0.02f;
        sun.shadowNormalBias = 0.3f;

        var fireGo = new GameObject("Brazier Light");
        fireGo.transform.position = new Vector3(4.5f, 1.6f, 2f);
        var fire = fireGo.AddComponent<Light>();
        fire.type = LightType.Point;
        fire.color = new Color(1f, 0.5f, 0.16f);
        fire.range = 9f;
        fire.intensity = 4f;
        fire.shadows = LightShadows.None;
    }

    // 用内置 primitive 拼一个低多边形地牢场景：台阶、断墙、柱子、火盆、碎石。
    // 目的是给移轴景深提供前后层次，而不是做完整关卡。
    static void CreateEnvironment()
    {
        // 石头用带纹理的材质：纯色受光后会整片发白，石板纹理能保住细节
        var stone = CreateStoneMaterial("HD2D_Stone", new Color(0.48f, 0.47f, 0.50f));
        var darkStone = CreateStoneMaterial("HD2D_DarkStone", new Color(0.33f, 0.32f, 0.36f));
        var wood = CreateLitMaterial("HD2D_Wood", new Color(0.20f, 0.13f, 0.08f));

        // 远处高台 + 两级台阶（推远，别挡住角色）
        CreateBox("Dais", new Vector3(0f, 0.4f, 8.5f), new Vector3(9f, 0.8f, 5f), darkStone);
        CreateBox("Step1", new Vector3(0f, 0.15f, 6.2f), new Vector3(5.5f, 0.3f, 1.2f), stone);
        CreateBox("Step2", new Vector3(0f, 0.35f, 7.2f), new Vector3(5.5f, 0.7f, 1.2f), stone);

        // 两侧断墙
        CreateBox("WallL", new Vector3(-8f, 1.2f, 7.5f), new Vector3(0.8f, 2.4f, 9f), darkStone);
        CreateBox("WallR", new Vector3(8f, 1.2f, 7.5f), new Vector3(0.8f, 2.4f, 9f), darkStone);

        // 高台上的柱子
        for (int i = 0; i < 3; i++)
            CreateCylinder("Column" + i, new Vector3(-4f + i * 4f, 2.6f, 10f),
                new Vector3(1f, 2.6f, 1f), stone);

        // 火盆（点光源在 CreateLights 里，位置 4.5, 1.6, 2）
        CreateCylinder("BrazierBase", new Vector3(4.5f, 0.5f, 2f), new Vector3(0.35f, 0.5f, 0.35f), darkStone);
        CreateCylinder("BrazierBowl", new Vector3(4.5f, 1.05f, 2f), new Vector3(0.7f, 0.15f, 0.7f), wood);

        // 散落碎石
        CreateBox("Rubble1", new Vector3(-3.2f, 0.15f, -1.2f), new Vector3(0.55f, 0.3f, 0.45f), stone);
        CreateBox("Rubble2", new Vector3(3.4f, 0.12f, -2.4f), new Vector3(0.4f, 0.24f, 0.6f), stone);
        CreateBox("Rubble3", new Vector3(-5.2f, 0.18f, 2.2f), new Vector3(0.7f, 0.36f, 0.5f), darkStone);
    }

    static void CreateBox(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetPositionAndRotation(pos, Quaternion.identity);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        var col = go.GetComponent<BoxCollider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    static void CreateCylinder(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetPositionAndRotation(pos, Quaternion.identity);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        var col = go.GetComponent<CapsuleCollider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    static Material CreateLitMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader) { name = name };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.1f);
        mat.SetFloat("_Metallic", 0f);

        var path = SettingsDir + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material CreateStoneMaterial(string name, Color tint)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Textures/Background/bg_stone_floor.png");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader) { name = name };
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_Smoothness", 0.06f);
        mat.SetFloat("_Metallic", 0f);

        var path = SettingsDir + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void CreateBillboard(Material mat, Sprite sprite, Vector3 pos, string name, bool billboard = true)
    {
        if (mat == null || sprite == null) return;

        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = mat;
        sr.shadowCastingMode = ShadowCastingMode.On;
        sr.receiveShadows = true;

        // pivot 在中心：抬高半高让脚底贴地
        go.transform.position = new Vector3(pos.x, sprite.bounds.extents.y, pos.z);
        if (billboard) go.AddComponent<BillboardSprite>();
    }

    static Material CreateSpriteMaterial(string spritePath, string matName)
    {
        var sprite = LoadSprite(spritePath);
        var shader = Shader.Find("HD2D/SpriteLit");
        if (sprite == null || shader == null)
        {
            Debug.LogError("[HD2D] 缺少 sprite 或 shader: " + spritePath);
            return null;
        }

        var mat = new Material(shader) { name = matName };
        mat.SetTexture("_MainTex", sprite.texture);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_AmbientBoost", 0.75f);

        var path = SettingsDir + "/" + matName + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material CreateGroundMaterial()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Background/bg_stone_floor.png");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader) { name = "HD2D_Ground" };
        mat.SetTexture("_BaseMap", tex);
        // tiling 从 8 降到 5：减少地砖重复的机械感
        mat.SetTextureScale("_BaseMap", new Vector2(5f, 5f));
        mat.SetColor("_BaseColor", new Color(0.80f, 0.80f, 0.85f));
        mat.SetFloat("_Smoothness", 0.12f);
        mat.SetFloat("_Metallic", 0f);

        var path = SettingsDir + "/HD2D_Ground.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void CreateVolume()
    {
        var path = SettingsDir + "/HD2D_Profile.asset";
        AssetDatabase.DeleteAsset(path);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        // 位移轴改由 TiltShiftFeature 负责（按屏幕 Y），这里关掉按深度的 DOF，避免双重模糊
        var dof = AddVolumeComponent<DepthOfField>(profile);
        dof.mode.Override(DepthOfFieldMode.Off);

        var bloom = AddVolumeComponent<Bloom>(profile);
        bloom.intensity.Override(1.5f);
        bloom.threshold.Override(0.62f);
        bloom.scatter.Override(0.72f);

        var color = AddVolumeComponent<ColorAdjustments>(profile);
        color.postExposure.Override(0.42f);
        color.contrast.Override(22f);
        color.saturation.Override(20f);
        color.colorFilter.Override(new Color(1f, 0.92f, 0.80f));

        var split = AddVolumeComponent<SplitToning>(profile);
        split.shadows.Override(new Color(0.20f, 0.24f, 0.36f));
        split.highlights.Override(new Color(1f, 0.78f, 0.48f));
        split.balance.Override(0f);

        var vignette = AddVolumeComponent<Vignette>(profile);
        vignette.intensity.Override(0.27f);
        vignette.smoothness.Override(0.45f);

        var tone = AddVolumeComponent<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);

        var grain = AddVolumeComponent<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.12f);

        EditorUtility.SetDirty(profile);

        var go = new GameObject("Global Volume");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        // 必须用 sharedProfile：profile setter 只改内存副本，不会序列化到场景
        vol.sharedProfile = profile;
    }

    // 把自定义移轴 pass 注册到 HD2D_Renderer，重复运行会先清掉旧的。
    static void EnsureTiltShiftFeature()
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
            SettingsDir + "/HD2D_Renderer.asset");
        if (rendererData == null)
        {
            Debug.LogError("[HD2D] 找不到 HD2D_Renderer.asset");
            return;
        }

        for (int i = rendererData.rendererFeatures.Count - 1; i >= 0; i--)
        {
            var old = rendererData.rendererFeatures[i];
            if (old is TiltShiftFeature)
            {
                rendererData.rendererFeatures.RemoveAt(i);
                if (old != null) AssetDatabase.RemoveObjectFromAsset(old);
            }
        }

        var shader = Shader.Find("Hidden/HD2D/TiltShift");
        if (shader == null)
        {
            Debug.LogError("[HD2D] 找不到 Hidden/HD2D/TiltShift");
            return;
        }

        var feature = ScriptableObject.CreateInstance<TiltShiftFeature>();
        feature.name = "TiltShift";
        feature.settings.shader = shader;
        // 清晰带压在下 1/3（角色所在高度），更接近八方的移轴构图
        feature.settings.focusY = 0.45f;
        feature.settings.focusBand = 0.14f;
        feature.settings.falloff = 0.34f;
        feature.settings.maxBlur = 4.5f;
        feature.settings.curve = 1.35f;

        AssetDatabase.AddObjectToAsset(feature, rendererData);
        rendererData.rendererFeatures.Add(feature);
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();

        Debug.Log("[HD2D] TiltShift 已注册到 HD2D_Renderer");
    }

    // VolumeProfile.Add 只把组件加进内存列表，不会写进 asset；
    // 不手动 AddObjectToAsset 的话，保存/重载后 DOF、Bloom 等全部丢失。
    static T AddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        var c = profile.Add<T>(true);
        c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, profile);
        return c;
    }

    static void SetupAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.12f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.55f);
    }

    static void EnsureRepeat(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        if (ti.wrapMode == TextureWrapMode.Repeat) return;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.SaveAndReimport();
    }

    static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogError("[HD2D] 找不到 Sprite: " + path);
        return s;
    }
}
