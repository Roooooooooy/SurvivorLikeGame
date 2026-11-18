using UnityEngine;

namespace RpgGame
{
    // 3D 刀光预制体库（Free Slash VFX / MaykerStudio）。
    // 放在 Resources/FX/SlashVfxLibrary.asset；缺失时 SlashArcEffect 自动退回旧的序列帧实现。
    [CreateAssetMenu(menuName = "角色扮演/刀光库", fileName = "SlashVfxLibrary")]
    public class SlashVfxLibrary : ScriptableObject
    {
        [System.Serializable]
        public class StyleEntry
        {
            public GameObject prefab;

            [Header("模式")]
            [Tooltip("拳击模式：禁用主刀弧与扭曲，改用 Impact 命中层，生成在拳头落点而不是角色中心")]
            public bool punchMode;

            [Header("形状")]
            [Tooltip("预制体 scale = 1 时，刀光外缘到中心的半径（世界单位）")]
            public float localRadius = 1.45f;

            [Tooltip("绕 Y 的额外旋转（度）；三段共用同一朝向，靠这个做出挥砍角度差异")]
            public float rotationOffset;

            [Tooltip("在判定框反推缩放之上的美术缩放系数")]
            public float scaleMultiplier = 1f;

            [Header("颜色 / 速度")]
            [Tooltip("HDR 颜色强度；素材默认 _Color 约 8.5")]
            public float colorIntensity = 7f;

            [Tooltip("整体播放速度倍率；1 = 素材原速")]
            public float playbackSpeed = 1f;

            [Header("粒子层次")]
            [Tooltip("只作用于子粒子（火花/烟/冲击），主弧面大小由 scaleMultiplier 控制")]
            [Range(0f, 2f)] public float particleIntensity = 1f;

            [Tooltip("烟雾层")]
            public bool includeSmoke;

            [Tooltip("飞散火花层")]
            public bool includeFlying;

            [Tooltip("扭曲/热浪层")]
            public bool includeDistortion = true;

            [Tooltip("命中冲击层（Impact 子预制体）")]
            public bool includeImpact;

            [Tooltip("点光源闪光")]
            public bool includeLight;

            [Tooltip("启用点光源时的强度")]
            public float lightIntensity = 1.2f;
        }

        public StyleEntry[] styles = new StyleEntry[3];

        public StyleEntry Get(SlashStyle style)
        {
            if (styles == null || styles.Length == 0) return null;
            int i = Mathf.Clamp((int)style, 0, styles.Length - 1);
            return styles[i];
        }
    }
}
