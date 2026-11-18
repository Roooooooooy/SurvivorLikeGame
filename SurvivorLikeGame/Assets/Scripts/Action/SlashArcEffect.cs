using UnityEngine;

namespace RpgGame
{
    public enum SlashStyle
    {
        Arc = 0,     // 暖金新月，横斩
        Wide = 1,    // 青蓝宽弧，回斩
        Thrust = 2,  // 紫白长刺，突刺
    }

    // 动作区攻击刀光。
    // 素材：OpenGameArt "Weapon Slash - Effect"（CC0），每套 5 行 × 6 帧。
    // 三段连招各取一行，颜色/形状/节奏都不同。
    //
    // HD-2D 下不经过 Hd2dSpriteBridge：桥会把精灵按“脚底贴地”抬高半个贴图高度，
    // 刀光会飘到头顶。这里直接把精灵放到 (x, 高度, y) 并每帧朝向相机。
    public class SlashArcEffect : MonoBehaviour
    {
        const int FrameCount = 6;
        const float PixelsPerUnit = 100f;

        // 三套帧：gold = Classic row 1，blue = Alternative 2 row 2，purple = Alternative 1 row 5。
        static readonly string[] StylePrefixes = { "gold", "blue", "purple" };

        // 贴图本地外缘半径（像素 → 世界单位，PPU = 100）。
        // 由每行 6 帧的 alpha 最大半径测得，用来把“刀光外缘”对齐到“判定框前缘”。
        static readonly float[] LocalRadii = { 0.5501f, 0.4689f, 0.7663f };

        // 每段播放的帧序列。原素材 6 帧是转满一圈，横斩只取半圈会更像“挥砍”而不是“转圈”。
        static readonly int[][] FrameOrder =
        {
            new[] { 1, 2, 3, 4 },             // Arc：半圈横扫
            new[] { 1, 2, 3, 4 },             // Wide：半圈回扫（flipY 反向）
            new[] { 0, 1, 2, 3, 4, 5 },       // Thrust：长刺完整伸展
        };

        static readonly Sprite[][] frameSprites = new Sprite[3][];
        static readonly Material[][] frameMaterials = new Material[3][];
        static Shader additiveShader;

        // 3D 预制体刀光。存在 Resources/FX/SlashVfxLibrary.asset 时优先使用；
        // 没有或某一档未配置 prefab 时，仍走上面的序列帧实现。
        static SlashVfxLibrary vfxLibrary;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static SlashVfxLibrary VfxLibrary
        {
            get
            {
                if (vfxLibrary == null)
                    vfxLibrary = Resources.Load<SlashVfxLibrary>("FX/SlashVfxLibrary");
                return vfxLibrary;
            }
        }

        public float lifetime = 0.18f;
        public Color color = Color.white;

        SpriteRenderer view;
        SpriteRenderer coreView;
        int[] sequence;
        Camera cam;
        SlashStyle style;
        float age;
        float attackAngle;
        bool hd2d;
        int frameIndex = -1;
        bool started;

        public static void Spawn(Vector3 position, Vector2 direction, Color color,
            float scale, SlashStyle style, float lifetime, bool flipY = false,
            float rotationOffset = 0f, float height = 0.85f)
        {
            var entry = VfxLibrary != null ? VfxLibrary.Get(style) : null;
            if (entry != null && entry.prefab != null)
            {
                SpawnPrefab(position, direction, color, scale, style, lifetime, flipY,
                    rotationOffset, height, entry);
                return;
            }

            var go = new GameObject("SlashArc");

            bool hd2d = Hd2dSpriteBridge.Active;
            go.transform.position = hd2d
                ? new Vector3(position.x, height, position.y)
                : new Vector3(position.x, position.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 20;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.flipY = flipY;

            var fx = go.AddComponent<SlashArcEffect>();
            fx.color = color;
            fx.style = style;
            fx.lifetime = Mathf.Max(0.03f, lifetime);
            fx.attackAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
            fx.hd2d = hd2d;
            fx.view = sr;
            fx.sequence = FrameOrder[Mathf.Clamp((int)style, 0, FrameOrder.Length - 1)];

            // 白色内核：同一帧再叠一层更小的加法精灵，让刀锋中心更亮、更像实体剑光。
            var coreGo = new GameObject("SlashCore");
            coreGo.transform.SetParent(go.transform, false);
            coreGo.transform.localPosition = Vector3.zero;
            coreGo.transform.localScale = Vector3.one * 0.62f;
            if (hd2d) coreGo.AddComponent<Hd2dProxyMarker>();

            var coreSr = coreGo.AddComponent<SpriteRenderer>();
            coreSr.sortingOrder = 21;
            coreSr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coreSr.receiveShadows = false;
            coreSr.flipY = flipY;
            fx.coreView = coreSr;

            go.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);
            fx.ApplyFrame(fx.sequence[0]);
            if (hd2d) fx.UpdateBillboard();
            else go.transform.rotation = Quaternion.Euler(0f, 0f, fx.attackAngle);

            // 阻止 HD-2D 桥再建一个“脚底贴地”的代理副本。
            if (hd2d) go.AddComponent<Hd2dProxyMarker>();
        }

