using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// HD-2D 视觉桥：主场景的玩法仍然使用 XY 平面（Rigidbody2D / 2D 碰撞不变），
// 只把 SpriteRenderer 的视觉映射到世界 (x, 0, y) 的 billboard 上。
// - 敌人由 EnemySimRenderer 自己处理，不经过这里；
// - 源 renderer 用 forceRenderingOff 隐藏，enabled / color / 破坏逻辑照常；
// - 池化对象由 SmallObjectPool.Spawned 事件登记，障碍由 ObstacleField 主动登记；
// - 其余运行时生成的精灵（法球等）由低频扫描兜底。
public class Hd2dSpriteBridge : MonoBehaviour
{
    public static Hd2dSpriteBridge Instance { get; private set; }
    public static bool Active => Instance != null;

    [Header("离地高度")]
    public float playerLift = 0f;
    public float propLift = 0f;
    public float effectLift = 0.25f;
    public float playerScale = 1.30f;

    [Header("角色材质")]
    [Range(0f, 2f)] public float playerAmbient = 0.75f;
    public Color playerTint = new Color(0.96f, 0.98f, 1.00f, 1f);
    [Range(0f, 2f)] public float propAmbient = 0.60f;

    [Header("主角遮挡 X 光")]
    [Tooltip("被前景墙面/障碍/大怪挡住时，用低透明度剪影标出主角位置")]
    public bool playerXray = true;
    public Color playerXrayColor = new Color(0.42f, 0.66f, 1.00f, 0.30f);

    [Header("扫描")]
    public float rescanInterval = 0.5f;

    [Header("低模障碍")]
    public Hd2dPropLibrary propLibrary;

    class Proxy
    {
        public SpriteRenderer source;
        public SpriteRenderer renderer;
        public SpriteRenderer xray;   // 仅主角：被遮挡时的剪影
        public GameObject go;
        public bool isPlayer;
        public bool isObstacle;
        public float lift;
        public Texture tex;

        public bool isMesh;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public Hd2dPropLibrary.Entry entry;
        public Color baseColor = Color.white;
        public string spriteName;
        public MaterialPropertyBlock mpb;
        public GameObject flame;   // 火盆火焰（仅 brazier 类障碍）
    }

    Camera cam;
    Transform root;
    Shader spriteShader;
    Shader xrayShader;
    Shader flameShader;
    Mesh flameQuad;
    Material flameMaterial;
    Texture2D flameTexture;
    float scanTimer;

    readonly Dictionary<SpriteRenderer, Proxy> proxies = new Dictionary<SpriteRenderer, Proxy>();
    readonly Dictionary<Texture, Material> playerMats = new Dictionary<Texture, Material>();
    readonly Dictionary<Texture, Material> propMats = new Dictionary<Texture, Material>();
    readonly Dictionary<Texture, Material> xrayMats = new Dictionary<Texture, Material>();
    readonly List<SpriteRenderer> dead = new List<SpriteRenderer>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        cam = Camera.main;
        spriteShader = Shader.Find("HD2D/SpriteLit");
        xrayShader = Shader.Find("HD2D/SpriteXRay");
        flameShader = Shader.Find("HD2D/Flame");
        if (flameShader != null)
        {
            flameTexture = CreateFlameTexture();
            flameMaterial = new Material(flameShader)
            {
                name = "BrazierFlame (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            flameMaterial.mainTexture = flameTexture;
            flameQuad = CreateFlameQuad();
        }
        if (propLibrary == null) propLibrary = Resources.Load<Hd2dPropLibrary>("HD2D/PropLibrary");
        root = new GameObject("[HD2D_Visuals]").transform;

        if (spriteShader == null)
            Debug.LogWarning("[HD2D] 找不到 HD2D/SpriteLit，角色/障碍会退回原材质");
        if (playerXray && xrayShader == null)
            Debug.LogWarning("[HD2D] 找不到 HD2D/SpriteXRay，主角被遮挡时不会有透视剪影");

        DamageNumbers.Hd2d = true;
        Scan();

        SmallObjectPool.Spawned += OnPooledSpawned;
    }

    void OnDestroy()
    {
        SmallObjectPool.Spawned -= OnPooledSpawned;
        DamageNumbers.Hd2d = false;

        if (Instance == this) Instance = null;
        if (root != null) Destroy(root.gameObject);

        DestroyMats(playerMats);
        DestroyMats(propMats);
        DestroyMats(xrayMats);
        if (flameMaterial != null) Destroy(flameMaterial);
        if (flameTexture != null) Destroy(flameTexture);
        if (flameQuad != null) Destroy(flameQuad);
    }

