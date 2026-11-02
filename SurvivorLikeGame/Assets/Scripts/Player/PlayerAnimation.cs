using UnityEngine;

// 主角动画：从 Resources 的横向精灵表在运行时切片，按移动状态播放待机/行走。
// 不修改 prefab 结构，也不接管 flipX（朝向仍由 PlayerController 写入）。
[DisallowMultipleComponent]
public class PlayerAnimation : MonoBehaviour
{
    [Header("精灵表")]
    public string sheetPath = "Player/player_cat_sheet";
    public int frameCount = 4;
    public int frameWidth = 64;
    public int frameHeight = 48;
    public float pixelsPerUnit = 48f;
    [Tooltip("脚底在帧内的像素行（从顶部算）。枢轴放在这里，角色原点即落脚点。")]
    public float groundLine = 46f;

    [Header("节奏")]
    public int idleStart = 0;
    public int idleEnd = 0;
    public float idleFps = 2.5f;
    public int walkStart = 0;
    public int walkEnd = 3;
    public float walkFps = 9f;

    SpriteRenderer sr;
    Sprite[] frames;
    float timer;
    int current;
    bool walking;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var player = PlayerController.Instance;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        if (player.GetComponent<PlayerAnimation>() != null) return;
        player.gameObject.AddComponent<PlayerAnimation>();
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        BuildFrames();
        if (frames != null && frames.Length > 0 && sr != null)
            sr.sprite = frames[idleStart];
    }

    void BuildFrames()
    {
        var tex = Resources.Load<Texture2D>(sheetPath);
        if (tex == null)
        {
            Debug.LogWarning($"[PlayerAnimation] 找不到精灵表 Resources/{sheetPath}");
            return;
        }

        int count = Mathf.Max(1, frameCount);
        frames = new Sprite[count];
        // 枢轴放在脚底线上：角色原点是落脚点，不会因为帧高变化上下跳
        float pivotY = frameHeight > 0 ? Mathf.Clamp01(groundLine / frameHeight) : 0.5f;
        var pivot = new Vector2(0.5f, pivotY);
        // Unity 纹理坐标原点在左下角，帧 0 在最上方
        for (int i = 0; i < count; i++)
        {
            var rect = new Rect(i * frameWidth, tex.height - frameHeight, frameWidth, frameHeight);
            frames[i] = Sprite.Create(tex, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            frames[i].name = $"player_{i}";
        }
    }

    void LateUpdate()
    {
        if (frames == null || frames.Length == 0 || sr == null) return;

        var rb = GetComponent<Rigidbody2D>();
        bool moving = false;
        if (rb != null)
            moving = rb.linearVelocity.sqrMagnitude > 0.01f;
        if (!moving && GameManager.Instance != null && GameManager.Instance.isGameOver)
            moving = false;

        float fps = moving ? walkFps : idleFps;
        int start = moving ? walkStart : idleStart;
        int end = moving ? walkEnd : idleEnd;
        int span = Mathf.Max(1, end - start + 1);

        if (moving != walking)
        {
            walking = moving;
            timer = 0f;
            current = 0;
        }

        timer += Time.unscaledDeltaTime;
        float step = 1f / Mathf.Max(0.01f, fps);
        while (timer >= step)
        {
            timer -= step;
            current = (current + 1) % span;
        }

        int index = Mathf.Clamp(start + current, 0, frames.Length - 1);
        if (sr.sprite != frames[index]) sr.sprite = frames[index];
    }
}