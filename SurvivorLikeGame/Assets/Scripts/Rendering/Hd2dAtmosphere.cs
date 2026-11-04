using UnityEngine;
using UnityEngine.SceneManagement;

// HD-2D 氛围补光：在原有暖色主光之外补一盏无阴影的冷色辅光，
// 让地面/低模道具的暗部偏冷、角色轮廓更清楚。
// 只在 HD-2D 场景（存在 Hd2dSpriteBridge）里自动创建，不影响其他场景。
[DefaultExecutionOrder(200)]
public class Hd2dAtmosphere : MonoBehaviour
{
    public Color fillColor = new Color(0.50f, 0.62f, 0.85f);
    public float fillIntensity = 0.32f;
    public Vector3 fillEuler = new Vector3(35f, 205f, 0f);

    public bool tuneAmbient = true;
    public Color ambient = new Color(0.40f, 0.44f, 0.52f);

    [Tooltip("0 = 不改雾；其余值为 ExponentialSquared 密度")]
    public float fogDensity = 0.010f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHooks()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CreateIfNeeded();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CreateIfNeeded();
    }

    static void CreateIfNeeded()
    {
        if (!Hd2dSpriteBridge.Active) return;
        if (FindFirstObjectByType<Hd2dAtmosphere>() != null) return;
        var go = new GameObject("Hd2dAtmosphere");
        go.AddComponent<Hd2dAtmosphere>();
    }

    void Awake()
    {
        var go = new GameObject("HD2D_CoolFill");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(fillEuler);

        var fill = go.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = fillColor;
        fill.intensity = fillIntensity;
        fill.shadows = LightShadows.None;

        if (tuneAmbient)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
        }

        if (fogDensity > 0f)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
        }
    }
}
