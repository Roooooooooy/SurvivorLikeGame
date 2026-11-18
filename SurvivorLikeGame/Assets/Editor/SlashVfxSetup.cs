using UnityEditor;
using UnityEngine;
using RpgGame;

namespace RpgGameEditor
{
    // 配置 3D 刀光库：三段共用同一基础刀光与颜色，
    // 用旋转/强度/粒子层数做出 1 → 2 → 3 段的层次。
    // 只需要跑一次：工具 / 角色扮演 / 生成 3D 刀光库。
    public static class SlashVfxSetup
    {
        const string LibraryFolder = "Assets/Resources/FX";
        const string LibraryPath = LibraryFolder + "/SlashVfxLibrary.asset";
        const string PrefabFolder = "Assets/Free Slash VFX/Prefabs/";

        [MenuItem("工具/角色扮演/生成 3D 刀光库")]
        public static void Setup()
        {
            EnsureFolder("Assets/Resources", "FX");

            var library = AssetDatabase.LoadAssetAtPath<SlashVfxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SlashVfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            // 三段共用同一套基础刀光，只靠旋转/强度/粒子层数做递进。
            const string basePrefab = "Slash Fire VFX";
            library.styles = new[]
            {
                // 第一段：小冲击，快、干净
                Make(basePrefab, rotationOffset: 0f, scaleMultiplier: 0.95f,
                    colorIntensity: 5.5f, playbackSpeed: 1.2f, particleIntensity: 0.8f,
                    smoke: false, flying: false, impact: true, light: false, punch: true),
                // 第二段：冲击 + 烟 + 飞散火花
                Make(basePrefab, rotationOffset: 14f, scaleMultiplier: 1.0f,
                    colorIntensity: 6.5f, playbackSpeed: 1.0f, particleIntensity: 1.0f,
                    smoke: true, flying: true, impact: true, light: false, punch: true),
                // 第三段：全层 + 点光源，重拳收尾
                Make(basePrefab, rotationOffset: -14f, scaleMultiplier: 1.15f,
                    colorIntensity: 8.0f, playbackSpeed: 0.9f, particleIntensity: 1.35f,
                    smoke: true, flying: true, impact: true, light: true, punch: true),
            };

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = library;
            Debug.Log("[Slash] 3D 刀光库已生成: " + LibraryPath);
        }

        static SlashVfxLibrary.StyleEntry Make(string prefabName, float rotationOffset,
            float scaleMultiplier, float colorIntensity, float playbackSpeed,
            float particleIntensity, bool smoke, bool flying, bool impact, bool light,
            bool punch)
        {
            var entry = new SlashVfxLibrary.StyleEntry
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + prefabName + ".prefab"),
                punchMode = punch,
                // 刀光按 Circle.005 半径 1.45 校准；拳击命中按 Impact 的实际观感先取 2.0。
                localRadius = punch ? 2.0f : 1.45f,
                rotationOffset = rotationOffset,
                scaleMultiplier = scaleMultiplier,
                colorIntensity = colorIntensity,
                playbackSpeed = playbackSpeed,
                particleIntensity = particleIntensity,
                includeSmoke = smoke,
                includeFlying = flying,
                includeDistortion = !punch,
                includeImpact = impact,
                includeLight = light,
                lightIntensity = 1.2f,
            };

            if (entry.prefab == null)
                Debug.LogWarning("[Slash] 找不到预制体: " + PrefabFolder + prefabName + ".prefab");
            return entry;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
