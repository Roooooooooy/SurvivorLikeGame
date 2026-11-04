using UnityEngine;

// 伤害数字：世界空间 TextMesh 池化显示，订阅 CombatEvents.HitDetailed。
// 普通命中用暖白色，暴击更大且为金色；数字向上飘并淡出。
// 池对象在 Awake 一次性创建，运行时只更新文本/颜色/位置，不逐帧分配内存；
// 场景重载后由 RuntimeInitializeOnLoadMethod 自动重建，跟随当前场景销毁。
public class DamageNumbers : MonoBehaviour
{
    private const int PoolSize = 48;          // 同时可见上限，5000 压测时循环复用不扩容
    private const int NumberCacheSize = 4096; // 常用伤害值的字符串缓存（聚合后数值会变大）
    private const int SortingOrder = 100;

    // 文本世界尺寸：TextMesh 的 fontSize 控制字形精度，characterSize 控制世界大小
    private const int FontSize = 72;
    private const float CharacterSize = 0.1f;

    private const float NormalLife = 0.65f;
    private const float CritLife = 0.85f;
    private const float NormalScale = 1f;
    private const float CritScale = 1.45f;
    private const float NormalRise = 1.15f;
    private const float CritRise = 1.6f;

    private static DamageNumbers instance;
    private static readonly string[] numberCache = new string[NumberCacheSize];

    // 由 Hd2dSpriteBridge 打开：世界空间文字映射到 (x, 高度, y)，并面向相机
    public static bool Hd2d;

    [Header("节流 / 聚合")]
    [Tooltip("每帧最多生成的普通伤害数字；暴击单独计数，避免暴击被普通命中挤掉")]
    public int maxNormalPerFrame = 3;
    public int maxCritPerFrame = 4;
    [Tooltip("同一位置在该时间窗口内的连续命中会累加到同一个数字上")]
    public float mergeWindow = 0.12f;
    public float mergeRadius = 0.4f;
    [Tooltip("距离玩家超过该逻辑距离的命中不显示数字（0 = 不限制）")]
    public float maxDistance = 14f;

    public static DamageNumbers Instance
    {
        get
        {
            if (instance == null)
            {
                // 场景重载后旧实例已被销毁（Unity 的伪 null），这里重新创建
                var go = new GameObject("DamageNumbers");
                instance = go.AddComponent<DamageNumbers>();
            }
            return instance;
        }
    }

    // 每次场景加载后立即重建实例并重新订阅：
    // RuntimeInitializeOnLoadMethod 只在启动时执行一次，重开场景必须靠 sceneLoaded。
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

    private class Entry
    {
        public TextMesh mesh;
        public TextMesh shadow;   // 深色投影，提升亮背景下的可读性
        public Transform tr;
        public float age;
        public float life;
        public Vector3 origin;
        public Vector2 logicPos;
        public int damage;
        public bool crit;
        public float driftX;
        public float rise;
        public float baseScale;
        public Color color;
        public float lastAlpha;
        public bool active;
    }

    private Entry[] pool;
    private int next;
    private Color normalColor;
    private Color critColor;
    private Color shadowColor;
    private bool suppressed;
    private Camera cam;

