# HD-2D 500 敌人性能实测

- 时间: 2026-11-02 21:00:34
- Unity: 6000.3.16f1
- 分辨率: 1022x1452  (vsync=0, targetFrameRate=-1)
- GPU: Apple M5 Pro
- CPU: Apple M5 Pro
- 敌人数量: 500  (请求 500)
- 类型分布: NormalA=201, NormalB=140, Fast=100, Tank=30, Elite=29

## 阶段 A：纯渲染（timeScale=0，静止分布）
- 样本帧数: 2924  (累计 7001 ms，剔除 >200ms 卡顿帧 0 个)
- 平均 FPS（稳定帧）: 417.7
- 平均帧时间（稳定帧）: 2.39 ms  (P95 4.60 ms, P99 4.84 ms)
- 原始平均帧时间（含卡顿）: 2.39 ms，最差帧 13.06 ms
- 1% Low FPS: 206.8
- CPU 主线程: 0.66 ms  (FrameTimingManager, 2924 样本)
- CPU 渲染线程: 0.48 ms
- GPU: 1.83 ms  (2923 样本)
- 场景总 Draw Calls: 指标不可用（见下方 Profiler 计数器诊断）
- 敌人实例化 Draw Calls: 平均 4.0, 峰值 4
- 贴地阴影 Draw Calls: 平均 1.0, 峰值 1
- 实际绘制实例数: 平均 500.0, 峰值 500

## 阶段 B：全量仿真（timeScale=1，Burst 移动 + 空间哈希 + 渲染）
- 样本帧数: 3102  (累计 7001 ms，剔除 >200ms 卡顿帧 0 个)
- 平均 FPS（稳定帧）: 443.1
- 平均帧时间（稳定帧）: 2.26 ms  (P95 3.52 ms, P99 4.65 ms)
- 原始平均帧时间（含卡顿）: 2.26 ms，最差帧 10.90 ms
- 1% Low FPS: 215.0
- CPU 主线程: 0.71 ms  (FrameTimingManager, 3102 样本)
- CPU 渲染线程: 0.49 ms
- GPU: 1.79 ms  (3102 样本)
- 场景总 Draw Calls: 指标不可用（见下方 Profiler 计数器诊断）
- 敌人实例化 Draw Calls: 平均 4.0, 峰值 4
- 贴地阴影 Draw Calls: 平均 1.0, 峰值 1
- 实际绘制实例数: 平均 500.0, 峰值 500

## Profiler 计数器诊断
- Draw Calls recorder: missing:Render/Draw Calls
- SetPass recorder: missing:Render/SetPass Calls
- Batches recorder: missing:Render/Batches
- Main Thread recorder: Internal/Main Thread/TimeNanoseconds
- Render Thread recorder: missing:Internal/Render Thread
  - Render :: Wait for swapchain drawable [TimeNanoseconds]
  - Render :: GpuProgramMetal.GetCachedPipeline [TimeNanoseconds]
  - Render :: GpuProgramMetal.CreateCachedPipelineAsync [TimeNanoseconds]
  - Render :: Wait for nextDrawable (display link) [TimeNanoseconds]
  - Render :: GPUProfiler.EndQueries [TimeNanoseconds]
  - Render :: GPUProfiler.BeginQueries [TimeNanoseconds]
  - Render :: Gfx.DrawDynamic [TimeNanoseconds]
  - Other :: TimelineView.DrawLabels [TimeNanoseconds]
  - Other :: TimelineView.DrawMeshes [TimeNanoseconds]
  - Other :: TimelineView.Draw [TimeNanoseconds]
  - Render :: SceneCamera.PrepareRenderBatch [TimeNanoseconds]
  - Render :: SceneCamera.RenderBatch [TimeNanoseconds]
  - Render :: GizmoRenderer.RenderBatches [TimeNanoseconds]
  - Other :: GizmoManager.DrawAllGizmos [TimeNanoseconds]
  - Lighting :: RadeonRaysJobCompositeLightmap.CompositeLightmapGPU [TimeNanoseconds]
  - Lighting :: GPULightmapper.Lightmaps.CopyCurrentBakedTileResultFromGPUToCPU [TimeNanoseconds]
  - Lighting :: GPULightmapper.Lightmaps.IncrementTilingPassNumAndUpdateBuffersIfNeeded [TimeNanoseconds]
  - Lighting :: GPULightmapper.Lightmaps.Render [TimeNanoseconds]
  - Lighting :: GPULightmapper.RadeonRaysMeshManager.AddInstance [TimeNanoseconds]
  - Lighting :: GPULightmapper.UpdateGPUTimings [TimeNanoseconds]
  - Lighting :: GPULightmapper.Lightprobes.IntegrateFromAsyncReadBack [TimeNanoseconds]
  - Lighting :: GPULightmapper.Lightprobes.UpdateFromAsyncReadBack [TimeNanoseconds]
  - Lighting :: GPULightmapper.Lightprobes.ApplyLatestProbeData [TimeNanoseconds]
  - Lighting :: GPULightmapper.Lightprobes.ApplyUpdateFromAsyncReadBacks [TimeNanoseconds]
  - Lighting :: GPULightmapper.Lightprobes.TryUpdateFromAsyncReadBacks [TimeNanoseconds]


## 结论提示
- Draw Calls 主要由贴图批次数决定：每种敌人贴图 1~2 个实例化 draw call，阴影额外 1~2 个。
- 若 GPU 耗时明显高于 CPU 主线程，瓶颈在填充率/overdraw（精灵重叠、移轴模糊、Bloom）。
- 编辑器内 FrameTimingManager 的 GPU 值可能为 0（受平台/设置限制），以 Profiler 的 GPU 模块为准。
