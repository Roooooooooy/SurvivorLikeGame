using UnityEngine;
using UnityEngine.Rendering;

// 主角跑步时的脚步尘土。
// 采用和 EmberParticles 相同的方案：发射器跟随角色、粒子世界空间模拟、
// billboard 渲染；停下时把 emission rate 设为 0，已经产生的尘土留在原地。
public class Hd2dFootDust : MonoBehaviour
{
    ParticleSystem ps;
    ParticleSystemRenderer psr;
    Material material;
    Texture2D texture;

    bool emitting;
    Color color = new Color(0.90f, 0.86f, 0.78f, 0.42f);
    float size = 0.38f;
    float rate = 22f;

    void Awake()
    {
        texture = CreateSoftDot();

        // Alpha Blended 版在当前 URP 下不显示；用项目里余烬已验证可渲染的 Additive。
        var shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) shader = Shader.Find("Particles/Additive");
        if (shader == null) shader = Shader.Find("HD2D/AdditiveSprite");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        material = new Material(shader)
        {
            name = "FootDust (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = texture,
        };

        ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;              // 保持播放，发射器才持续工作
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.42f;
        main.startSpeed = 0.18f;
        main.startSize = size;
        main.startColor = color;
        main.gravityModifier = -0.04f; // 负值 = 缓慢上飘
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;    // 由 SetEmitting 控制

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.3f, 0.1f, 0.2f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.75f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(1f, 1.45f)));

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.10f, 0.30f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.14f, 0.14f);

        psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.alignment = ParticleSystemRenderSpace.View;
        psr.sortingOrder = 20; // 画在角色/地面之上，避免被排序和阴影遮挡
        psr.maxParticleSize = 1.2f;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
        psr.sharedMaterial = material;

        ps.Play();
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (texture != null) Destroy(texture);
    }

    public void Configure(Color c, float s, float r)
    {
        color = c;
        size = s;
        rate = r;
        if (ps == null) return;

        var main = ps.main;
        main.startColor = c;
        main.startSize = s;

        var shape = ps.shape;
        shape.scale = new Vector3(s * 0.8f, 0.1f, s * 0.55f);
    }

    // 每帧由 Hd2dPixelPlayer 调用：移动发射器到脚下，并控制是否发射。
    public void SetEmitting(bool on, Vector3 feetPos)
    {
        if (ps == null) return;
        transform.position = feetPos;

        if (emitting == on) return;
        emitting = on;
        var emission = ps.emission;
        emission.rateOverTime = on ? rate : 0f;
    }

    // 转向/急停时的一次性小尘土爆发。
    public void Burst(Vector3 pos, int count, float sizeScale = 1f)
    {
        if (ps == null) return;

        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(
                    pos.x + Random.Range(-0.12f, 0.12f),
                    0.05f,
                    pos.z + Random.Range(-0.12f, 0.12f)),
                velocity = new Vector3(
                    dir.x * Random.Range(0.35f, 0.85f),
                    Random.Range(0.15f, 0.35f),
                    dir.y * Random.Range(0.35f, 0.85f)),
                startColor = color,
                startSize = size * sizeScale * Random.Range(0.8f, 1.2f),
                startLifetime = 0.32f,
            };
            ps.Emit(emit, 1);
        }
    }

    static Texture2D CreateSoftDot()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "FootDustDot",
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
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d * 1.1f);
                a = a * a * (3f - 2f * a);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
