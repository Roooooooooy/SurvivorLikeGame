using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// HD-2D（八方旅人风格）最小验证：切 URP + 3D 场景 + billboard 精灵 + Volume 后处理。
// 只新增 Assets/Settings 下的管线资源，不改主场景。
public static class HD2DSetup
{
    const string SettingsDir = "Assets/Settings";
    const string RendererPath = SettingsDir + "/HD2D_Renderer.asset";
    const string PipelinePath = SettingsDir + "/HD2D_Pipeline.asset";

    [MenuItem("Tools/HD2D/1. Create URP Assets")]
    public static void CreateUrpAssets()
    {
        if (!AssetDatabase.IsValidFolder(SettingsDir))
            AssetDatabase.CreateFolder("Assets", "Settings");

        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);
        }

        // postProcessData 的获取方法是 internal；不设置的话 Volume 后处理完全不生效。
        if (rendererData.postProcessData == null)
        {
            var mi = typeof(PostProcessData).GetMethod(
                "GetDefaultPostProcessData",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi != null)
                rendererData.postProcessData = (PostProcessData)mi.Invoke(null, null);
            else
                Debug.LogWarning("[HD2D] 找不到 GetDefaultPostProcessData");
        }
        EditorUtility.SetDirty(rendererData);

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }

        pipeline.supportsHDR = true;
        // 先关 MSAA：自定义全屏 pass 在 MSAA 颜色目标上写回容易出问题
        pipeline.msaaSampleCount = 1;
        pipeline.renderScale = 1f;
        pipeline.shadowDistance = 40f;
        pipeline.supportsCameraDepthTexture = true;
        pipeline.supportsCameraOpaqueTexture = true;
        EditorUtility.SetDirty(pipeline);

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        AssetDatabase.SaveAssets();

        Debug.Log("[HD2D] URP Asset 已创建并设为默认管线: " + PipelinePath);
    }

    [MenuItem("Tools/HD2D/2. Switch To URP")]
    public static void SwitchToUrp()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            Debug.LogError("[HD2D] 找不到 " + PipelinePath + "，先运行 1");
            return;
        }
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        AssetDatabase.SaveAssets();
        Debug.Log("[HD2D] 已切换到 URP");
    }
}
