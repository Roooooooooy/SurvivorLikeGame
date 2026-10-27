using UnityEngine;

// 音效统一入口：懒加载单例 + AudioSource 播放池。
// 玩法层只通过 CombatEvents 发事件，本类负责订阅并播放占位音效；
// 玩家受击/升级、武器发射这类表现明确的节点也可以直接调用 PlayXxx 便捷方法。
// TODO: 音量目前是 masterVolume * sfxVolume 的简易相乘，
//       后续接入 AudioMixer（Master / SFX Group）时替换为 mixer.SetFloat。
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<AudioManager>();
                if (instance == null)
                {
                    // 跟随当前场景创建，不做 DontDestroyOnLoad；重开场景时由新场景重新创建
                    var go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                }
            }
            return instance;
        }
    }

    [Header("音量")]
    [Range(0f, 1f)] public float masterVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 0.7f;

    [Header("占位音效（留空时自动从 Resources/Audio/SFX 加载）")]
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private AudioClip killClip;
    [SerializeField] private AudioClip pickupClip;
    [SerializeField] private AudioClip levelUpClip;
    [SerializeField] private AudioClip hurtClip;
    [SerializeField] private AudioClip shootClip;

    // 命中音效全局节流，避免持续伤害/多段伤害把播放池刷满
    private const float HitMinInterval = 0.04f;
    private const int PoolSize = 10;

    private static AudioManager instance;
    private AudioSource[] pool;
    private int nextSource;
    private float lastHitTime = -1f;

    // ---- 验证用只读计数（不参与播放逻辑） ----
    public int TotalPlayCount { get; private set; }
    public int HitPlayCount { get; private set; }
    public int KillPlayCount { get; private set; }
    public int PickupPlayCount { get; private set; }
    public int LevelUpPlayCount { get; private set; }
    public int HurtPlayCount { get; private set; }
    public int ShootPlayCount { get; private set; }
    public string LastClipName { get; private set; }

    // 供自检使用：占位音效是否全部加载成功、播放池容量
    public bool HasAllClips =>
        hitClip != null && killClip != null && pickupClip != null &&
        levelUpClip != null && hurtClip != null && shootClip != null;

    public int PoolCapacity => pool != null ? pool.Length : 0;

    public int ActiveSourceCount
    {
        get
        {
            if (pool == null) return 0;
            int count = 0;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i] != null && pool[i].isPlaying) count++;
            return count;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        BuildPool();
        LoadClips();
    }

    void OnEnable()
    {
        CombatEvents.Hit += OnHit;
        CombatEvents.Kill += OnKill;
        CombatEvents.Pickup += OnPickup;
    }

    void OnDisable()
    {
        CombatEvents.Hit -= OnHit;
        CombatEvents.Kill -= OnKill;
        CombatEvents.Pickup -= OnPickup;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // 播放池只建一次，之后所有播放复用，避免 Update 中反复 new AudioSource
    void BuildPool()
    {
        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("SfxSource_" + i);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.spatialBlend = 1f; // 事件携带世界坐标，用 3D 衰减区分远近
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 30f;
            pool[i] = source;
        }
    }

    void LoadClips()
    {
        if (hitClip == null) hitClip = LoadClip("hit");
        if (killClip == null) killClip = LoadClip("kill");
        if (pickupClip == null) pickupClip = LoadClip("pickup");
        if (levelUpClip == null) levelUpClip = LoadClip("levelup");
        if (hurtClip == null) hurtClip = LoadClip("hurt");
        if (shootClip == null) shootClip = LoadClip("shoot");
    }

    static AudioClip LoadClip(string name)
    {
        // 编辑器与打包统一走 Resources，保证构建后也有声音。
        var clip = Resources.Load<AudioClip>("Audio/SFX/" + name);
        if (clip != null) return clip;
#if UNITY_EDITOR
        // 兜底：Resources 索引尚未刷新时直接按路径读
        return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Resources/Audio/SFX/" + name + ".wav");
#else
        return null;
#endif
    }

    // 通用播放：从池中取空闲（或轮转抢占）的 AudioSource，不新建对象
    public void Play(AudioClip clip, Vector2 pos, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || pool == null) return;

        var source = TakeSource();
        source.Stop();
        source.transform.position = new Vector3(pos.x, pos.y, 0f);
        source.clip = clip;
        source.pitch = pitch;
        source.volume = Mathf.Clamp01(masterVolume * sfxVolume * volume);
        source.Play();

        TotalPlayCount++;
        LastClipName = clip.name;
    }

    AudioSource TakeSource()
    {
        for (int i = 0; i < pool.Length; i++)
        {
            int index = (nextSource + i) % pool.Length;
            if (!pool[index].isPlaying)
            {
                nextSource = (index + 1) % pool.Length;
                return pool[index];
            }
        }

        // 全部在播时轮转抢占最早的一路，池子大小恒定
        var fallback = pool[nextSource];
        nextSource = (nextSource + 1) % pool.Length;
        return fallback;
    }

    // ---------------- 便捷播放接口（事件与外部调用共用） ----------------

    public void PlayHit(Vector2 pos)
    {
        if (hitClip == null) return;
        Play(hitClip, pos, 1f, Random.Range(0.92f, 1.08f));
        HitPlayCount++;
    }

    public void PlayKill(Vector2 pos)
    {
        if (killClip == null) return;
        Play(killClip, pos, 1f, Random.Range(0.9f, 1.1f));
        KillPlayCount++;
    }

    public void PlayPickup(Vector2 pos)
    {
        if (pickupClip == null) return;
        Play(pickupClip, pos, 0.9f, Random.Range(0.95f, 1.05f));
        PickupPlayCount++;
    }

    public void PlayLevelUp(Vector2 pos)
    {
        if (levelUpClip == null) return;
        Play(levelUpClip, pos, 1f, 1f);
        LevelUpPlayCount++;
    }

    public void PlayHurt(Vector2 pos)
    {
        if (hurtClip == null) return;
        Play(hurtClip, pos, 1f, Random.Range(0.9f, 1f));
        HurtPlayCount++;
    }

    public void PlayShoot(Vector2 pos)
    {
        if (shootClip == null) return;
        Play(shootClip, pos, 0.7f, Random.Range(0.95f, 1.08f));
        ShootPlayCount++;
    }

    // ---------------- CombatEvents 订阅 ----------------

    void OnHit(Vector2 pos, int damage)
    {
        // 持续伤害/多段伤害会高频触发，全局最短间隔节流
        if (Time.unscaledTime - lastHitTime < HitMinInterval) return;
        lastHitTime = Time.unscaledTime;
        PlayHit(pos);
    }

    void OnKill(Vector2 pos, int expValue) => PlayKill(pos);

    void OnPickup(Vector2 pos, int expValue) => PlayPickup(pos);
}
