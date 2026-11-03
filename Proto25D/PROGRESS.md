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

## 2026-11-02 美术打磨（未提交）

### 地面重复感
- 新增 `Assets/Shaders/HD2DGround.shader`，全部按世界坐标采样，地面 Plane 的 scale 不影响纹理尺度：
  - 基础层 `bg_stone_floor`，约 16m 重复
  - 低频层 `bg_stone_floor`（0.019/m，约 52m 周期），只调制明暗、不叠砖缝
  - 细节层 `bg_dungeon_floor`，只在污渍区域以乘法增强
  - 宏观层 `bg_grunge_large`，约 70m 周期，做明暗/暖色污渍
- 三层尺度互不成整数比，重复周期被错开；`HD2D_Ground.mat` 已切到该 shader。
- 可调参数：`_BaseScale` / `_BroadScale` / `_DetailScale` / `_MacroScale` / `_BroadStrength` / `_DetailStrength` / `_MacroStrength` / `_Ambient`。

### 猫偏白
- `HD2D/SpriteLit` 新增可选描边：`_OutlineColor`、`_OutlineWidth`（texel，默认 0，其他精灵完全不受影响）。
- `HD2D_Cat.mat`：`_AmbientBoost` 0.75 → 0.30，`_Color` 调成中性银灰，描边 1 texel 冷蓝；受光面不再过曝。
- 描边在 8 邻域采样，宽为 0 时不执行额外采样。

### 场景元素方硬
- 新增 `Assets/Editor/HD2DLowPolyKit.cs`（菜单 `Tools/HD2D/6. Build Low-Poly Dungeon Kit`）：
  - 断墙：沿 Z 分 18 列、顶部高度随机，形成参差断口
  - 石柱：八棱底座 + 收分柱身 + 外扩柱头，柱头顶面随机压低
  - 高台/台阶：6 面内缩 + 12 条倒角边 + 8 个角三角面，顶面边缘带破损
  - 火盆、碎石：低边数棱柱 / 强噪声倒角盒
- 生成资产在 `Assets/Meshes/HD2D/`；Mesh 与材质都原地更新、保留 GUID，重跑菜单不会让已打开场景丢引用。
- `HD2DSceneSetup.CreateEnvironment` 已全部改用这些 Mesh，不再用 Cube/Cylinder 缩放。
- 火盆点光从 1.6m 抬到 2.1m、强度 4→3，避免把盆体照成发光球；盆体改用暗石材质。

### 验证
- `/tmp/hd2d_final_game.png`：HD2D_Proto 游戏视图（猫、地面、高台）
- `/tmp/hd2d_final_overview2.png`：HD2D_EnemyPerf 拉远视角（断墙、八角柱、倒角高台、火盆）
- Console 0 error；`HD2D_EnemyPerf` 已按新 HD2D_Proto 重建，压测驱动/1000 敌人渲染路径未改动。
- 没有重跑 1000 敌人压测：避免覆盖已入库的 `perf_*.md`，且本次只改环境材质/几何，不影响敌人实例化渲染。

### 重建顺序
1. `Tools/HD2D/3. Setup HD-2D Scene`（内部会自动跑菜单 6）
2. `Tools/HD2D/5. Build 1000 Enemy Perf Scene`（从 HD2D_Proto 复制，必须跟在 3 之后）

