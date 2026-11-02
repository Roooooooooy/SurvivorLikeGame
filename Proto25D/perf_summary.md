# HD-2D 1000 敌人性能验证总结

测试日期：2026-11-02  
场景：`Assets/Scenes/HD2D_EnemyPerf.unity`（复制自 `HD2D_Proto`，保留移轴 + Bloom + ACES 调色 + 主光/点光）  
测试机：Apple M5 Pro / Unity 6000.3.16f1 / URP 17.3  
Game View：1022×1452（约 1.48 MP），vsync=0，targetFrameRate=-1

## 1. 场景与压测方法

- 相机后拉到 `(0, 12, -20)`，俯角 30°，FOV 40，尽量覆盖敌群。
- 敌人类型：NormalA 40%、NormalB 28%、Fast 20%、Tank 6%、Elite 6%。  
  排除了 Exploder（自爆会减少存活数）、Ranged（16×16 贴图无 alpha）与 Boss。
- 敌人逻辑坐标 `float2 (x, y)` 映射到世界 `(x, 0, y)`；精灵使用脚底 pivot 的 billboard quad，
  按 Y 轴朝向相机；URP Unlit + Alpha Clip + ZWrite，不投射/接收实时阴影。
- 每个敌人脚下额外提交一个 XZ 平面椭圆阴影，实例化绘制。
- 两个采样阶段，各 2s/1.5s 预热 + 7s 采样：
  - A 纯渲染：`timeScale=0`，敌人静止，Job 的 dt=0，但矩阵构建/排序/实例化绘制全量执行。
  - B 全量仿真：`timeScale=1`，Burst 移动 + 空间哈希 + 接触检测 + 渲染。
- 指标：`Time.unscaledDeltaTime` 帧时间；`FrameTimingManager` 的 CPU 主线程/渲染线程/GPU；
  `EnemySimRenderer` 自报的实例化 draw call 与实际绘制实例数。
  （本 Unity 编辑器版本通过 `ProfilerRecorder` 拿不到 Rendering 模块的 “Draw Calls” 计数器，
  因此总场景 draw call 未采到；敌人自身的 draw call 是精确计数。）

## 2. 结果

### 阶段 A：纯渲染（静止分布）

| 敌人数 | 平均 FPS | 帧时间 | CPU 主线程 | CPU 渲染线程 | GPU | 敌人 DrawCall | 阴影 DrawCall | 实例数 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0    | 519.5 | 1.92 ms | 0.39 ms | 0.45 ms | 1.45 ms | 0 | 0 | 0 |
| 500  | 417.7 | 2.39 ms | 0.66 ms | 0.48 ms | 1.83 ms | 4 | 1 | 500 |
| 1000 | 412.1 | 2.43 ms | 0.88 ms | 0.50 ms | 1.90 ms | 4 | 1 | 1000 |
| 2000 | 298.1 | 3.35 ms | 1.30 ms | 0.52 ms | 2.61 ms | 4 | 2 | 2000 |

### 阶段 B：全量仿真（Burst 移动 + 空间哈希 + 渲染）

| 敌人数 | 平均 FPS | 帧时间 | CPU 主线程 | CPU 渲染线程 | GPU | 敌人 DrawCall | 阴影 DrawCall | 实例数 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0    | 486.2 | 2.06 ms | 0.39 ms | 0.45 ms | 1.52 ms | 0 | 0 | 0 |
| 500  | 443.1 | 2.26 ms | 0.71 ms | 0.49 ms | 1.79 ms | 4 | 1 | 500 |
| 1000 | 417.7 | 2.39 ms | 0.88 ms | 0.48 ms | 2.41 ms | 4 | 1 | 1000 |
| 2000 | 334.9 | 2.99 ms | 1.39 ms | 0.52 ms | 3.97 ms | 4 | 2 | 2000 |

1% Low 基本稳定在 200~215 FPS，不随敌人数明显变化，主要来自编辑器/呈现抖动。

## 3. 瓶颈分析

1. **1000 敌人的增量成本很小**
   - CPU 主线程：0 → 1000 增加约 **0.49 ms**（0.39 → 0.88 ms）。
   - GPU：静止分布增加约 **0.45 ms**（1.45 → 1.90 ms）；全量/聚集时增加约 **0.89 ms**（1.52 → 2.41 ms）。
   - 每只敌人约 **0.45~0.5 µs 主线程**、**0.4~0.9 µs GPU**，在 0~2000 范围内基本线性。

2. **最大的 GPU 开销是场景固有后处理，而不是敌人**
   - 0 敌人时 GPU 已有 **1.45~1.52 ms**，来自移轴（两次全屏 blit + 模糊采样）、Bloom、ACES 调色、
     地面/墙体/柱子/猫/4 个场景 billboard。
   - 1000 敌人时 GPU 约 1.9~2.4 ms，仍远低于 60 FPS 的 16.7 ms 预算。

