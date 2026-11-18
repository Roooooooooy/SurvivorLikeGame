using UnityEngine;
using UnityEngine.Rendering;

namespace RpgGame
{
    // 动作区受击碎光：命中时发射短促像素粒子，池化复用，不在每次命中创建材质/对象。
    public class ActionHitParticles : MonoBehaviour
    {
        const int PoolSize = 12;
        static ActionHitParticles instance;
        readonly ParticleSystem[] pool = new ParticleSystem[PoolSize];
        Material material;
        int next;

        static ActionHitParticles Instance
        {
            get
            {
                if (instance == null)
                    instance = new GameObject("[ActionHitParticles]").AddComponent<ActionHitParticles>();
                return instance;
            }
        }

        public static void Prepare() => _ = Instance;

        public static void Spawn(Vector2 position, Vector2 sourcePosition, bool playerHit, bool crit)
        {
            Instance.Emit(position, sourcePosition, playerHit, crit);
        }

        void Awake()
        {
            instance = this;
            var shader = Shader.Find("HD2D/AdditiveSprite");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            material = new Material(shader)
            {
                name = "ActionHitParticles (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = Texture2D.whiteTexture,
                renderQueue = 3000,
            };
            material.SetColor("_Color", Color.white);

            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("HitParticles_" + i);
                go.transform.SetParent(transform, false);
                var particles = go.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.loop = false;
                main.playOnAwake = false;
                main.duration = 0.4f;
                main.startLifetime = 0.28f;
                main.startSpeed = 0f;
                main.startSize = 0.08f;
                main.maxParticles = 24;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 0.35f;
                var emission = particles.emission;
                emission.rateOverTime = 0f;
                var shape = particles.shape;
                shape.enabled = false;

                var fade = particles.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.25f),
                        new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;
                var size = particles.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));

                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sortingOrder = 96;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                pool[i] = particles;
            }
        }

        void Emit(Vector2 position, Vector2 sourcePosition, bool playerHit, bool crit)
        {
            if (material == null) return;
            int slot = next;
            for (int i = 0; i < pool.Length; i++)
            {
                int candidate = (next + i) % pool.Length;
                if (!pool[candidate].IsAlive()) { slot = candidate; break; }
            }
            next = (slot + 1) % pool.Length;
            var particles = pool[slot];
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            bool hd2d = Hd2dSpriteBridge.Active || Hd2dPixelPlayer.Active;
            Vector3 point = hd2d ? new Vector3(position.x, 0.85f, position.y)
                : new Vector3(position.x, position.y, -0.1f);
            Vector2 away = position - sourcePosition;
            if (away.sqrMagnitude > 0.001f) away.Normalize();
            Vector3 push = hd2d ? new Vector3(away.x, 0f, away.y) : (Vector3)away;
            var cam = Camera.main;
            Vector3 right = hd2d && cam != null ? cam.transform.right : Vector3.right;
            Vector3 up = hd2d && cam != null ? cam.transform.up : Vector3.up;
            Color color = playerHit ? new Color(1.6f, 0.35f, 0.12f, 1f)
                : new Color(1.8f, 1.1f, 0.35f, 1f);
            int count = crit ? 14 : 10;
            particles.Play();
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.Range(-0.3f, 0.3f)) / count * Mathf.PI * 2f;
                Vector3 spread = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                var particle = new ParticleSystem.EmitParams
                {
                    position = point + spread * Random.Range(0f, 0.05f),
                    velocity = spread * Random.Range(1.2f, 2.4f) + push * 0.5f,
                    startLifetime = Random.Range(0.16f, 0.3f),
                    startSize = Random.Range(0.055f, 0.095f),
                    startColor = color,
                };
                particles.Emit(particle, 1);
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            if (material != null) Destroy(material);
        }
    }
}