        // 贴图本地半径（世界单位）。
        public static float GetLocalRadius(SlashStyle style)
        {
            var entry = VfxLibrary != null ? VfxLibrary.Get(style) : null;
            if (entry != null && entry.prefab != null && entry.localRadius > 0.001f)
                return entry.localRadius;

            int i = Mathf.Clamp((int)style, 0, LocalRadii.Length - 1);
            return LocalRadii[i];
        }

        public static bool IsPunch(SlashStyle style)
        {
            var entry = VfxLibrary != null ? VfxLibrary.Get(style) : null;
            return entry != null && entry.punchMode;
        }

        // 3D 预制体刀光：素材主粒子带 startRotationX = 90°（水平弧面）。
        // 这里把它抵消成竖直弧面，并沿用旧版的“绕 Y 朝相机 + 绕 Z 对齐攻击方向”；
        // 非 HD-2D 时只绕 Z。
        static void SpawnPrefab(Vector3 position, Vector2 direction, Color color,
            float scale, SlashStyle style, float lifetime, bool flipY,
            float rotationOffset, float height, SlashVfxLibrary.StyleEntry entry)
        {
            bool hd2d = Hd2dSpriteBridge.Active;
            Vector3 pos = hd2d
                ? new Vector3(position.x, height, position.y)
                : new Vector3(position.x, position.y, 0f);

            var go = Object.Instantiate(entry.prefab, pos, Quaternion.identity);
            go.name = "SlashVfx_" + style;

            // 素材主粒子带 startRotationX = 90°，原生就是水平弧面。
            // HD-2D 下保留水平面，绕 Y 对准攻击方向；这样 8 个朝向的观感一致，
            // 且刀光可以以角色为中心铺开。
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg
                + rotationOffset + entry.rotationOffset;

            if (entry.punchMode)
            {
                // 拳击是径向冲击，不需要竖直 billboard；只绕 Y 让拉伸的光条对准出拳方向。
                if (flipY) yaw += 180f;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            else if (hd2d)
            {
                if (flipY) yaw += 180f;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            else
            {
                // 纯 2D 相机看水平弧面会侧成一条线，退回竖直 billboard。
                float roll = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                    + rotationOffset + entry.rotationOffset;
                if (flipY) roll += 180f;
                go.transform.rotation = Quaternion.AngleAxis(roll, Vector3.forward)
                    * Quaternion.AngleAxis(-90f, Vector3.right);
            }

            float s = Mathf.Max(0.01f, scale) * Mathf.Max(0.01f, entry.scaleMultiplier);
            go.transform.localScale = Vector3.one * s;

            // 拳击模式：关掉主刀弧，只保留 Impact / 烟 / 火花 / 点光。
            ParticleSystem rootSystem = null;
            if (entry.punchMode)
                rootSystem = go.GetComponent<ParticleSystem>(); // 下面跳过它，主弧不 Play

            // 段落层次：按配置开关子层，第一段只留主弧，第三段全开。
            SetChildActive(go, "Smoke", entry.includeSmoke);
            SetChildActive(go, "Flying", entry.includeFlying);
            SetChildActive(go, "Slash Distortion", entry.includeDistortion);
            SetChildActive(go, "Impact", entry.includeImpact);
            SetChildActive(go, "Point Light", entry.includeLight);

            var light = go.transform.Find("Point Light")?.GetComponent<Light>();
            if (light != null && entry.includeLight)
            {
                light.color = new Color(color.r, color.g, color.b, 1f);
                light.intensity = Mathf.Max(0f, entry.lightIntensity);
            }

            // 素材主粒子寿命 0.5s；按招式 lifetime 压缩，避免斩击结束后刀光还挂着。
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            float nominal = 0.5f;
            if (systems.Length > 0)
                nominal = Mathf.Max(0.05f, systems[0].main.startLifetime.constantMax);

            float speed = Mathf.Max(0.1f, entry.playbackSpeed)
                * Mathf.Clamp(nominal / Mathf.Max(0.03f, lifetime), 1f, 6f);

            float particleIntensity = Mathf.Max(0f, entry.particleIntensity);
            float maxLife = 0.05f;
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (entry.punchMode && ps == rootSystem) continue;
                if (!ps.gameObject.activeInHierarchy) continue;

                var main = ps.main;
                if (ps.gameObject != go)
                {
                    main.startSizeMultiplier *= particleIntensity;
                    main.startSpeedMultiplier *= Mathf.Lerp(1f, particleIntensity, 0.5f);
                    var emission = ps.emission;
                    emission.rateOverTimeMultiplier *= particleIntensity;
                }
                main.simulationSpeed = speed;
                float life = main.startLifetime.constantMax / Mathf.Max(0.01f, speed);
                if (life > maxLife) maxLife = life;
                ps.Play(true);
            }

            // Shader Graph 暴露的是 HDR _Color；用 PropertyBlock 逐次染色，不改共享材质。
            float intensity = entry.colorIntensity > 0f ? entry.colorIntensity : 7f;
            Color tint = new Color(color.r * intensity, color.g * intensity, color.b * intensity, 1f);
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                var mat = r.sharedMaterial;
                if (mat == null || !mat.HasProperty(ColorId)) continue;

                r.GetPropertyBlock(block);
                block.SetColor(ColorId, tint);
                r.SetPropertyBlock(block);
                block.Clear();
            }

            // 等最长寿命的粒子自然淡完，再用统一的材质层淡出收尾，避免烟雾/火花硬切。
            float totalLife = maxLife + 0.10f;
            var fade = go.AddComponent<SlashVfxFade>();
            fade.lifetime = totalLife;
            fade.fadeTime = Mathf.Min(0.14f, totalLife * 0.45f);
            fade.tint = tint;
        }

