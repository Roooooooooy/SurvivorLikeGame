using System.Reflection;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

// 导入 Catalyst 猫 Spine 资产（Spine 3.6 数据）：
//   cat_spine.png / .atlas.txt / .json → Material + SpineAtlasAsset + SkeletonDataAsset
// 自带 28 个动作，以及 default / custom skin 和 hat / wing / accessory 等换装槽位。
public static class CatSpineSetup
{
    const string Dir = "Assets/Resources/Player/CatSpine";
    const string PngPath = Dir + "/cat_spine.png";
    const string AtlasPath = Dir + "/cat_spine.atlas.txt";
    const string JsonPath = Dir + "/cat_spine.json";
    const string MatPath = Dir + "/cat_spine_Mat.mat";
    const string AtlasAssetPath = Dir + "/cat_spine_Atlas.asset";
    const string DataAssetPath = Dir + "/cat_spine_SkeletonData.asset";

    [MenuItem("Tools/HD2D/8. Build Cat Spine Assets")]
    public static void BuildMenu()
    {
        Build();
    }

    public static SkeletonDataAsset Build()
    {
        var importer = AssetImporter.GetAtPath(PngPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            // PMA 图集不要开 Alpha Is Transparency，否则 Spine 会警告并可能污染边缘
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PngPath);
        var atlasText = AssetDatabase.LoadAssetAtPath<TextAsset>(AtlasPath);
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
        if (tex == null || atlasText == null || json == null)
        {
            Debug.LogError("[HD2D] 猫 Spine 源文件缺失: " + PngPath + " / " + AtlasPath + " / " + JsonPath);
            return null;
        }

        // 主体走正常深度（被墙面/大怪挡住），遮挡时的可见性由 Hd2dSpinePlayer 的 X 光剪影负责。
        // 不要再用 Spine/SkeletonAlways（ZTest Always）：那会让主角整块盖在前景几何上。
        var shader = Shader.Find("Spine/Skeleton");
        if (shader == null)
        {
            Debug.LogError("[HD2D] 找不到 Spine/Skeleton shader");
            return null;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "cat_spine_Mat" };
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        mat.shader = shader;
        mat.SetTexture("_MainTex", tex);
        // Catalyst 猫是 PMA 图集；若边缘发黑再切到 straight alpha
        mat.SetFloat("_StraightAlphaInput", 0f);
        mat.DisableKeyword("_STRAIGHT_ALPHA_INPUT");
        EditorUtility.SetDirty(mat);

        var atlas = AssetDatabase.LoadAssetAtPath<SpineAtlasAsset>(AtlasAssetPath);
        if (atlas == null)
        {
            atlas = SpineAtlasAsset.CreateRuntimeInstance(atlasText, new[] { mat }, false);
            AssetDatabase.CreateAsset(atlas, AtlasAssetPath);
        }
        else
        {
            atlas.atlasFile = atlasText;
            atlas.materials = new[] { mat };
            // 清掉缓存的 Atlas，让新的图集/材质生效
            var field = typeof(SpineAtlasAsset).GetField(
                "atlas", BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(atlas, null);
            EditorUtility.SetDirty(atlas);
        }

        var data = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(DataAssetPath);
        if (data == null)
        {
            data = SkeletonDataAsset.CreateRuntimeInstance(json, atlas, false, 0.004f);
            AssetDatabase.CreateAsset(data, DataAssetPath);
        }
        else
        {
            data.skeletonJSON = json;
            data.atlasAssets = new AtlasAssetBase[] { atlas };
            data.scale = 0.004f;
            data.Clear();
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var skeleton = data.GetSkeletonData(false);
        if (skeleton == null)
        {
            Debug.LogError("[HD2D] 猫 Spine 数据加载失败，检查 JSON/atlas");
            return null;
        }

        Debug.Log("[HD2D] 猫 Spine 资产就绪: animations=" + skeleton.Animations.Count
            + " skins=" + skeleton.Skins.Count + " scale=" + data.scale);
        return data;
    }
}
