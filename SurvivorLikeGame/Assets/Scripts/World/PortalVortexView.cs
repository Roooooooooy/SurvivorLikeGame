using UnityEngine;
using UnityEngine.Rendering;

namespace RpgGame
{
    // 裂隙传送门表现：点光呼吸 + 绕漩涡面旋转、向内收束的加法混合光点。
    // 光点用运行时生成的小圆点贴图，避免依赖额外美术资源。
    public class PortalVortexView : MonoBehaviour
    {
        public Light glowLight;
        public float baseLightIntensity = 3f;
        public float lightPulseSpeed = 2.1f;

        [Header("漩涡光点")]
        public Vector2 vortexSize = new Vector2(1.65f, 2.15f);
        public Vector3 vortexCenter = new Vector3(0f, 1.2f, -0.08f);
        public int particleCount = 46;
        public float inwardSpeed = 0.22f;
        public float angularSpeed = 1.1f;

        struct Mote
        {
            public Transform tr;
            public SpriteRenderer sr;
            public float angle;
            public float radius;
            public float size;
            public float spin;
            public float phase;
        }

        static Material moteMaterial;
        static Sprite moteSprite;

        Mote[] motes;
        Camera cam;
        float lightSeed;

        void Start()
        {
            cam = Camera.main;
            lightSeed = Random.Range(0f, 10f);
            BuildMotes();
        }

        void OnDestroy()
        {
            if (motes == null) return;
            for (int i = 0; i < motes.Length; i++)
            {
                if (motes[i].tr != null) Destroy(motes[i].tr.gameObject);
            }
        }

        void BuildMotes()
        {
            EnsureMoteAssets();
            if (moteSprite == null || moteMaterial == null) return;

            motes = new Mote[Mathf.Max(1, particleCount)];
            for (int i = 0; i < motes.Length; i++)
            {
                var go = new GameObject("VortexMote");
                go.transform.SetParent(transform, false);
                go.AddComponent<Hd2dProxyMarker>();

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = moteSprite;
                sr.sharedMaterial = moteMaterial;
                sr.shadowCastingMode = ShadowCastingMode.Off;
                sr.receiveShadows = false;

                motes[i] = new Mote
                {
                    tr = go.transform,
                    sr = sr,
                    angle = Random.Range(0f, Mathf.PI * 2f),
                    radius = Random.Range(0.18f, 1f),
                    size = Random.Range(0.55f, 1.35f),
                    spin = Random.Range(-160f, 160f),
                    phase = Random.Range(0f, 10f),
                };

                UpdateMote(ref motes[i], 0f, true);
            }
        }

        void Update()
        {
            if (glowLight != null)
            {
                glowLight.intensity = baseLightIntensity
                    * (0.88f + 0.12f * Mathf.Sin(Time.time * lightPulseSpeed + lightSeed));
            }

            if (motes == null) return;
            if (cam == null) cam = Camera.main;

            float dt = Time.deltaTime;
            for (int i = 0; i < motes.Length; i++)
                UpdateMote(ref motes[i], dt, false);
        }

        void UpdateMote(ref Mote m, float dt, bool initial)
        {
            if (m.tr == null) return;

            m.radius -= inwardSpeed * dt;
            m.angle += angularSpeed * dt * (0.35f + m.radius * 0.9f);

            if (m.radius < 0.10f)
            {
                m.radius = Random.Range(0.88f, 1.05f);
                m.angle = Random.Range(0f, Mathf.PI * 2f);
                m.size = Random.Range(0.55f, 1.35f);
                m.phase = Random.Range(0f, 10f);
            }

            float rx = vortexSize.x * 0.5f;
            float ry = vortexSize.y * 0.5f;
            Vector3 local = vortexCenter + new Vector3(
                Mathf.Cos(m.angle) * m.radius * rx,
                Mathf.Sin(m.angle) * m.radius * ry,
                0f);

            Vector3 world = transform.TransformPoint(local);
            m.tr.position = world;

            float edgeFade = Mathf.Clamp01((1.08f - m.radius) * 1.8f);
            float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * 3.2f + m.phase);
            float scale = m.size * (0.05f + 0.04f * edgeFade) * pulse;
            m.tr.localScale = new Vector3(scale, scale, 1f);

            Color c = Color.Lerp(
                new Color(0.55f, 0.75f, 1f, 0.9f),
                new Color(0.78f, 0.45f, 1f, 0.9f),
                m.radius);
            c.a *= edgeFade * pulse;
            m.sr.color = c;

            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - world;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.0001f)
                    m.tr.rotation = Quaternion.LookRotation(toCam, Vector3.up)
                        * Quaternion.Euler(0f, 0f, m.angle * Mathf.Rad2Deg + m.spin);
            }
            else if (initial)
            {
                m.tr.rotation = Quaternion.identity;
            }
        }

        static void EnsureMoteAssets()
        {
            if (moteSprite != null) return;

            var shader = Shader.Find("HD2D/AdditiveSprite");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "PortalMoteTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x - center) / center;
                    float v = (y - center) / center;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    a *= a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            moteSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size);
            moteSprite.name = "PortalMote";

            moteMaterial = new Material(shader)
            {
                name = "PortalMote (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            moteMaterial.mainTexture = tex;
            if (moteMaterial.HasProperty("_Color"))
                moteMaterial.SetColor("_Color", Color.white);
            moteMaterial.renderQueue = 3010;
        }
    }
}