        static void SetChildActive(GameObject root, string childName, bool active)
        {
            var child = root.transform.Find(childName);
            if (child != null) child.gameObject.SetActive(active);
        }

        void Update()
        {
            if (!started)
            {
                started = true;
                if (view == null) view = GetComponent<SpriteRenderer>();
            }

            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Mathf.Max(0.01f, lifetime));

            int count = sequence != null ? sequence.Length : FrameCount;
            int seq = Mathf.Min(count - 1, Mathf.FloorToInt(t * count));
            int frame = sequence != null ? sequence[seq] : seq;
            ApplyFrame(frame);

            // 前 65% 保持亮度，最后 35% 快速淡出。
            float fade = t <= 0.65f ? 1f : 1f - Mathf.InverseLerp(0.65f, 1f, t);
            fade = fade * fade * (3f - 2f * fade);

            if (view != null)
            {
                Color c = color;
                c.a *= fade;
                view.color = c;
            }

            if (coreView != null)
                coreView.color = new Color(1f, 1f, 1f, fade * 0.40f);

            UpdateBillboard();

            if (age >= lifetime) Destroy(gameObject);
        }

        void LateUpdate()
        {
            UpdateBillboard();
        }

        void UpdateBillboard()
        {
            if (!hd2d) return;

            if (cam == null) cam = Camera.main;
            if (cam == null)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, attackAngle);
                return;
            }

            Vector3 toCam = cam.transform.position - transform.position;
            // 与 HD-2D 角色一致：保持竖直 billboard，只绕 Y 朝向相机。
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(toCam, Vector3.up)
                * Quaternion.Euler(0f, 0f, attackAngle);
        }

        void ApplyFrame(int index)
        {
            if (view == null) return;
            index = Mathf.Clamp(index, 0, FrameCount - 1);
            if (index == frameIndex) return;

            frameIndex = index;
            var sprites = GetSprites(style);
            var mats = GetMaterials(style);
            if (sprites == null || mats == null) return;

            view.sprite = sprites[index];
            view.sharedMaterial = mats[index];

            if (coreView != null)
            {
                coreView.sprite = sprites[index];
                coreView.sharedMaterial = mats[index];
            }
        }

        static Sprite[] GetSprites(SlashStyle style)
        {
            int i = Mathf.Clamp((int)style, 0, StylePrefixes.Length - 1);
            if (frameSprites[i] != null) return frameSprites[i];

            var sprites = new Sprite[FrameCount];
            for (int f = 0; f < FrameCount; f++)
            {
                string path = "FX/Slash/" + StylePrefixes[i] + "_" + (f + 1).ToString("00");
                var tex = Resources.Load<Texture2D>(path);
                if (tex == null)
                {
                    Debug.LogWarning("[Slash] 缺少刀光帧: " + path);
                    sprites[f] = sprites[Mathf.Max(0, f - 1)];
                    continue;
                }

                var sprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    PixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);
                sprite.name = "Slash_" + style + "_" + f;
                sprites[f] = sprite;
            }

            frameSprites[i] = sprites;
            return sprites;
        }

        static Material[] GetMaterials(SlashStyle style)
        {
            int i = Mathf.Clamp((int)style, 0, StylePrefixes.Length - 1);
            if (frameMaterials[i] != null) return frameMaterials[i];

            if (additiveShader == null)
            {
                additiveShader = Shader.Find("HD2D/AdditiveSprite");
                if (additiveShader == null) additiveShader = Shader.Find("Sprites/Default");
            }
            if (additiveShader == null) return null;

            var sprites = GetSprites(style);
            var mats = new Material[FrameCount];
            for (int f = 0; f < FrameCount; f++)
            {
                var sprite = sprites[f];
                var mat = new Material(additiveShader)
                {
                    name = "Slash_" + style + "_" + f,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                if (sprite != null) mat.mainTexture = sprite.texture;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                mat.renderQueue = 3000;
                mats[f] = mat;
            }

            frameMaterials[i] = mats;
            return mats;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int i = 0; i < frameSprites.Length; i++)
            {
                frameSprites[i] = null;
                frameMaterials[i] = null;
            }
            additiveShader = null;
            vfxLibrary = null;
        }
    }
}