    static void DestroyMats(Dictionary<Texture, Material> cache)
    {
        foreach (var m in cache.Values)
        {
            if (m != null) Destroy(m);
        }
        cache.Clear();
    }

    void OnPooledSpawned(GameObject go)
    {
        Register(go);
    }

    // 供 ObstacleField 等非 SmallObjectPool 的生成点主动登记
    public void Register(GameObject go)
    {
        if (go == null) return;
        var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            Register(renderers[i]);
    }

    public void Register(SpriteRenderer source)
    {
        if (source == null || proxies.ContainsKey(source)) return;
        if (source.forceRenderingOff) return;
        if (source.GetComponent<Hd2dProxyMarker>() != null) return;
        if (root != null && source.transform.IsChildOf(root)) return;

        if (IsIgnored(source))
        {
            // 旧 2D 背景由 3D 地面替代，直接隐藏
            source.forceRenderingOff = true;
            return;
        }

        bool isPlayer = source.GetComponentInParent<PlayerController>() != null;
        bool isObstacle = !isPlayer && source.GetComponentInParent<Obstacle>() != null;

        // 主角已由 Spine / 8 方向像素视图接管时，不再创建 2D billboard 代理
        if (isPlayer && (Hd2dSpinePlayer.Active || Hd2dPixelPlayer.Active))
        {
            source.forceRenderingOff = true;
            return;
        }

        // 有映射表的障碍换成低模地牢模块，不再用扁平贴图
        if (isObstacle && propLibrary != null)
        {
            var entry = propLibrary.Find(source.sprite != null ? source.sprite.name : null);
            if (entry != null)
            {
                CreateMeshProxy(source, entry);
                return;
            }
        }

        var go = new GameObject("[HD2D] " + source.gameObject.name);
        go.transform.SetParent(root, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.shadowCastingMode = (isPlayer || isObstacle)
            ? ShadowCastingMode.On
            : ShadowCastingMode.Off;
        sr.receiveShadows = isPlayer || isObstacle;

        var proxy = new Proxy
        {
            source = source,
            renderer = sr,
            go = go,
            isPlayer = isPlayer,
            isObstacle = isObstacle,
            lift = isPlayer ? playerLift : (isObstacle ? propLift : effectLift),
        };

        // 主角额外挂一个“被遮挡时可见”的剪影；它只在深度测试失败处出像素
        if (isPlayer && playerXray && xrayShader != null)
        {
            var xgo = new GameObject("[HD2D XRay]");
            xgo.transform.SetParent(go.transform, false);
            var xsr = xgo.AddComponent<SpriteRenderer>();
            xsr.shadowCastingMode = ShadowCastingMode.Off;
            xsr.receiveShadows = false;
            xsr.color = Color.white;
            proxy.xray = xsr;
        }

        go.AddComponent<Hd2dProxyMarker>();
        proxies[source] = proxy;
        source.forceRenderingOff = true;

        ApplyMaterial(proxy);
    }

    void CreateMeshProxy(SpriteRenderer source, Hd2dPropLibrary.Entry entry)
    {
        var go = new GameObject("[HD2D] " + source.gameObject.name);
        go.transform.SetParent(root, false);

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = entry.mesh;
        mr.sharedMaterial = entry.material;
        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.receiveShadows = true;
        go.AddComponent<Hd2dProxyMarker>();

        var proxy = new Proxy
        {
            source = source,
            go = go,
            isObstacle = true,
            isMesh = true,
            meshFilter = mf,
            meshRenderer = mr,
            entry = entry,
            baseColor = GetBaseColor(entry.material),
            spriteName = source.sprite != null ? source.sprite.name : null,
            mpb = new MaterialPropertyBlock(),
        };

        proxies[source] = proxy;
        source.forceRenderingOff = true;
    }

    static Color GetBaseColor(Material mat)
    {
        if (mat == null) return Color.white;
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        return Color.white;
    }

    void UpdateMeshProxy(Proxy p)
    {
        var src = p.source;
        bool active = src.enabled && src.gameObject.activeInHierarchy;
        p.go.SetActive(active);
        p.meshRenderer.enabled = active;
        if (!active) return;

        string spriteName = src.sprite != null ? src.sprite.name : null;
        if (spriteName != p.spriteName)
        {
            p.spriteName = spriteName;
            var entry = propLibrary != null ? propLibrary.Find(spriteName) : null;
            if (entry != null)
            {
                p.entry = entry;
                p.meshFilter.sharedMesh = entry.mesh;
                p.meshRenderer.sharedMaterial = entry.material;
                p.baseColor = GetBaseColor(entry.material);
            }
        }

        var e = p.entry;
        if (e == null) return;

        // 障碍物池会把同一个代理复用给不同类型：按当前 entry 动态加/删火焰
        bool wantFlame = flameMaterial != null && e.key != null && e.key.Contains("brazier");
        if (wantFlame && p.flame == null)
            p.flame = CreateBrazierFlame(p.go);
        else if (!wantFlame && p.flame != null)
        {
            Destroy(p.flame);
            p.flame = null;
        }

        Vector3 pos = src.transform.position;
        p.go.transform.position = new Vector3(pos.x, e.yOffset, pos.y);

        float h = Mathf.Abs(Mathf.Sin(pos.x * 12.9898f + pos.y * 78.233f) * 43758.5453f);
        float yaw = (h - Mathf.Floor(h)) * Mathf.Max(0f, e.yawRange);
        p.go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        p.go.transform.localScale = e.scale;

        Color c = p.baseColor * src.color;
        p.mpb.SetColor("_BaseColor", c);
        p.mpb.SetColor("_Color", c);
        p.meshRenderer.SetPropertyBlock(p.mpb);
    }

    // Spine 主角接管后，销毁已经建好的玩家 2D 代理
    public void SuppressPlayerProxy()
    {
        SpriteRenderer key = null;
        foreach (var pair in proxies)
        {
            if (pair.Value.isPlayer)
            {
                key = pair.Key;
                break;
            }
        }
        if (key == null) return;

        if (proxies.TryGetValue(key, out var p))
        {
            if (p.go != null) Destroy(p.go);
            proxies.Remove(key);
        }
        if (key != null) key.forceRenderingOff = true;
    }

    bool IsIgnored(SpriteRenderer source)
    {
        if (source.GetComponent<BackgroundFollow>() != null) return true;
        if (source.GetComponentInParent<BackgroundDecals>() != null) return true;
        if (source.GetComponentInParent<Canvas>() != null) return true;

        string n = source.gameObject.name;
        return n == "Background" || n == "BackgroundGrunge" || n == "BackgroundDecals";
    }

    void Scan()
    {
        var all = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            Register(all[i]);
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        scanTimer += Time.unscaledDeltaTime;
        if (scanTimer >= rescanInterval)
        {
            scanTimer = 0f;
            Scan();
        }

        dead.Clear();
        foreach (var pair in proxies)
        {
            var p = pair.Value;
            if (p.source == null)
            {
                dead.Add(pair.Key);
                continue;
            }
            UpdateProxy(p);
        }

        for (int i = 0; i < dead.Count; i++)
        {
            if (proxies.TryGetValue(dead[i], out var p))
            {
                if (p.go != null) Destroy(p.go);
                proxies.Remove(dead[i]);
            }
        }
    }

    void UpdateProxy(Proxy p)
    {
        if (p.isMesh)
        {
            UpdateMeshProxy(p);
            return;
        }

        var src = p.source;
        bool active = src.enabled && src.gameObject.activeInHierarchy;
        p.go.SetActive(active);
        p.renderer.enabled = active;
        if (!active) return;

        var sprite = src.sprite;
        var tex = sprite != null ? sprite.texture : null;
        if (tex != p.tex)
        {
            p.tex = tex;
            ApplyMaterial(p);
        }

        p.renderer.sprite = sprite;
        p.renderer.color = src.color;
        p.renderer.flipX = src.flipX;
        p.renderer.flipY = src.flipY;

        if (p.xray != null)
        {
            p.xray.enabled = true;
            p.xray.sprite = sprite;
            p.xray.flipX = p.renderer.flipX;
            p.xray.flipY = p.renderer.flipY;
        }

        Vector3 pos = src.transform.position;
        Vector3 scale = src.transform.lossyScale;
        float sx = Mathf.Abs(scale.x);
        float sy = Mathf.Abs(scale.y);
        if (p.isPlayer)
        {
            // 主角在斜俯视角下偏小，放大一点提升存在感（脚底仍然贴地）
            sx *= playerScale;
            sy *= playerScale;
        }
        if (scale.x < 0f) p.renderer.flipX = !p.renderer.flipX;
        if (scale.y < 0f) p.renderer.flipY = !p.renderer.flipY;

        // 脚底贴地：sprite.bounds.min.y 到 pivot 的距离
        float foot = sprite != null ? -sprite.bounds.min.y * sy : 0f;
        Vector3 world = new Vector3(pos.x, foot + p.lift, pos.y);
        p.go.transform.position = world;
        p.go.transform.localScale = new Vector3(sx, sy, 1f);

        // 绕 Y 轴面向相机；保留源精灵的 Z 旋转（旋转飞镖等）
        Vector3 toCam = cam.transform.position - world;
        toCam.y = 0f;
        if (toCam.sqrMagnitude > 0.0001f)
        {
            float zRot = src.transform.eulerAngles.z;
            p.go.transform.rotation =
                Quaternion.LookRotation(toCam, Vector3.up) * Quaternion.Euler(0f, 0f, zRot);
        }
    }

    void ApplyMaterial(Proxy p)
    {
        if (p.isPlayer)
        {
            p.renderer.sharedMaterial = LitMaterial(playerMats, p.tex, true);
            if (p.xray != null)
                p.xray.sharedMaterial = XRayMaterial(p.tex);
        }
        else if (p.isObstacle)
            p.renderer.sharedMaterial = LitMaterial(propMats, p.tex, false);
        else
            p.renderer.sharedMaterial = p.source.sharedMaterial; // 特效保留原材质
    }

    // 主角被遮挡时的剪影材质：与精灵同贴图，ZTest Greater + 低透明度
    Material XRayMaterial(Texture tex)
    {
        if (xrayShader == null) return null;
        if (tex != null && xrayMats.TryGetValue(tex, out var cached) && cached != null)
        {
            cached.SetColor("_Color", playerXrayColor);
            return cached;
        }

        var mat = new Material(xrayShader);
        mat.SetTexture("_MainTex", tex);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetColor("_Color", playerXrayColor);
        mat.renderQueue = 2601; // 主体之后，透明特效之前
        if (tex != null) xrayMats[tex] = mat;
        return mat;
    }

    Material LitMaterial(Dictionary<Texture, Material> cache, Texture tex, bool player)
    {
        if (spriteShader == null) return null;
        if (tex != null && cache.TryGetValue(tex, out var cached) && cached != null)
            return cached;

        var mat = new Material(spriteShader);
        mat.SetTexture("_MainTex", tex);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_AmbientBoost", player ? playerAmbient : propAmbient);
        mat.SetColor("_Color", player ? playerTint : Color.white);
        mat.SetFloat("_OutlineWidth", 0f);
        mat.SetColor("_OutlineColor", Color.clear);

        if (player)
        {
            // 主体走正常深度：被墙面/障碍挡住时不再整块盖在墙上。
            // 被大怪贴脸或走位到前景物体后面时，由 XRayMaterial 的低透明度剪影补可见性。
            mat.SetInt("_ZTest", 4);   // LEqual
            mat.SetInt("_ZWrite", 1);
            mat.renderQueue = 2600;
        }
        else
        {
            mat.SetInt("_ZTest", 4);   // LEqual
            mat.SetInt("_ZWrite", 1);
        }

        if (tex != null) cache[tex] = mat;
        return mat;
    }

    GameObject CreateBrazierFlame(GameObject parent)
    {
        var go = new GameObject("BrazierFlame");
        go.transform.SetParent(parent.transform, false);
        // 火盆网格顶部约 1.2 个本地单位；父物体的 scale 会把这个高度一起放大
        go.transform.localPosition = new Vector3(0f, 1.18f, 0f);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = flameQuad;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = flameMaterial;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        go.AddComponent<Hd2dBrazierFlame>();
        return go;
    }

    static Mesh CreateFlameQuad()
    {
        var m = new Mesh { name = "FlameQuad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
            new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f),
        };
        m.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.1f));
        return m;
    }

    static Texture2D CreateFlameTexture()
    {
        const int w = 48;
        const int h = 96;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "BrazierFlameTex",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var pixels = new Color32[w * h];
        var core = new Color(1f, 0.95f, 0.62f);
        var edge = new Color(1f, 0.28f, 0.04f);

        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1); // 0 = 火根，1 = 火尖
            float widthAt = 0.62f * Mathf.Pow(1f - v, 0.75f) + 0.05f;

            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float d = Mathf.Abs(u) / widthAt;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                a *= Mathf.Clamp01((1f - v) * 1.4f); // 火尖收细
                a *= Mathf.Clamp01(v * 10f);         // 火根柔和

                Color c = Color.Lerp(core, edge, Mathf.Clamp01(d));
                pixels[y * w + x] = new Color(c.r, c.g, c.b, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }
}
