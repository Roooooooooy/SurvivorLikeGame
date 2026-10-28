using UnityEngine;
using UnityEngine.SceneManagement;

// 全局屏幕后处理：暗角、胶片颗粒、扫描线、轻微色差与暖色调。
// 场景加载后自动挂到 Camera.main；跟随场景销毁，重开场景时由 sceneLoaded 重新挂载。
// 直接在 Inspector 取消勾选本组件即可关闭后处理（OnRenderImage 不再执行）。
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraPostFX : MonoBehaviour
{
    [Header("画面后处理")]
    [Tooltip("暗角强度：0 关闭，越大四角越暗")]
    [Range(0f, 1f)] public float vignette = 0.35f;

    [Tooltip("胶片颗粒强度")]
    [Range(0f, 0.3f)] public float grain = 0.06f;

    [Tooltip("扫描线强度")]
    [Range(0f, 0.3f)] public float scanlines = 0.04f;

    [Tooltip("色差像素偏移：中心干净，边缘轻微分离")]
    [Range(0f, 0.005f)] public float aberration = 0.0008f;

    [Tooltip("整体色调：默认轻微暖色，接近烛光")]
    public Color tint = new Color(1f, 0.97f, 0.9f, 1f);

    // 属性 ID 缓存，避免每帧字符串查找
    static readonly int VignetteId = Shader.PropertyToID("_Vignette");
    static readonly int GrainId = Shader.PropertyToID("_Grain");
    static readonly int ScanlinesId = Shader.PropertyToID("_Scanlines");
    static readonly int AberrationId = Shader.PropertyToID("_Aberration");
    static readonly int TintId = Shader.PropertyToID("_Tint");
    static readonly int TimeSeedId = Shader.PropertyToID("_TimeSeed");

    Material material;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHooks()
    {
        // 关闭 Domain Reload 时先清掉上一次 Play 留下的静态订阅
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToMainCamera();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 场景重载后相机会重建，这里补挂；不做 DontDestroyOnLoad
        AttachToMainCamera();
    }

    static void AttachToMainCamera()
    {
        var cam = Camera.main;
        if (cam == null) return; // 无主相机时跳过，不报错

        if (cam.GetComponent<CameraPostFX>() != null) return;
        cam.gameObject.AddComponent<CameraPostFX>();
    }

    void Awake()
    {
        var shader = Shader.Find("Hidden/ScreenFX");
#if UNITY_EDITOR
        // 兜底：Resources 索引或导入顺序异常时直接按路径取
        if (shader == null)
            shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Shaders/ScreenFX.shader");
#endif
        if (shader == null)
        {
            Debug.LogWarning("[CameraPostFX] 找不到 ScreenFX.shader，后处理已跳过。");
            enabled = false;
            return;
        }

        material = new Material(shader)
        {
            name = "ScreenFX (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        material = null;
    }

    // 参数每帧写入材质；不创建临时对象，不产生每帧 GC
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        material.SetFloat(VignetteId, vignette);
        material.SetFloat(GrainId, grain);
        material.SetFloat(ScanlinesId, scanlines);
        material.SetFloat(AberrationId, aberration);
        material.SetColor(TintId, tint);
        // 颗粒逐帧变化；用 unscaledTime 保证暂停/结算界面也有细微呼吸感
        material.SetFloat(TimeSeedId, Time.unscaledTime % 1024f);

        Graphics.Blit(source, destination, material);
    }
}