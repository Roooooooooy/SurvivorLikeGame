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

## 2026-11-03 主场景 SampleScene HD-2D 迁移（未提交）

### 架构
- **逻辑仍然在 XY**：Rigidbody2D、CircleCollider2D、武器/拾取/敌人仿真的坐标与判定全部不变。
- **渲染映射到 XZ**：`Hd2dSpriteBridge` 把场景里的 `SpriteRenderer` 镜像成世界 `(x, 0, y)` 的 billboard。
  - 源 renderer 只设 `forceRenderingOff = true`，不改 `enabled`，所以闪烁/破坏/动画逻辑照常。
  - 角色用 `HD2D/SpriteLit`（受光/阴影），特效/弹体保留原材质（加法混合等）。
  - 主角使用 `ZTest Always + ZWrite Off` 且队列 2600，被大怪贴脸时仍然可见。
- 敌人继续走 `EnemySimRenderer` 的 `hd2dRendering=true` 路径（URP alpha clip + XZ + 实例化阴影）。
- 旧 2D 背景（`Background` / `BackgroundGrunge` / `BackgroundDecals`）关闭，换成跟随玩家的 3D 地面。

### 场景改动（`Tools/HD2D/7. Migrate SampleScene to HD-2D`）
- `Assets/Editor/SampleSceneHd2dSetup.cs`，可重复执行：
  - 相机正交 → 透视（FOV 35，30° 俯角，offset `(0, 9, -14)`），`CameraFollow.hd2d = true`
  - Canvas `ScreenSpaceOverlay`（HUD/升级/死亡/结算不再受 3D 相机与移轴后处理影响）
  - `HD2D_Ground`：Plane 200×200 + `HD2D_GroundGame.mat` + `Hd2dGroundFollow`
  - `GroundTorch`：跟随玩家的暖色点光，强度 1.1 / 范围 7
  - `EnemySimulation`（`hd2dRendering=true`）
  - `HD2D_Bridge`（`Hd2dSpriteBridge`，playerAmbient 0.75 / playerScale 1.3）
  - `HD2D_GlobalVolume`（`HD2D_Profile.asset`）
  - `HD2D_GameTuning`（priority 10，仅覆盖 Bloom：intensity 0.65 / threshold 0.90）
  - 方向光、雾、环境光；玩家起点回到 `(0,0)`

### 代码接入点
- `Assets/Scripts/Rendering/Hd2dSpriteBridge.cs`：billboard 代理、材质、深度状态
- `Assets/Scripts/Rendering/Hd2dGroundFollow.cs`：地面跟随
- `Assets/Scripts/System/SmallObjectPool.cs`：新增 `Spawned` 事件，池化弹体/拾取物自动登记
- `Assets/Scripts/System/ObstacleField.cs`：障碍生成后主动登记
- `Assets/Scripts/System/DamageNumbers.cs`：`Hd2d` 模式，文字映射到 `(x, 高度, y)` 并面向相机
- `Assets/Scripts/Weapons/LightningWeapon.cs`：电弧落在 `y=0.6` 的地面平面
- `Assets/Scripts/System/CameraFollow.cs`：`hd2d` 跟随模式（震屏投到 XZ）
- `Assets/Shaders/HD2DSprite.shader`：顶点色、`_ZTest`/`_ZWrite` 可配置

### 验证
- `/tmp/game_hd2d_final1.png`：实际游戏战斗画面（HUD、主角、敌人、伤害数字、地面）
- `/tmp/game_hd2d_title2.png`：实际游戏标题页（3D 地面 + 标题 UI）
- Console 0 error；实际游戏可正常开局、移动、击杀、升级。

### 已知限制 / 下一步
- 主角 `ZTest Always` 会盖住前景障碍；若之后加真正的 3D 墙体，需要改成“正常深度 + 被遮挡时画轮廓”的方案。
- 障碍、拾取物、弹体目前仍是 billboard；要把障碍换成低模地牢模块，需要按 `Obstacle` 类型映射到 `Assets/Meshes/HD2D`。
- 非 SpriteRenderer 的世界特效（LineRenderer 已处理，ParticleSystem 等未逐一验证）仍可能停留在 XY 平面。
- 代理扫描间隔 0.5s；非池化、运行时新建的精灵最多延迟半秒出现。
- 主场景敌人路径与 `HD2D_EnemyPerf` 相同；若改敌人 shader/渲染，再重跑 1000 敌人压测。

