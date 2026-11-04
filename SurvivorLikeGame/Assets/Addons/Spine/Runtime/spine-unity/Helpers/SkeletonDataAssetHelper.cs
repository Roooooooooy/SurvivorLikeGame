using System.IO;
using UnityEngine;

namespace Spine.Unity
{
    // 运行时从二进制 skel 字节数据创建 SkeletonDataAsset 的辅助类
    // 放在 spine-unity 程序集内以访问 SkeletonDataAsset.InitializeWithData (internal)
    public static class SkeletonDataAssetHelper
    {
        // 从二进制 skel 字节数据创建并初始化 SkeletonDataAsset（不依赖 TextAsset）
        public static SkeletonDataAsset CreateFromBinary(
            byte[] skelBytes, AtlasAssetBase atlasAsset, float scale = 0.01f)
        {
            var sd = ScriptableObject.CreateInstance<SkeletonDataAsset>();
            sd.atlasAssets = new[] { atlasAsset };
            sd.scale = scale;
            // GetSkeletonData() 首先检查 skeletonJSON != null，若为空会 Clear() 掉已加载数据。
            // 设置占位值让守卫通过，之后 skeletonData != null 分支直接返回缓存。
            sd.skeletonJSON = new TextAsset(string.Empty);

            var atlas = atlasAsset.GetAtlas();
            var loader = new AtlasAttachmentLoader(atlas);
            var binary = new SkeletonBinary(loader) { Scale = scale };
            var skeletonData = binary.ReadSkeletonData(new MemoryStream(skelBytes));

            sd.InitializeWithData(skeletonData);
            return sd;
        }
    }
}
