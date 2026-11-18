using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RpgGame
{
    // 8 方向逐帧敌人视图（哥布林战士）。
    // 资源规范：Resources/Enemies/GoblinWarrior/{State}/{prefix}_{direction}.png
    // 每张表横向排列 96x96 帧；帧按画布中心对齐，脚底线由 directionGroundY 决定。
    // 状态：待机/奔跑由 Rigidbody2D 速度自动切换；攻击/受击/死亡由 EnemyBrain 驱动。
    [DisallowMultipleComponent]
    public class DirectionalEnemyView : MonoBehaviour
    {
        [Header("资源")]
        public string resourceRoot = "Enemies/GoblinWarrior";
        public int frameWidth = 96;
        public int frameHeight = 96;
        public float pixelsPerUnit = 24f;

        [Header("节奏")]
        public float idleFps = 6f;
        public float runFps = 10f;
        public float hitFps = 16f;
        public float deathFps = 10f;
        public float moveThreshold = 0.05f;

        [Header("死亡消散")]
        public float deathFadeDelay = 0.7f;
        public float deathFadeDuration = 0.9f;

        [Header("脚底（归一化画布，顶部为 0）")]
        // 索引是世界方向：0=东 1=东北 2=北 3=西北 4=西 5=西南 6=南 7=东南。
        // 数值已按下面的镜像 DirNames 取对应美术方向的脚底线。
        public float[] directionGroundY = { 67f, 66f, 66f, 64f, 67f, 65f, 65f, 63f };

        [Header("表现")]
        public bool groundShadow = true;
        public float shadowDiameter = 1.15f;
        public float shadowDepth = 0.60f;
        [Range(0f, 1f)] public float shadowOpacity = 0.45f;
        [Range(0f, 2f)] public float litAmbient = 0.70f;

        // 世界方向 → 美术方向。PixelLab 导出的美术整体左右与游戏世界相反，
        // 与 Hd2dPixelPlayer 的镜像表保持一致：E↔W、NE↔NW、SE↔SW，N/S 不变。
        static readonly string[] DirNames =
        {
            "west", "north-west", "north", "north-east",
            "east", "south-east", "south", "south-west",
        };

        enum State { Locomotion, Attack, Hit, Death }

        class Clip
        {
            public Sprite[] frames;
            public float fps;
        }

        // 两个哥布林共用同一批精灵，避免重复建 Sprite。
        static readonly Dictionary<string, Sprite[]> sheetCache =
            new Dictionary<string, Sprite[]>();

        // 关闭 Domain Reload 时也能在每次进入 Play 前丢掉上一次的 Sprite 缓存。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSheetCache()
        {
            sheetCache.Clear();
        }

        SpriteRenderer sr;
        Material material;
        Hd2dProxyLift liftMarker;
        Hd2dBlobShadow shadow;
        Transform shadowAnchor;
        Rigidbody2D body;

        readonly Clip[] idleClips = new Clip[8];
        readonly Clip[] runClips = new Clip[8];
        readonly Clip[] attackClips = new Clip[8];
        readonly Clip[] hitClips = new Clip[8];
        readonly Clip[] deathForward = new Clip[8];
        readonly Clip[] deathBack = new Clip[8];
        readonly Clip[] deathCollapse = new Clip[8];

        State state = State.Locomotion;
        int facing = 6;              // 南
        int frame;
        float frameTimer;

        Clip currentClip;
        int actionFrame;
        float actionElapsed;
        float actionDuration;
        float deathClipDuration;

        public int Facing => facing;
        public bool IsDead => state == State.Death;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = Mathf.Max(sr.sortingOrder, 5);

            body = GetComponent<Rigidbody2D>();

            SetupMaterial();
            SetupLift();
            LoadAll();
            if (groundShadow) SetupShadow();

            if (idleClips[facing] != null && idleClips[facing].frames.Length > 0)
                Apply(idleClips[facing].frames[0]);
        }

        void Start()
        {
            // 不等桥的 0.5 秒扫描，立刻接管第一帧。
            if (Hd2dSpriteBridge.Instance != null)
                Hd2dSpriteBridge.Instance.Register(sr);
        }

        void LateUpdate()
        {
            if (sr == null) return;

            switch (state)
            {
                case State.Death:
                    UpdateDeath();
                    break;
                case State.Attack:
                    UpdateAttack();
                    break;
                case State.Hit:
                    UpdateHit();
                    break;
                default:
                    UpdateLocomotion();
                    break;
            }

            UpdateLift();
            UpdateShadow();
        }

        // ---- 供 EnemyBrain 调用 ----

        public void SetFacing(int direction)
        {
            facing = ClampDir(direction);
        }

        // 攻击动作按总时长铺满（前摇 + 后摇），保证出手时机和动画一致。
        public float PlayAttack(int direction, float duration)
        {
            facing = ClampDir(direction);
            var clip = attackClips[facing] != null ? attackClips[facing] : FindAny(attackClips);
            if (clip == null) return 0f;

            state = State.Attack;
            currentClip = clip;
            actionElapsed = 0f;
            actionDuration = Mathf.Max(0.05f, duration);
            actionFrame = 0;
            Apply(clip.frames[0]);
            return actionDuration;
        }

        // 返回硬直时长，EnemyBrain 用它锁定移动。
        public float PlayHit(int direction)
        {
            facing = ClampDir(direction);
            var clip = hitClips[facing] != null ? hitClips[facing] : FindAny(hitClips);
            if (clip == null) return 0f;

            state = State.Hit;
            currentClip = clip;
            actionElapsed = 0f;
            actionDuration = clip.frames.Length / Mathf.Max(1f, clip.fps);
            actionFrame = 0;
            Apply(clip.frames[0]);
            return actionDuration;
        }

        // 按当前方向随机选一种死亡；返回动画时长。
        public float PlayDeath(int direction)
        {
            facing = ClampDir(direction);
            var clip = PickDeathClip(facing);
            if (clip == null) return 0f;

            state = State.Death;
            currentClip = clip;
            actionElapsed = 0f;
            actionDuration = clip.frames.Length / Mathf.Max(1f, clip.fps);
            deathClipDuration = actionDuration;
            actionFrame = 0;
            Apply(clip.frames[0]);
            ConfigureDissolveMaterial(material);

            // 返回“动画 + 消散”的总时长，ActionEnemy 用它决定何时销毁。
            return actionDuration + deathFadeDelay + deathFadeDuration;
        }

        // 调试用：把死亡消散停在 t 附近，方便截图确认中间态。
        public void DebugPreviewDeathDissolve(float t = 0.5f)
        {
            PlayDeath(facing);
            deathFadeDelay = 0f;
            deathFadeDuration = 60f;
            actionElapsed = deathClipDuration + Mathf.Clamp01(t) * deathFadeDuration;
            if (currentClip != null && currentClip.frames.Length > 0)
                Apply(currentClip.frames[currentClip.frames.Length - 1]);
            SetDissolve(Mathf.Clamp01(t));
        }

        // 世界方向（+X 为 0°，逆时针）→ 0..7，与美术方向命名一致。
        public static int DirectionFromWorld(Vector2 v)
        {
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            return Mathf.RoundToInt(angle / 45f) % 8;
        }

        // ---- 状态更新 ----

        void UpdateLocomotion()
        {
            Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
            bool moving = velocity.sqrMagnitude > moveThreshold * moveThreshold;

            if (moving)
            {
                int newFacing = DirectionFromWorld(velocity);
                if (newFacing != facing)
                {
                    facing = newFacing;
                    frame = 0;
                    frameTimer = 0f;
                }
            }

            var clip = moving ? runClips[facing] : idleClips[facing];
            if (clip == null) clip = idleClips[facing];
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return;

            if (clip != currentClip)
            {
                currentClip = clip;
                frame = 0;
                frameTimer = 0f;
            }

            float fps = moving ? Mathf.Max(1f, clip.fps) : Mathf.Max(1f, idleFps);
            frameTimer += Time.deltaTime;
            float step = 1f / fps;
            while (frameTimer >= step)
            {
                frameTimer -= step;
                frame = (frame + 1) % clip.frames.Length;
            }

            Apply(clip.frames[Mathf.Clamp(frame, 0, clip.frames.Length - 1)]);
        }

        void UpdateAttack()
        {
            if (currentClip == null)
            {
                state = State.Locomotion;
                return;
            }

            actionElapsed += Time.deltaTime;
            int count = currentClip.frames.Length;
            actionFrame = Mathf.Min(count - 1,
                Mathf.FloorToInt(actionElapsed / actionDuration * count));
            Apply(currentClip.frames[actionFrame]);

            if (actionElapsed >= actionDuration)
            {
                state = State.Locomotion;
                currentClip = null;
                frame = 0;
                frameTimer = 0f;
            }
        }

        void UpdateHit()
        {
            if (currentClip == null)
            {
                state = State.Locomotion;
                return;
            }

            actionElapsed += Time.deltaTime;
            int count = currentClip.frames.Length;
            actionFrame = Mathf.Min(count - 1,
                Mathf.FloorToInt(actionElapsed * currentClip.fps));
            Apply(currentClip.frames[actionFrame]);

            if (actionElapsed >= actionDuration)
            {
                state = State.Locomotion;
                currentClip = null;
                frame = 0;
                frameTimer = 0f;
            }
        }

        void UpdateDeath()
        {
            if (currentClip == null) return;

            actionElapsed += Time.deltaTime;
            int count = currentClip.frames.Length;
            actionFrame = Mathf.Min(count - 1,
                Mathf.FloorToInt(actionElapsed * currentClip.fps));
            Apply(currentClip.frames[actionFrame]); // 停在最后一帧

            float start = deathClipDuration + Mathf.Max(0f, deathFadeDelay);
            float duration = Mathf.Max(0.01f, deathFadeDuration);
            float raw = Mathf.Clamp01((actionElapsed - start) / duration);
            float t = raw * raw * (3f - 2f * raw);   // 缓入缓出
            SetDissolve(t);
        }

        static void ConfigureDissolveMaterial(Material mat)
        {
            if (mat == null) return;
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = 3000;
        }

        void SetDissolve(float t)
        {
            if (material != null && material.HasProperty("_Dissolve"))
                material.SetFloat("_Dissolve", Mathf.Clamp01(t));

            if (shadow != null)
                shadow.SetOpacity(shadowOpacity * (1f - Mathf.Clamp01(t)));

            // 消散开始后不再投射实时阴影；桥会逐帧读取这个标记。
            if (liftMarker != null && t > 0f)
                liftMarker.castShadow = false;

            if (t >= 1f && sr != null)
                sr.enabled = false;
        }

        // ---- 资源 ----

        void LoadAll()
        {
            for (int i = 0; i < 8; i++)
            {
                idleClips[i] = LoadClip("Idle", "idle", i, idleFps);
                runClips[i] = LoadClip("Run", "run", i, runFps);
                attackClips[i] = LoadClip("Attack", "attack", i, 12f);
                hitClips[i] = LoadClip("Hit", "hit", i, hitFps);
                deathForward[i] = LoadClip("Death", "death_forward", i, deathFps);
                deathBack[i] = LoadClip("Death", "death_back", i, deathFps);
                deathCollapse[i] = LoadClip("Death", "death_collapse", i, deathFps);
            }
        }

        Clip LoadClip(string folder, string prefix, int direction, float fps)
        {
            string path = resourceRoot + "/" + folder + "/" +
                          prefix + "_" + DirNames[direction];
            var frames = LoadSheet(path);
            if (frames == null || frames.Length == 0) return null;

            return new Clip { frames = frames, fps = Mathf.Max(1f, fps) };
        }

        Sprite[] LoadSheet(string path)
        {
            if (sheetCache.TryGetValue(path, out var cached))
                return cached;

            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                sheetCache[path] = null;
                return null;
            }

            if (texture.width < frameWidth || texture.height < frameHeight)
            {
                Debug.LogWarning("[DirectionalEnemyView] 精灵表尺寸异常 " +
                                 texture.width + "x" + texture.height +
                                 "：" + path);
                sheetCache[path] = null;
                return null;
            }

            int count = Mathf.Max(1, texture.width / frameWidth);
            var frames = new Sprite[count];
            var pivot = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(i * frameWidth, 0f, frameWidth, frameHeight);
                frames[i] = Sprite.Create(texture, rect, pivot, pixelsPerUnit, 0,
                    SpriteMeshType.FullRect);
                frames[i].name = path + "_" + i;
            }

            sheetCache[path] = frames;
            return frames;
        }

        Clip PickDeathClip(int direction)
        {
            Clip forward = deathForward[direction];
            Clip back = deathBack[direction];
            Clip collapse = deathCollapse[direction];

            int count = (forward != null ? 1 : 0) +
                        (back != null ? 1 : 0) +
                        (collapse != null ? 1 : 0);
            if (count == 0) return null;

            int pick = Random.Range(0, count);
            if (forward != null && pick-- == 0) return forward;
            if (back != null && pick-- == 0) return back;
            return collapse;
        }

        static Clip FindAny(Clip[] clips)
        {
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null) return clips[i];
            return null;
        }

        static int ClampDir(int direction)
        {
            return Mathf.Clamp(direction, 0, 7);
        }

        void Apply(Sprite sprite)
        {
            if (sr == null || sprite == null) return;
            if (sr.sprite == sprite) return;

            sr.sprite = sprite;
            if (material != null && sprite.texture != null)
                material.SetTexture("_MainTex", sprite.texture);
        }

        // ---- 材质 / 贴地 / 阴影 ----

        void SetupMaterial()
        {
            var shader = Shader.Find("HD2D/SpriteLit");
            if (shader == null) return;

            material = new Material(shader)
            {
                name = "GoblinSprite (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_AmbientBoost", litAmbient);
            material.SetFloat("_OutlineWidth", 0f);
            material.SetFloat("_Dissolve", 0f);
            material.SetInt("_ZTest", 4);   // LEqual
            material.SetInt("_ZWrite", 1);
            material.renderQueue = 2600;
            sr.sharedMaterial = material;
        }

        void SetupLift()
        {
            liftMarker = GetComponent<Hd2dProxyLift>();
            if (liftMarker == null)
                liftMarker = gameObject.AddComponent<Hd2dProxyLift>();
            UpdateLift();
        }

        void UpdateLift()
        {
            if (liftMarker == null) return;

            int dir = Mathf.Clamp(facing, 0, 7);
            float ground = directionGroundY != null && directionGroundY.Length > dir
                ? directionGroundY[dir]
                : 65f;

            // 精灵矩形底边到脚底线的距离；负 lift 把它压回世界 y=0。
            liftMarker.lift = -((frameHeight - 1f - ground) /
                                Mathf.Max(0.01f, pixelsPerUnit));
        }


        void SetupShadow()
        {
            var anchorGo = new GameObject(name + "_ShadowAnchor");
            shadowAnchor = anchorGo.transform;

            var shadowGo = new GameObject(name + "_Shadow");
            shadow = shadowGo.AddComponent<Hd2dBlobShadow>();
            shadow.follow = shadowAnchor;
            shadow.diameter = shadowDiameter;
            shadow.depth = shadowDepth;
            shadow.opacity = shadowOpacity;
            shadow.groundY = 0.02f;

            UpdateShadow();
        }

        void UpdateShadow()
        {
            if (shadowAnchor == null) return;

            Vector3 p = transform.position;
            shadowAnchor.position = new Vector3(p.x, 0f, p.y);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);

            if (shadow != null) Destroy(shadow.gameObject);
            if (shadowAnchor != null) Destroy(shadowAnchor.gameObject);
        }
    }
}
