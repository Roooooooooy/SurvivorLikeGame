using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// P0 验证：斜俯视 45° 的最小场景。
// 逻辑空间仍是 XY，渲染空间映射到 XZ（(x, y) -> (x, 0, y)）。
// 本场景只验证空间关系/比例，不影响 SampleScene。
public static class Proto25DSetup
{
    const string ScenePath = "Assets/Scenes/Proto25D.unity";
    const string PillarPath = "Assets/Resources/Proto25D/pillar_25d.png";
    const string GoblinPath = "Assets/Resources/Proto25D/goblin_25d.png";
    const string CatPath = "Assets/Resources/Proto25D/cat_25d.png";

    [MenuItem("Tools/Proto/1. Setup 2.5D Scene")]
    public static void Setup()
    {
        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.ambientLight = new Color(0.25f, 0.27f, 0.32f);

        CreateCamera();

        // 地面：3x3 平铺，sprite 平躺到 XZ 平面
        var floor = LoadSprite("Assets/Textures/Background/bg_stone_floor.png");
        if (floor != null)
        {
            for (int z = -1; z <= 1; z++)
                for (int x = -1; x <= 1; x++)
                    CreateGround(floor, new Vector3(x * 16f, 0f, z * 16f));
        }

        // 3/4 视角新素材（AI 生成，统一 PPU 32 / Point / 无压缩）
        AssetDatabase.Refresh();
        EnsureSpriteImport(PillarPath);
        EnsureSpriteImport(GoblinPath);
        EnsureSpriteImport(CatPath);

        var pillar = LoadSprite(PillarPath);
        var goblin = LoadSprite(GoblinPath);
        var cat = LoadSprite(CatPath);

        // 障碍：立起来，脚底贴地；高度补偿 1.15 抵消 45° 视角的纵向压缩
        CreateUpright(pillar, new Vector3(-4f, 0f, 0f), "PillarA", 1.15f);
        CreateUpright(pillar, new Vector3(4f, 0f, 1f), "PillarB", 1.15f);
        CreateUpright(pillar, new Vector3(0f, 0f, -2.5f), "PillarC", 1f);

        // 主角
        CreateUpright(cat, new Vector3(0f, 0f, 0f), "PlayerCat", 1.25f);

        // 敌人
        CreateUpright(goblin, new Vector3(2.5f, 0f, 2f), "GoblinA", 1.25f);
        CreateUpright(goblin, new Vector3(-2.5f, 0f, 2.5f), "GoblinB", 1.25f);
        CreateUpright(goblin, new Vector3(1.5f, 0f, 4f), "GoblinC", 1.25f);
        CreateUpright(goblin, new Vector3(-1.5f, 0f, -3f), "GoblinD", 1.15f);
        CreateUpright(goblin, new Vector3(3.5f, 0f, -2f), "GoblinE", 1.35f);

        // 对比：旧俯视素材（偏红标记）
        CreateUpright(LoadSprite("Assets/Resources/Obstacles/obstacle_pillar_a.png"),
            new Vector3(-6.5f, 0f, 3f), "OLD_Pillar", 1f, new Color(1f, 0.75f, 0.75f));
        CreateUpright(LoadSprite("Assets/Textures/enemy_goblin.png"),
            new Vector3(6.5f, 0f, 3f), "OLD_Goblin", 1f, new Color(1f, 0.75f, 0.75f));

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[Proto25D] 场景已生成: " + ScenePath);
    }

    [MenuItem("Tools/Proto/2. Capture 2.5D Preview")]
    public static void Capture()
    {
        CaptureTo("/tmp/proto25d.png");
    }

    // 透视对比：同样的场景，改为 FOV 40 的透视相机。
    // 正交只有压缩、没有近大远小；透视才会出现地面收敛和纵深。
    [MenuItem("Tools/Proto/3. Capture Perspective Preview")]
    public static void CapturePerspective()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[Proto25D] 找不到 Main Camera");
            return;
        }

        bool ortho = cam.orthographic;
        float size = cam.orthographicSize;
        float fov = cam.fieldOfView;
        Vector3 pos = cam.transform.position;
        Quaternion rot = cam.transform.rotation;

        cam.orthographic = false;
        cam.fieldOfView = 40f;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 9.7f, -9.7f), Quaternion.Euler(45f, 0f, 0f));

        CaptureTo("/tmp/proto25d_persp.png");

        // 窄 FOV + 拉远：保留轻微近大远小，但压缩比例更小、更接近正交的可读性
        cam.fieldOfView = 25f;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 16f, -16f), Quaternion.Euler(45f, 0f, 0f));
        CaptureTo("/tmp/proto25d_persp_narrow.png");

        cam.orthographic = ortho;
        cam.orthographicSize = size;
        cam.fieldOfView = fov;
        cam.transform.SetPositionAndRotation(pos, rot);
    }

    static void CaptureTo(string path)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[Proto25D] 找不到 Main Camera");
            return;
        }

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

        var bytes = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, bytes);

        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        Debug.Log("[Proto25D] 截图已保存: " + path);
    }

    static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogError("[Proto25D] 找不到 Sprite: " + path);
        return s;
    }

    // 新生成的 PNG 默认是 Texture，需要改成 Sprite 并统一 PPU，否则 LoadSprite 返回 null。
    static void EnsureSpriteImport(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
        {
            Debug.LogWarning("[Proto25D] 找不到导入设置: " + path);
            return;
        }
        if (ti.textureType == TextureImporterType.Sprite &&
            Mathf.Approximately(ti.spritePixelsPerUnit, 32f))
            return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 32f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
    }

    static void CreateCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.035f, 0.055f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        go.transform.SetPositionAndRotation(new Vector3(0f, 8f, -8f), Quaternion.Euler(45f, 0f, 0f));
        go.AddComponent<AudioListener>();
    }

    static void CreateGround(Sprite sprite, Vector3 pos)
    {
        var go = new GameObject("Ground");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -100;
        // 绕 X 轴 90°：sprite 的局部 Y 映射到世界 Z，平躺在地面
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, 0f, 0f));
    }

    static void CreateUpright(Sprite sprite, Vector3 groundPos, string name,
        float heightScale = 1f, Color? tint = null)
    {
        if (sprite == null) return;

        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint ?? Color.white;

        // sprite pivot 在中心：抬高半个高度让脚底贴地
        float halfHeight = sprite.bounds.extents.y * heightScale;
        go.transform.position = groundPos + new Vector3(0f, halfHeight, 0f);
        go.transform.localScale = new Vector3(1f, heightScale, 1f);

        CreateShadow(groundPos, sprite.bounds.size.x * 0.75f);
    }

    static Sprite shadowSprite;

    static void CreateShadow(Vector3 groundPos, float width)
    {
        var go = new GameObject("Shadow");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetShadowSprite();
        sr.color = new Color(0f, 0f, 0f, 0.45f);
        sr.sortingOrder = -50;
        go.transform.SetPositionAndRotation(
            groundPos + new Vector3(0f, 0.02f, 0f),
            Quaternion.Euler(90f, 0f, 0f));
        go.transform.localScale = new Vector3(width, width * 0.55f, 1f);
    }

    static Sprite GetShadowSprite()
    {
        if (shadowSprite != null) return shadowSprite;

        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x - n * 0.5f) / (n * 0.5f);
                float dy = (y - n * 0.5f) / (n * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * 0.65f;
                pixels[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        shadowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 32f);
        shadowSprite.name = "Proto25D_Shadow";
        return shadowSprite;
    }
}
