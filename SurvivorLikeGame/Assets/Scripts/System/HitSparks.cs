using UnityEngine;

// 命中火花：订阅 CombatEvents.HitDetailed，在命中点生成短暂的四角星。
// - 池化，运行时只更新位置/缩放/颜色，不逐帧分配；
// - HD-2D 下映射到世界 (x, 高度, y) 并面向相机；
// - 每帧有生成预算 + 距离限制，避免多段武器把画面刷满。
public class HitSparks : MonoBehaviour
{
    private const int PoolSize = 24;
    private const int MaxPerFrame = 6;
    private const float MaxDistance = 14f;

    private static HitSparks instance;
    public static HitSparks Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("HitSparks");
                instance = go.AddComponent<HitSparks>();
            }
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        _ = Instance;
    }

    private class Spark
    {
        public GameObject go;
        public Transform tr;
        public SpriteRenderer sr;
        public float age;
        public float life;
        public float startScale;
        public float endScale;
        public float angle;
        public float spin;
        public Color color;
        public bool active;
    }

    private Spark[] pool;
    private int next;
    private Camera cam;
    private Material material;
    private Texture2D texture;
    private Sprite sprite;
    private int budgetFrame = -1;
    private int spawned;

    private static readonly Color NormalColor = new Color(1f, 0.80f, 0.50f, 0.95f);
    private static readonly Color CritColor = new Color(1f, 0.72f, 0.28f, 1f);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        BuildPool();
    }

    private void OnEnable() => CombatEvents.HitDetailed += OnHitDetailed;
    private void OnDisable() => CombatEvents.HitDetailed -= OnHitDetailed;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (material != null) Destroy(material);
        if (texture != null) Destroy(texture);
    }

    private void BuildPool()
    {
        var shader = Shader.Find("HD2D/AdditiveSprite");
        if (shader == null)
        {
            Debug.LogWarning("[HitSparks] 找不到 HD2D/AdditiveSprite，命中火花已跳过");
            enabled = false;
            return;
        }

        texture = CreateSparkTexture();
        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), texture.width);
        material = new Material(shader)
        {
            name = "HitSpark (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
        };
        material.mainTexture = texture;
        material.SetColor("_Color", Color.white);
        material.renderQueue = 3000;

        pool = new Spark[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("Spark_" + i);
            go.transform.SetParent(transform, false);
            go.AddComponent<Hd2dProxyMarker>(); // 避免被 HD-2D 视觉桥二次代理

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.sortingOrder = 95;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.enabled = false;

            go.SetActive(false);
            pool[i] = new Spark { go = go, tr = go.transform, sr = sr };
        }
    }

    private void OnHitDetailed(Vector2 pos, int damage, bool crit)
    {
        if (pool == null || IsUiBlocking()) return;

        if (PlayerController.Instance != null)
        {
            Vector2 p = PlayerController.Instance.transform.position;
            if ((pos - p).sqrMagnitude > MaxDistance * MaxDistance) return;
        }

        if (Time.frameCount != budgetFrame)
        {
            budgetFrame = Time.frameCount;
            spawned = 0;
        }
        if (spawned >= MaxPerFrame) return;
        spawned++;

        bool hd2d = Hd2dSpriteBridge.Active;
        var s = TakeSpark();
        s.age = 0f;
        s.life = crit ? 0.34f : 0.22f;
        s.startScale = crit ? 0.60f : 0.40f;
        s.endScale = crit ? 1.40f : 0.85f;
        s.angle = Random.Range(0f, 360f);
        s.spin = Random.Range(-220f, 220f);
        s.color = crit ? CritColor : NormalColor;

        Vector3 world;
        if (hd2d)
        {
            world = new Vector3(
                pos.x + Random.Range(-0.14f, 0.14f),
                0.75f + Random.Range(-0.08f, 0.16f),
                pos.y + Random.Range(-0.14f, 0.14f));

            // 火花和敌人 billboard 在同一深度会被敌人自身遮住：沿视线往相机挪一点
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - world;
                if (toCam.sqrMagnitude > 0.0001f)
                    world += toCam.normalized * 0.45f;
            }
        }
        else
        {
            world = new Vector3(pos.x, pos.y + Random.Range(-0.08f, 0.08f), -1f);
        }

        s.tr.position = world;

        s.tr.localScale = Vector3.one * s.startScale;
        s.sr.color = s.color;
        s.sr.enabled = true;
        s.go.SetActive(true);
        s.active = true;
    }

    private Spark TakeSpark()
    {
        for (int i = 0; i < pool.Length; i++)
        {
            int index = (next + i) % pool.Length;
            if (!pool[index].active)
            {
                next = (index + 1) % pool.Length;
                return pool[index];
            }
        }

        int oldest = next;
        for (int i = 1; i < pool.Length; i++)
        {
            int index = (next + i) % pool.Length;
            if (pool[index].age > pool[oldest].age) oldest = index;
        }
        next = (oldest + 1) % pool.Length;
        return pool[oldest];
    }

    private void Update()
    {
        if (pool == null) return;

        bool hd2d = Hd2dSpriteBridge.Active;
        if (hd2d && cam == null) cam = Camera.main;

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < pool.Length; i++)
        {
            var s = pool[i];
            if (!s.active) continue;

            s.age += dt;
            float k = s.life > 0f ? s.age / s.life : 1f;
            if (k >= 1f)
            {
                s.active = false;
                s.sr.enabled = false;
                s.go.SetActive(false);
                continue;
            }

            float eased = 1f - (1f - k) * (1f - k);
            float scale = Mathf.Lerp(s.startScale, s.endScale, eased);
            s.tr.localScale = Vector3.one * scale;

            var c = s.color;
            c.a = s.color.a * (1f - k * k);
            s.sr.color = c;

            s.angle += s.spin * dt;
            if (hd2d && cam != null)
            {
                Vector3 dir = s.tr.position - cam.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    s.tr.rotation = Quaternion.LookRotation(dir, Vector3.up)
                        * Quaternion.Euler(0f, 0f, s.angle);
            }
            else
            {
                s.tr.rotation = Quaternion.Euler(0f, 0f, s.angle);
            }
        }
    }

    private static bool IsUiBlocking()
    {
        var gm = GameManager.Instance;
        if (gm == null) return false;
        if (gm.isGameOver) return true;
        return gm.levelUpPanel != null && gm.levelUpPanel.activeInHierarchy;
    }

    private static Texture2D CreateSparkTexture()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "HitSparkTex",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var pixels = new Color32[size * size];
        const float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x - center) / center;
                float v = (y - center) / center;

                // 中心柔光
                float d = Mathf.Sqrt(u * u + v * v);
                float core = Mathf.Clamp01(1f - d * 1.7f);
                core = core * core * (3f - 2f * core);

                // 四个方向的细长尖刺
                float ax = Mathf.Abs(u);
                float ay = Mathf.Abs(v);
                float spike = Mathf.Max(0f, 1f - ax * 9f) * Mathf.Max(0f, 1f - ay)
                            + Mathf.Max(0f, 1f - ay * 9f) * Mathf.Max(0f, 1f - ax);

                float a = Mathf.Clamp01(core + spike * 0.85f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }
}
