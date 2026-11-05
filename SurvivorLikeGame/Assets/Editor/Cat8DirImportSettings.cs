using UnityEditor;
using UnityEngine;

// Cat8Dir 精灵表的导入设置：Point、无压缩、无 mipmap、可读（运行时算脚底 pivot）。
public class Cat8DirImportSettings : AssetPostprocessor
{
    const string Root = "/Resources/Player/Cat8Dir/";

    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains(Root)) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 512;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.isReadable = true;
        ti.sRGBTexture = true;
    }

    [MenuItem("Tools/HD2D/9. Reimport Cat 8Dir Sheets")]
    static void Reimport()
    {
        AssetDatabase.ImportAsset("Assets/Resources/Player/Cat8Dir",
            ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        Debug.Log("[Cat8Dir] 已重新导入 8 方向精灵表");
    }
}