    // 每帧生成预算（用 frameCount 判断，避免依赖 Update 的执行顺序）
    private int budgetFrame = -1;
    private int spawnedNormal;
    private int spawnedCrit;

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
    }

    private void BuildPool()
    {
        var theme = UITheme.Current;
        var font = theme != null ? theme.BodyFont : null;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 普通命中：偏暖的白；暴击：主题金色（主题缺失时用硬编码兜底）
        normalColor = theme != null ? theme.parchment : new Color(0.95f, 0.93f, 0.88f, 1f);
        critColor = theme != null ? theme.goldHi : new Color(0.99f, 0.83f, 0.37f, 1f);
        shadowColor = new Color(0.03f, 0.02f, 0.02f, 0.85f);

        pool = new Entry[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("DamageNumber_" + i);
            go.transform.SetParent(transform, false);

            var mr = go.AddComponent<MeshRenderer>();
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = FontSize;
            mesh.characterSize = CharacterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = normalColor;
            mesh.text = string.Empty;

            // 动态字体材质：颜色/透明度走顶点色，不需要每个数字一份材质
            mr.sharedMaterial = font.material;
            mr.sortingOrder = SortingOrder;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // 深色投影：亮地面/技能特效上也能读清数字
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(go.transform, false);
            shadowGo.transform.localPosition = new Vector3(0.03f, -0.03f, 0.01f);
            var shadowMesh = shadowGo.AddComponent<TextMesh>();
            shadowMesh.font = font;
            shadowMesh.fontSize = FontSize;
            shadowMesh.characterSize = CharacterSize;
            shadowMesh.anchor = TextAnchor.MiddleCenter;
            shadowMesh.alignment = TextAlignment.Center;
            shadowMesh.color = shadowColor;
            shadowMesh.text = string.Empty;
            var shadowMr = shadowGo.GetComponent<MeshRenderer>();
            shadowMr.sharedMaterial = font.material;
            shadowMr.sortingOrder = SortingOrder - 1;
            shadowMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shadowMr.receiveShadows = false;

            go.SetActive(false);
            pool[i] = new Entry
            {
                mesh = mesh,
                shadow = shadowMesh,
                tr = go.transform,
                color = normalColor,
                lastAlpha = 1f,
            };
        }
    }

    // 显示在武器/弹体的命中位置；crit 决定尺寸、颜色与飘动幅度
    private void OnHitDetailed(Vector2 pos, int damage, bool crit)
    {
        // 升级/结算弹窗打开时不生成，避免世界空间文字盖住 ScreenSpaceCamera 画布上的 UI
        if (IsUiBlocking()) return;

        // 远处的命中不显示：镜头看不到的位置只会制造噪声
        if (maxDistance > 0f && PlayerController.Instance != null)
        {
            Vector2 playerPos = PlayerController.Instance.transform.position;
            if ((pos - playerPos).sqrMagnitude > maxDistance * maxDistance) return;
        }

        // 同一位置附近的连续命中先合并：不占池、不占每帧预算，也能避免数字叠成一团
        if (TryMerge(pos, damage, crit)) return;

        RefreshBudget();
        if (crit)
        {
            if (spawnedCrit >= maxCritPerFrame) return;
            spawnedCrit++;
        }
        else
        {
            if (spawnedNormal >= maxNormalPerFrame) return;
            spawnedNormal++;
        }

        var e = TakeEntry();

        e.age = 0f;
        e.life = crit ? CritLife : NormalLife;
        e.baseScale = crit ? CritScale : NormalScale;
        e.rise = crit ? CritRise : NormalRise;
        e.driftX = Random.Range(-0.22f, 0.22f);
        e.damage = damage;
        e.crit = crit;
        e.logicPos = pos;
        if (Hd2d)
        {
            // 逻辑命中点 (x,y) 映射到世界 (x, 0, y)，文字浮在角色上方
            e.origin = new Vector3(
                pos.x + Random.Range(-0.2f, 0.2f),
                1.0f + Random.Range(0f, 0.2f),
                pos.y + Random.Range(-0.2f, 0.2f));
        }
        else
        {
            e.origin = new Vector3(
                pos.x + Random.Range(-0.2f, 0.2f),
                pos.y + Random.Range(0.15f, 0.4f),
                -1f); // 比 z=0 的玩家/宝石更靠近 z=-10 的相机，保证画在前方
        }
        e.color = crit ? critColor : normalColor;
        e.lastAlpha = 1f;
        e.active = true;

        e.tr.position = e.origin;
        e.tr.localScale = Vector3.one * e.baseScale;
        e.mesh.text = FormatNumber(damage);
        if (e.shadow != null)
        {
            e.shadow.text = e.mesh.text;
            e.shadow.color = shadowColor;
        }
        e.mesh.color = e.color;
        e.mesh.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (pool == null) return;

        // 弹窗（升级/结算）打开时整体隐藏；关闭后新命中恢复正常显示
        if (IsUiBlocking())
        {
            if (!suppressed)
            {
                suppressed = true;
                HideAll();
            }
            return;
        }
        suppressed = false;

        // 顿帧/暂停期间数字仍然飘动淡出，因此使用不受 timeScale 影响的 deltaTime
        float dt = Time.unscaledDeltaTime;
        if (Hd2d && cam == null) cam = Camera.main;

        for (int i = 0; i < pool.Length; i++)
        {
            var e = pool[i];
            if (!e.active) continue;

            e.age += dt;
            float k = e.life > 0f ? e.age / e.life : 1f;
            if (k >= 1f)
            {
                e.active = false;
                e.mesh.gameObject.SetActive(false);
                continue;
            }

            // 先快后慢的上升 + 轻微横向漂移
            float eased = 1f - (1f - k) * (1f - k);
            if (Hd2d)
            {
                e.tr.position = new Vector3(
                    e.origin.x + e.driftX * eased,
                    e.origin.y + e.rise * eased,
                    e.origin.z);

                if (cam != null)
                {
                    Vector3 dir = e.tr.position - cam.transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f)
                    {
                        // TextMesh 正面朝本地 -Z；LookRotation(dir) 已经让它面向相机，
                        // 再乘 180° 会看到背面（文字镜像）。
                        e.tr.rotation = Quaternion.LookRotation(dir, Vector3.up);
                    }
                }
            }
            else
            {
                e.tr.position = new Vector3(
                    e.origin.x + e.driftX * eased,
                    e.origin.y + e.rise * eased,
                    e.origin.z);
            }

            // 生成瞬间轻微放大回弹
            float pop = 1f + 0.3f * Mathf.Max(0f, 1f - e.age / 0.08f);
            e.tr.localScale = Vector3.one * (e.baseScale * pop);

            // 后 40% 生命周期淡出
            float alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            if (alpha != e.lastAlpha)
            {
                e.lastAlpha = alpha;
                var c = e.color;
                c.a = alpha;
                e.mesh.color = c;
                if (e.shadow != null)
                {
                    var sc = shadowColor;
                    sc.a = shadowColor.a * alpha;
                    e.shadow.color = sc;
                }
            }
        }
    }

    // 重置本帧生成预算；frameCount 变化说明进入了新帧
    private void RefreshBudget()
    {
        if (Time.frameCount == budgetFrame) return;
        budgetFrame = Time.frameCount;
        spawnedNormal = 0;
        spawnedCrit = 0;
    }

    // 把命中累加到窗口内最近的同类数字上。返回 true 表示已合并，不需要新开一条。
    private bool TryMerge(Vector2 pos, int damage, bool crit)
    {
        if (pool == null) return false;

        Entry best = null;
        float bestDist = mergeRadius * mergeRadius;
        for (int i = 0; i < pool.Length; i++)
        {
            var e = pool[i];
            if (!e.active || e.crit != crit) continue;
            if (e.age > mergeWindow) continue;

            float d = (e.logicPos - pos).sqrMagnitude;
            if (d > bestDist) continue;
            bestDist = d;
            best = e;
        }
        if (best == null) return false;

        best.damage += damage;
        best.mesh.text = FormatNumber(best.damage);
        if (best.shadow != null) best.shadow.text = best.mesh.text;
        // 让仍在飘的数字回到接近起点并保持满透明度；不重播弹跳
        best.age = Mathf.Min(best.age, Mathf.Max(0.02f, mergeWindow * 0.5f));
        best.lastAlpha = 1f;
        var c = best.color;
        c.a = 1f;
        best.mesh.color = c;
        if (best.shadow != null) best.shadow.color = shadowColor;
        return true;
    }

    // 升级/结算弹窗是否正在挡住游戏画面
    private static bool IsUiBlocking()
    {
        var gm = GameManager.Instance;
        if (gm == null) return false;
        if (gm.isGameOver) return true;
        return gm.levelUpPanel != null && gm.levelUpPanel.activeInHierarchy;
    }

    private void HideAll()
    {
        for (int i = 0; i < pool.Length; i++)
        {
            var e = pool[i];
            if (!e.active) continue;
            e.active = false;
            e.mesh.gameObject.SetActive(false);
        }
    }

    // 从池中取一个空闲项；池满时覆盖年龄最大的（最早生成的）数字
    private Entry TakeEntry()
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

    // 伤害值字符串缓存：只在首次遇到某个数值时分配，常见伤害全程零 GC
    private static string FormatNumber(int value)
    {
        if (value >= 0 && value < NumberCacheSize)
        {
            var cached = numberCache[value];
            if (cached == null)
            {
                cached = value.ToString();
                numberCache[value] = cached;
            }
            return cached;
        }
        return value.ToString();
    }
}
