# HD-2D 迁移进度（2026-11-02）

## 目标
八方旅人式 HD-2D：3D 地面 + 2D billboard 精灵 + 实时阴影 + 移轴景深。同屏敌人目标约 1000。

## 已完成
- 切换 URP 17.3（`Packages/manifest.json`、`ProjectSettings/GraphicsSettings.asset`）
- URP 资源：`Assets/Settings/HD2D_Pipeline.asset`、`HD2D_Renderer.asset`
- 后处理 Profile：`Assets/Settings/HD2D_Profile.asset`
  - Bloom / ColorAdjustments / SplitToning / Vignette / Tonemapping / FilmGrain
  - DOF 已设为 Off（移轴改由自定义 pass 负责）
- 精灵着色器：`Assets/Shaders/HD2DSprite.shader`（URP Lit + alpha clip + ShadowCaster + DepthOnly）
- Billboard 脚本：`Assets/Scripts/Player/BillboardSprite.cs`
- 移轴后处理：`Assets/Shaders/TiltShift.shader` + `Assets/Scripts/Rendering/TiltShiftFeature.cs`
- 验证场景：`Assets/Scenes/HD2D_Proto.unity`
- 生成脚本：`Assets/Editor/HD2DSetup.cs`、`Assets/Editor/HD2DSceneSetup.cs`
- 3/4 视角素材：`Assets/Resources/Proto25D/`（cat/goblin/pillar）
- 截图：`Proto25D/05_hd2d_urp.png`

## 已修掉的坑
1. `Volume.profile` setter 不序列化 → 必须用 `sharedProfile`
2. `VolumeProfile.Add<T>()` 不写入 asset → 必须 `AssetDatabase.AddObjectToAsset`
3. RenderGraph 的 `AddBlitPass` 用全屏三角形（SV_VertexID）→ shader 必须 include core 的 `Blit.hlsl`，不能自己写 `TransformObjectToHClip`
4. TiltShift pass 在 SceneView 也执行会导致编辑器持续重绘 → 已限制只对 `CameraType.Game` 执行

## 已验证
- TiltShift 移轴生效：中间清晰带、上下高斯虚化，截图 `Proto25D/06_hd2d_tiltshift.png`
- Volume 后处理（Bloom / 调色 / 暗角）正常
- 精灵 billboard + 实时投影正常

## 待验证
- 1000 敌人 URP instanced 渲染 + 假光照/贴地阴影的性能
- 主场景 SampleScene 迁移后的兼容性（内置管线的 ScreenFX / InstancedSprite 尚未适配 URP）

## 备注
- 编辑器曾因 TiltShift pass 在 SceneView 执行而持续重绘卡死；已限制只对 `CameraType.Game` 执行。
- 截图流程：进 Play → `Tools/HD2D/4. Capture HD-2D Preview`（Play 模式走 ScreenCapture，Edit 模式的手动 cam.Render 不会执行 Renderer Feature）。