3. **DrawCall 不是瓶颈**
   - 1000 敌人只有 **4 个实例化 draw call + 1 个阴影 draw call**（按贴图分批：
     绿/黄/蓝/红史莱姆共 4 张图；Tank 与 Elite 共用红史莱姆图）。
   - 2000 敌人仍是 4 个敌人 draw call，阴影因 1023 上限分成 2 个。

4. **不是 CPU 瓶颈**
   - 全量仿真下 1000 敌人的主线程 0.88 ms、渲染线程 0.48 ms；Burst 移动/空间哈希成本被压在 1 ms 内。
   - GPU 在 1000 敌人时高于 CPU 主线程，说明当前是 **GPU/填充率侧略主导**，但绝对余量很大。

5. **overdraw 是敌人侧的主要 GPU 成本**
   - 敌人 quad 使用整格图集尺寸（史莱姆单帧 2×4 世界单位，内容只占约 65% 高度），
     alpha clip 之前透明像素仍会跑片元着色器；精灵越大、重叠越多，GPU 越高。
   - 全量阶段敌人向玩家汇聚后重叠加剧，1000 敌人 GPU 从 1.90 ms 升到 2.41 ms。

## 4. 推荐方案

### 结论

- **不需要降低同屏敌人数。** 1000 敌人在本机（M5 Pro、1.48 MP、编辑器内）全量仿真约 2.4 ms/帧，
  仍有约 7 倍于 60 FPS 预算的余量；2000 敌人也仅约 3.0 ms/帧。
- **不需要用别的方案替换 DrawMeshInstanced。** `DrawMeshInstanced` 本身就是 GPU instancing，
  当前 4~5 个 draw call 已接近理论下限；换成 `RenderMeshIndirect`/`BatchRendererGroup` 不会带来
  可见收益，反而增加复杂度。只有在目标同屏 3000~5000+ 且需要逐实例视锥裁剪时才值得考虑。

### 优先级更高的优化（如果未来要冲 3000+ 或移动端）

1. **先优化移轴/Bloom 的固定后处理成本**：0 敌人时 GPU 已有 1.45 ms。
   移轴可改为半分辨率模糊或减少采样数；Bloom 可降 mip 或降低 intensity。
2. **裁剪精灵透明留白**：按每帧 alpha 包围盒收紧 quad 尺寸（或重导图集），
   可削减约 30%~50% 的片元着色面积；这是最直接的敌人侧 overdraw 优化。
3. **按空间分块做逐实例剔除**：当前 mesh bounds 被放大到 100000，所有实例都会被提交。
   若敌群会超出视野（例如 3000+ 或大地图），按 chunk 分批提交可省掉屏幕外实例。
4. **Bloom 视觉回归**：1000 个高亮史莱姆会把 Bloom（intensity 1.5、threshold 0.62）推到过曝；
   建议 HD-2D 场景单独调低 Bloom 或改用更保守的 threshold。
5. **构建时确保 shader 被打包**：`Survivor/URPInstancedSprite` 与 `Survivor/URPInstancedShadow`
   目前通过 `Shader.Find` 查找；正式出包前应加入 Always Included Shaders 或改由资源引用。
6. **阴影**：1000 敌人仅 1 个阴影 draw call，成本很低，建议保留；只有在移动端且敌人极多时再考虑
   只给近处敌人画阴影。

## 5. 复现方式

1. 菜单 `Tools/HD2D/5. Build 1000 Enemy Perf Scene` 生成/重建 `HD2D_EnemyPerf.unity`。
2. 打开该场景，进入 Play。`EnemyPerfStress` 会自动生成敌人、跑两个阶段、写报告和截图。
3. 输出目录：仓库根目录 `Proto25D/`：
   - `perf_{count}_enemies.md`（原始指标，已入库）
   - `08_{count}_enemies_static.png` / `09_{count}_enemies_live.png` / `10_{count}_enemies_report.png`
     （截图仅本地保留，未提交到仓库）

## 6. 注意事项

- 以上是 **编辑器内** 数据；Game View 为 1022×1452。按像素数换算到 1920×1080（约 1.4×），
  GPU 仍远低于 16.7 ms，结论不变。正式性能验证建议再跑一次 Development Build。
- 本 Unity 编辑器版本中 `ProfilerRecorder` 的 “Draw Calls / SetPass / Batches” 计数器不可用，
  报告里的敌人 draw call 来自 `EnemySimRenderer` 的精确计数；场景总 draw call 建议用 Frame Debugger 复核。
- `hd2dRendering` 是显式开关，默认 `false`；只有 `HD2D_EnemyPerf` 场景启用，
  未迁移的 `SampleScene` 仍走原来的 XY 映射与旧 shader。
