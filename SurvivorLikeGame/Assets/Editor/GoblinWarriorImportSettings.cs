using UnityEditor;
using UnityEngine;

// 哥布林战士精灵表的导入设置：Point、无压缩、无 mipmap、NPOT 不缩放。
// 表是运行时用 Texture2D 切帧的，所以导入类型保持 Default。
public class GoblinWarriorImportSettings : AssetPostprocessor
{
    const string Root = "/Resources/Enemies/GoblinWarrior/";

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
        // 最宽的表是攻击 9 帧 × 96px = 864px，不能被压到 512。
        ti.maxTextureSize = 1024;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.isReadable = false;
        ti.sRGBTexture = true;
    }
}
