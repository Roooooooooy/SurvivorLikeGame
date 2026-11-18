using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RpgGame;

// 宝箱/掉落物的收集特效：
// 物品图标从箱子上方升起、缩小并淡出，同时一圈加色粒子向外消散。
// 装备没有 icon 时，自动从装备图集取南向帧作为图标；仍然没有就只播放光晕。
public class LootCollectEffect : MonoBehaviour
{
    const int FrameSize = 64;
    const float Duration = 0.60f;

    readonly List<Sprite> ownedSprites = new List<Sprite>();

    SpriteRenderer iconRenderer;
    SpriteRenderer glowRenderer;
    ParticleSystem particles;

    Material iconMaterial;
    Material glowMaterial;
    Material particleMaterial;
    Texture2D dotTexture;

    Color gold = new Color(1f, 0.86f, 0.50f, 1f);
    float elapsed;

    public static void Spawn(EquipItemData item, Vector3 worldPosition)
    {
        var go = new GameObject("LootCollectEffect");
        go.transform.position = worldPosition;
        go.AddComponent<LootCollectEffect>().Init(item);
    }

    void Init(EquipItemData item)
    {
        var additiveShader = Shader.Find("HD2D/AdditiveSprite");
        if (additiveShader == null) additiveShader = Shader.Find("Sprites/Default");

        dotTexture = CreateSoftDot();

        // ---- 物品图标 ----
        var icon = ResolveIcon(item);
        if (icon != null)
        {
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(transform, false);
            iconGo.AddComponent<BillboardSprite>();

            iconRenderer = iconGo.AddComponent<SpriteRenderer>();
            iconRenderer.sprite = icon;
            iconRenderer.shadowCastingMode = ShadowCastingMode.Off;
            iconRenderer.receiveShadows = false;
            iconRenderer.sortingOrder = 30;

            var iconShader = Shader.Find("Sprites/Default");
            if (iconShader == null) iconShader = additiveShader;
            if (iconShader != null)
            {
                iconMaterial = new Material(iconShader)
                {
                    name = "LootIcon (Runtime)",
                    hideFlags = HideFlags.HideAndDontSave,
                    mainTexture = icon.texture,
                };
                iconRenderer.sharedMaterial = iconMaterial;
            }

            iconRenderer.transform.localScale = Vector3.one * 0.7f;
        }

        // ---- 中心光晕 ----
        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(transform, false);
        glowGo.AddComponent<BillboardSprite>();

        glowRenderer = glowGo.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = MakeDotSprite();
        glowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false;
        glowRenderer.sortingOrder = 29;
        glowRenderer.color = gold;

        if (additiveShader != null)
        {
            glowMaterial = new Material(additiveShader)
            {
                name = "LootGlow (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = dotTexture,
            };
            glowMaterial.SetColor("_Color", Color.white);
            glowMaterial.renderQueue = 3000;
            glowRenderer.sharedMaterial = glowMaterial;
        }

        SetupParticles(additiveShader);
        if (particles != null)
        {
            particles.Play();
            particles.Emit(24);
        }
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Duration);
        float ease = 1f - Mathf.Pow(1f - t, 2f);

        if (iconRenderer != null)
        {
            iconRenderer.transform.localPosition = new Vector3(0f, Mathf.Lerp(0f, 0.9f, ease), 0f);
            float scale = Mathf.Lerp(0.7f, 0.22f, ease);
            iconRenderer.transform.localScale = Vector3.one * scale;

            var color = iconRenderer.color;
            color.a = 1f - t;
            iconRenderer.color = color;
        }

        if (glowRenderer != null)
        {
            float scale = Mathf.Lerp(0.7f, 1.8f, ease);
            glowRenderer.transform.localScale = Vector3.one * scale;

            var color = gold;
            color.a = (1f - t) * 0.85f;
            glowRenderer.color = color;
        }

        if (t >= 1f) Destroy(gameObject);
    }

    Sprite ResolveIcon(EquipItemData item)
    {
        if (item == null) return null;
        if (item.icon != null) return item.icon;
        if (!item.HasVisual) return null;

        var texture = Resources.Load<Texture2D>(item.visualRoot);
        if (texture == null || texture.width < FrameSize || texture.height < FrameSize)
            return null;

        int count = Mathf.Min(8, texture.width / FrameSize);
        int index = Mathf.Clamp(6, 0, count - 1); // 南向帧，正面最好认
        var sprite = Sprite.Create(
            texture,
            new Rect(index * FrameSize, 0f, FrameSize, FrameSize),
            new Vector2(0.5f, 0.5f),
            48f);
        sprite.name = item.id + "_loot";

        ownedSprites.Add(sprite);
        return sprite;
    }

    Sprite MakeDotSprite()
    {
        var sprite = Sprite.Create(
            dotTexture,
            new Rect(0f, 0f, dotTexture.width, dotTexture.height),
            new Vector2(0.5f, 0.5f),
            32f);
        sprite.name = "LootGlowDot";
        ownedSprites.Add(sprite);
        return sprite;
    }

    void SetupParticles(Shader additiveShader)
    {
        var go = new GameObject("Particles");
        go.transform.SetParent(transform, false);

        particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.duration = 0.6f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.55f;
        main.startSpeed = 0.85f;
        main.startSize = 0.12f;
        main.startColor = gold;
        main.gravityModifier = -0.25f;
        main.maxParticles = 64;

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.16f;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.25f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingOrder = 31;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (additiveShader != null)
        {
            particleMaterial = new Material(additiveShader)
            {
                name = "LootParticles (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = dotTexture,
            };
            particleMaterial.SetColor("_Color", Color.white);
            particleMaterial.renderQueue = 3000;
            renderer.sharedMaterial = particleMaterial;
        }
    }

    static Texture2D CreateSoftDot()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "LootSoftDot",
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

    void OnDestroy()
    {
        if (iconMaterial != null) Destroy(iconMaterial);
        if (glowMaterial != null) Destroy(glowMaterial);
        if (particleMaterial != null) Destroy(particleMaterial);
        if (dotTexture != null) Destroy(dotTexture);

        for (int i = 0; i < ownedSprites.Count; i++)
            if (ownedSprites[i] != null) Destroy(ownedSprites[i]);
        ownedSprites.Clear();
    }
}
