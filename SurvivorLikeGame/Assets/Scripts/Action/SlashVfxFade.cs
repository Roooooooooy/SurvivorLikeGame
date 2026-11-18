using UnityEngine;

namespace RpgGame
{
    // 在生命周期末尾统一压暗所有材质层的 alpha/强度，避免粒子寿命结束或对象销毁时硬切。
    // 由 SlashArcEffect.SpawnPrefab 挂在 3D 刀光实例上。
    public class SlashVfxFade : MonoBehaviour
    {
        [HideInInspector] public float lifetime = 0.3f;
        [HideInInspector] public float fadeTime = 0.12f;
        [HideInInspector] public Color tint = Color.white;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int CustomColorId = Shader.PropertyToID("_CustomColor");
        static readonly int FrontColorId = Shader.PropertyToID("_FrontColor");
        static readonly int BackColorId = Shader.PropertyToID("_BackColor");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int StrengthId = Shader.PropertyToID("_Strength");
        static readonly int ColorPowerId = Shader.PropertyToID("_ColorPower");

        Renderer[] renderers;
        MaterialPropertyBlock block;
        float age;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            age += Time.deltaTime;
            float fadeStart = Mathf.Max(0.01f, lifetime - fadeTime);
            if (age <= fadeStart) return;

            float a = Mathf.Clamp01((lifetime - age) / Mathf.Max(0.01f, fadeTime));
            ApplyAlpha(a);

            if (age >= lifetime) Destroy(gameObject);
        }

        void ApplyAlpha(float a)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                var mat = r.sharedMaterial;
                if (mat == null) continue;

                r.GetPropertyBlock(block);
                bool changed = false;

                if (mat.HasProperty(ColorId))
                {
                    Color c = tint;
                    c.a *= a;
                    block.SetColor(ColorId, c);
                    changed = true;
                }
                else if (mat.HasProperty(CustomColorId))
                {
                    Color c = mat.GetColor(CustomColorId);
                    c.a *= a;
                    block.SetColor(CustomColorId, c);
                    changed = true;
                }
                else if (mat.HasProperty(FrontColorId))
                {
                    if (mat.HasProperty(BackColorId))
                    {
                        Color b = mat.GetColor(BackColorId);
                        b.a *= a;
                        block.SetColor(BackColorId, b);
                    }

                    Color f = mat.GetColor(FrontColorId);
                    f.a *= a;
                    block.SetColor(FrontColorId, f);
                    changed = true;
                }

                if (mat.HasProperty(OpacityId))
                    block.SetFloat(OpacityId, mat.GetFloat(OpacityId) * a);
                if (mat.HasProperty(StrengthId))
                    block.SetFloat(StrengthId, mat.GetFloat(StrengthId) * a);
                if (mat.HasProperty(ColorPowerId))
                    block.SetFloat(ColorPowerId, mat.GetFloat(ColorPowerId) * a);

                if (changed) r.SetPropertyBlock(block);
                block.Clear();
            }
        }
    }
}
