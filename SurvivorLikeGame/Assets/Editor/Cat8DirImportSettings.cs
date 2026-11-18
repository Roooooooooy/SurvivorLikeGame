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
        // 攻击表为 16 帧 × 64px = 1024px 宽，512 会把帧压成 32px。
        ti.maxTextureSize = 1024;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.isReadable = true;
        ti.sRGBTexture = true;
    }

    [MenuItem("工具/HD-2D/11. 重新导入猫 8 方向精灵表")]
    static void Reimport()
    {
        AssetDatabase.ImportAsset("Assets/Resources/Player/Cat8Dir",
            ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        Debug.Log("[Cat8Dir] 已重新导入 8 方向精灵表");
    }
}
