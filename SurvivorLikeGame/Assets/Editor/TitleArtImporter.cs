using UnityEditor;
using UnityEngine;

// 标题页新增素材的导入设置（Kenney CC0）：
// - 边框/分隔线是像素线稿，用 Point + 无压缩，边框按 96px 源图给 24px 九宫格边界
// - 光晕/烟雾/余烬是大尺寸软边贴图，用 Bilinear，避免明显锯齿
// 可重复执行；Assets/Resources/TitleUI 下的文件缺失时只跳过并给出警告。
public static class TitleArtImporter
{
    const string Dir = "Assets/Resources/TitleUI";

    [MenuItem("Tools/Survivor/Configure Title UI Art")]
    public static void Configure()
    {
        ConfigureSprite(Dir + "/frame_double.png", FilterMode.Point, new Vector4(24f, 24f, 24f, 24f));
        ConfigureSprite(Dir + "/divider_fade.png", FilterMode.Point, Vector4.zero);

        ConfigureSprite(Dir + "/glow_soft.png", FilterMode.Bilinear, Vector4.zero);
        ConfigureSprite(Dir + "/ember_star.png", FilterMode.Bilinear, Vector4.zero);
        ConfigureSprite(Dir + "/smoke_wide.png", FilterMode.Bilinear, Vector4.zero);
        ConfigureSprite(Dir + "/smoke_wisp.png", FilterMode.Bilinear, Vector4.zero);

        AssetDatabase.SaveAssets();
        Debug.Log("[TitleArt] 标题页素材导入设置完成");
    }

    static void ConfigureSprite(string path, FilterMode filter, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[TitleArt] 找不到贴图导入器: " + path);
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = border;
        importer.filterMode = filter;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}
