using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// HD-2D 移轴后处理：屏幕中间保留清晰带，上下按高斯模糊虚化。
// URP 17 默认走 RenderGraph，所以用 RecordRenderGraph 而不是旧的 Execute。
public class TiltShiftFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        public Shader shader;

        [Tooltip("清晰带中心的屏幕 Y（0=底部，1=顶部）")]
        [Range(0f, 1f)] public float focusY = 0.52f;

        [Tooltip("清晰带半宽，屏幕 UV 单位")]
        [Range(0f, 0.5f)] public float focusBand = 0.12f;

        [Tooltip("从清晰带边缘到最大模糊的过渡宽度")]
        [Range(0.01f, 1f)] public float falloff = 0.38f;

        [Tooltip("最大模糊半径（像素）")]
        [Range(0f, 12f)] public float maxBlur = 5f;

        [Tooltip("模糊曲线：越大清晰带越窄、边缘越陡")]
        [Range(0.5f, 4f)] public float curve = 1.6f;
    }

    public Settings settings = new Settings();

    TiltShiftPass pass;

    public override void Create()
    {
        pass = new TiltShiftPass(settings)
        {
            // 放在后处理之前：此时 activeColorTexture 是普通颜色目标，
            // 写回后 Bloom/调色还能吃到模糊结果。
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.shader == null || pass == null) return;

        // 只在游戏相机执行。SceneView 也跑的话，Edit 模式下会持续改动 scene 颜色目标，
        // 触发 Scene 视图反复重绘（曾把编辑器卡死）。
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
    }

    class TiltShiftPass : ScriptableRenderPass
    {
        readonly Settings settings;
        Material material;

        static readonly int FocusYId = Shader.PropertyToID("_FocusY");
        static readonly int FocusBandId = Shader.PropertyToID("_FocusBand");
        static readonly int FalloffId = Shader.PropertyToID("_Falloff");
        static readonly int MaxBlurId = Shader.PropertyToID("_MaxBlur");
        static readonly int CurveId = Shader.PropertyToID("_Curve");
        static readonly int TexelSizeId = Shader.PropertyToID("_BlitTexture_TexelSize");

        public TiltShiftPass(Settings settings)
        {
            this.settings = settings;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (settings.shader == null) return;

            if (material == null)
                material = CoreUtils.CreateEngineMaterial(settings.shader);
            if (material == null) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;

            var desc = renderGraph.GetTextureDesc(source);

            material.SetFloat(FocusYId, settings.focusY);
            material.SetFloat(FocusBandId, settings.focusBand);
            material.SetFloat(FalloffId, settings.falloff);
            material.SetFloat(MaxBlurId, settings.maxBlur);
            material.SetFloat(CurveId, settings.curve);

            var tempDesc = desc;
            tempDesc.name = "_HD2D_TiltShiftTemp";
            tempDesc.clearBuffer = false;
            tempDesc.msaaSamples = MSAASamples.None;
            var temp = renderGraph.CreateTexture(tempDesc);

            // source -> temp 复制一份，避免同一个 RT 同时读写
            renderGraph.AddBlitPass(source, temp, Vector2.one, Vector2.zero,
                passName: "TiltShift Copy");

            // temp -> source 应用移轴模糊
            renderGraph.AddBlitPass(
                new RenderGraphUtils.BlitMaterialParameters(temp, source, material, 0),
                passName: "TiltShift Blur");
        }

        public void Dispose()
        {
            CoreUtils.Destroy(material);
            material = null;
        }
    }
}
