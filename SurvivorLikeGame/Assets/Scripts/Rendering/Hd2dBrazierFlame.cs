using UnityEngine;

// 火盆火焰：一个面向相机的加法混合 billboard + 一个暖色点光。
// 尺寸/亮度用两条不同频率的正弦叠加做闪烁，避免整排火盆同步跳动。
public class Hd2dBrazierFlame : MonoBehaviour
{
    public float width = 0.5f;
    public float height = 0.72f;
    [Range(0f, 0.5f)] public float flicker = 0.14f;

    public float lightIntensity = 1.0f;
    public float lightRange = 3.5f;
    public Color lightColor = new Color(1f, 0.58f, 0.22f);

    Camera cam;
    Light glow;
    float seed;

    void Awake()
    {
        cam = Camera.main;
        seed = Random.value * 20f;

        var go = new GameObject("Glow");
        go.transform.SetParent(transform, false);
        glow = go.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = lightColor;
        glow.range = lightRange;
        glow.intensity = lightIntensity;
        glow.shadows = LightShadows.None;
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCam = cam.transform.position - transform.position;
            if (toCam.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
        }

        float t = Time.time * 9f + seed;
        float f = 1f + Mathf.Sin(t) * flicker + Mathf.Sin(t * 2.37f) * flicker * 0.35f;
        transform.localScale = new Vector3(width * f, height * f, 1f);

        if (glow != null)
            glow.intensity = lightIntensity * (0.85f + 0.15f * Mathf.Sin(t * 1.63f));
    }
}