## 2026-11-03 晚：Spine 主角 + 低模障碍

### Spine 主角
- 资源来源：`bendmorris/catalyst-demo` 的 Catalyst 猫（Spine 3.6 数据，已转成 4.1 JSON）。
  - 28 个动作：`walk` / `run` / `sit` / `lay` / `sleep` / `attack-str|int|cha` / `hit` / `defeat` / `jump` / `fishing` / `petting` 等。
  - 2 套 skin：`custom`（基础猫身体）+ `default`（9 顶帽子、眼镜、龙翼/飞翼、纹样、表情等约 253 个附件）。
- 运行时：Spine Runtimes 4.1.43（从本机 `pics-blast` 项目复制），补全 `spine-csharp` 源码与 asmdef。
- 资产：`Assets/Resources/Player/CatSpine/`（`cat_spine.json` / `.atlas.txt` / `.png` + `_Material` / `_Atlas` / `_SkeletonData`），菜单 `Tools/HD2D/8. Build Cat Spine Assets`。
- `Hd2dSpinePlayer`：
  - 逻辑/碰撞仍走 `PlayerController` 的 XY；视图映射到 `(x, 0, y)` 并面向相机。
  - 动画：静止 `eyes blink`、移动 `walk` / `run`、命中 `attack-str`（订阅 `CombatEvents.HitDetailed`）、受击 `hit`、死亡 `defeat`。
  - 换装接口：`EquipSkin(name)`、`EquipAttachment(slot, attachment)`、`EquipGear(hat, accessory, wings)`。
  - 验证默认装备：`cat_hat_wizard` + `cat_accessory_glasses`；`viewScale=1.25`、`worldYOffset=-0.35`。
  - `default` skin 单独使用只有身体（setup 只挂部分附件），必须以 `custom` 为底再用 `EquipGear` 叠加装备。
- 材质：`Spine/Skeleton`（PMA）。额外提供 `Spine/SkeletonAlways`（ZTest Always，主角可压在怪物之上），需要时把材质 shader 切过去。
- 验证截图：`/tmp/game_spine_cat4.png`（完整猫 + 低模柱/箱）、`/tmp/game_spine_gear.png`（巫师帽 + 眼镜 + 火盆/石棺/木箱）。
- 临时自动开局脚本 `TempHd2dAutoStart` 已删除；正式进入仍走标题页“任意键”。

### 低模障碍
- `HD2DLowPolyKit` 新增 `HD2D_Brazier`（底座+火盆合并）、`HD2D_Sarcophagus`（棺体+盖）、`HD2D_Crate`。
- `Assets/Resources/HD2D/PropLibrary.asset` 映射：
  - pillar / statue → `HD2D_Column`
  - rubble → `HD2D_RubbleA`
  - brazier → `HD2D_Brazier`
  - sarcophagus → `HD2D_Sarcophagus`
  - crate → `HD2D_Crate`
- `Hd2dSpriteBridge` 对 `Obstacle` 生成 MeshRenderer 代理（Mesh + URP Lit 材质 + 确定性朝向 + 受击闪色 MPB），源 SpriteRenderer 隐藏；未知类型仍回退 billboard。
- 验证截图见上；Console 0 error。

### 授权 / 注意
- Catalyst 猫来自公开 GitHub 仓库 `bendmorris/catalyst-demo`，仓库本身没有 LICENSE 文件；它看起来是 Spine/Catalyst 示例资源。**发布前请确认 Spine Editor 授权与该猫素材的使用许可**。
- Spine 运行时采用 `EsotericSoftware/spine-runtimes` 4.1 的运行时许可；项目内 `Assets/Addons/Spine` 保留了其 LICENSE。
- 贴图导入时 Spine 提示 `Alpha Is Transparency` 与 PMA 冲突：如需彻底消掉边缘问题，可在 `CatSpineSetup` 里把 `alphaIsTransparency` 设为 false，或把材质改成 straight alpha。
- 本次没有重跑 1000 敌人压测；Spine 猫/低模障碍不改变敌人实例化路径。



