# 像素风 / Low Poly 3D 场景与 AI 资产工作流调研

核对日期：2026-11-10。范围：用于 Unity 游戏的场景、建筑、自然物与道具；只调研官方网页、文档和仓库，没有生成资产、运行模型或导入 Unity 实测。工具能力来自当前文档，画质、拓扑、碰撞与性能仍需用代表性资产验收。以下面数和贴图预算是试做建议，不是本项目已经确定的预算。

## 建议先试的组合

**整关场景：KayKit 模块化资源打底，AI 通过 Blender 建模脚本 / MCP 或 Unity 编辑工具拼装，缺少的特殊道具再用 Meshy 或 Tripo 生成。** 这条路线能保留独立建筑、树木、宝箱和障碍物，方便修改尺度、碰撞、导航与遮挡。

**两款 AI 资产工具优先试 Meshy 和 Sloyd。** Meshy 适合从统一参考图生成特殊道具；Sloyd 的参数化模板适合量产可控的箱子、家具、武器与建筑变体。Tripo P1 是值得用同一参考图对比的第三个候选，已有明确低面结构接口。像素风再加 Blockbench / PixelLab 做贴图与造型统一。

“像素风 3D”与“Low Poly”分别涉及像素纹理 / 渲染和几何造型。只减少三角形或把大贴图降尺寸，不能自动得到一致的像素美术。

## 可直接利用的场景资源

| 资源 | 官方核实的内容 | 适合用途 | 许可 / 导出 |
| --- | --- | --- | --- |
| [KayKit Dungeon Remastered](https://kaylousberg.com/game-assets/dungeon-remastered) | 200+ 风格化地牢资产与道具；模块化墙、地板、楼梯、门，以及宝箱、桶、家具、机关、旗帜 | 地牢 / 废墟关卡，先搭可玩的整关 | CC0，个人与商用无需署名；FBX / glTF / OBJ；使用单张 1024² 渐变 atlas，官方说明可降至 128² |
| [KayKit Forest Nature Pack](https://kaylousberg.com/game-assets/forest-nature-pack) | FREE 层 100+ 树、石、灌木和草；EXTRA 层额外有模块化地形和更多颜色 | 林地、野外、战斗区域周边 | CC0；FBX / glTF / OBJ；渐变 atlas。免费层不等于包含 EXTRA 层全部地形 |
| [Quaternius Ultimate Stylized Nature](https://quaternius.com/packs/ultimatestylizednature.html) | 官方页列 63 models，正文为 60+ 自然资产，含无缝贴图和法线贴图 | 更有手绘质感的树林 / 草地 | CC0，个人 / 商用；FBX / OBJ / Blend / glTF |

更小的原型集合：[Kenney Mini Dungeon](https://kenney.nl/assets/mini-dungeon)（30 files）与 [Mini Forest](https://kenney.nl/assets/mini-forest)（20 files），官方均标 CC0。这些数字是文件数，不等同于独立模型数量。

KayKit 下载入口：[Dungeon Remastered](https://kaylousberg.itch.io/kaykit-dungeon-remastered)、[Forest Nature Pack](https://kaylousberg.itch.io/kaykit-forest)。资源包的渐变低分辨率 atlas 属于低多边形着色路线，不应直接称作像素贴图。

## AI 工具对比

| 工具 / 路线 | 可核实的输入与输出 | Low Poly / 风格控制 | 本项目定位与关键限制 |
| --- | --- | --- | --- |
| [Meshy](https://docs.meshy.ai/api/models) | 文本 / 单图 / 多图；输出实际 mesh、贴图；API 导出 GLB / FBX / OBJ 等，网页有 Blender / Unity 插件 | Smart Topology T2 / T2.5 原生生成目标面数的三角网格与分件；N1 偏风格化 base color | 优先试特殊道具。统一参考图比单纯反复写风格提示更易验收；多图几何接口不支持 Smart Topology 模型 |
| [Sloyd](https://www.sloyd.ai/templates) | 参数化模板 + AI 调参，也另有文本 / 图片生成；UV / LOD / 贴图 / 分件导出；插件与 REST API | 模板可用滑杆改造型；生成 API 可选面数、triangle / quad、贴图至 4K | 优先试常见道具、建筑变体；模板编辑器本身是参数化模型，AI 是可选调参助手；可生成对象仍受模板覆盖范围影响 |
| [Tripo](https://docs.tripo3d.ai/model-generation/image-to-model-p1-20260311.html) | 文本 / 单图 / 多视角生成，贴图 / PBR / 分件 / 编辑 / 转换 API | P1 明确面向低面结构，单图接口 face_limit 48–20,000；H3 有 smart_low_poly 与 quad 选项 | 与 Meshy 对比同一件道具；高级参数与后处理可能额外消耗 credits。主站许可页本次 403，免费商用未确认 |
| [Hunyuan3D 2.1](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1) | 参考图 → mesh → PBR 贴图；开放权重 / 代码，可用 Python；2.0 仓库有本地 API / Blender 插件与 GLB / OBJ 示例 | 适合可控的自部署和后续定制；不应默认把生成网格当低面游戏资产 | 有显卡服务器时再考虑；2.1 README 标 shape 10 GB、texture 21 GB、两者合计 29 GB 显存，本机 Mac 性能未验证；不是 MIT 许可 |
| [Rodin / Hyper3D](https://hyper3d.ai) | 文本 / 单图 / 可选多图；mesh / UV / PBR；常见 3D 格式、API、DCC / Unity 插件 | 提供 Smart Low-Poly、局部编辑和空间控制 | 适合复杂道具备选；本次依据官网能力描述，没有生成对比；免费预览不等于免费导出和商用 |
| [World Labs Marble](https://docs.worldlabs.ai/marble/export/mesh) | 文本 / 图 / 全景 / 多图 / 视频 / 粗布局 → 3D world；splats，另可导 collider GLB 与 HQ mesh GLB | 世界重建以 splats 保留最高视觉质量；HQ mesh 为高面重建结果 | 更适合概念空间 / 背景验证。可以导 mesh，但不是一键生成结构干净、模块独立的低面可玩关卡 |

## 免费、商用与自动化边界

- **Meshy**：[当前定价与许可](https://www.meshy.ai/pricing)写 Free 输出采用 CC BY 4.0，可商用但需署名；免费有月度 credits / 下载限制。付费提供私有资产许可、Remesh、插件、API / MCP。API 新建任务要求付费计划，网页版免费额度不能直接当免费自动化额度。
- **Meshy 当前模型**：[Models](https://docs.meshy.ai/api/models)中 T2 为 100–15,000 faces，默认 4,000；T2.5 为 100–30,000，默认 15,000；只输出 triangles。N1 为 2K base color，无 PBR / 文本贴图提示；需要细节和 PBR 时换相应纹理模型。旧 `lowpoly` 模型在 [Text to 3D 文档](https://docs.meshy.ai/en/api/text-to-3d)标注 2026-10-30 退休，不宜照旧教程调用。
- **Sloyd**：[定价](https://www.sloyd.ai/pricing)中 Guest 是 personal-use；Plus 包含商业使用，Pro 增加转售许可。模板页泛称可商业使用，但下载与具体权限以实际套餐为准。基础生成“unlimited”不覆盖所有多图、分件、场景等高级操作，这些仍可能消耗 credits。
- **Sloyd API**：[官方 API](https://www.sloyd.ai/api)是独立预付 credits，不包含在 Web App 订阅中；可异步轮询任务，支持重贴图、split-to-parts；导出 GLB / FBX / OBJ / BLEND 等格式。获得 API 凭据需要正余额；本次没有购买或调用。
- **Tripo**：[API 价目](https://docs.tripo3d.ai/get-started/pricing.html)分别列模型、贴图、分件、Smart Low Poly 与转换费用。H2 / H3 的 smart_low_poly / quad 等有附加成本，P1 按整任务计价。网页免费额度、免费模型商业权本次未核实，不据旧文章推断。
- **Hunyuan3D**：[2.1 LICENSE](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1/blob/main/LICENSE)为 Tencent Community License，含地域与商业使用条件；模型、在线服务、生成输出的权限需要分别核对，不能标成“MIT、无条件免费商用”。作为云端 / 自部署备选，本次不安装。
- **Rodin**：[当前定价](https://hyper3d.ai/pricing)把无限导出 / 商用、API、较高贴图与几何选项放在不同套餐中。官网称可免费起步，但本次未确认 Free 输出的完整商业许可，不给免费商用承诺。
- **Marble**：[导出说明](https://docs.worldlabs.ai/marble/export/mesh)写 Standard 有 collider 导出，Pro 有 HQ textured mesh；[套餐文档](https://docs.worldlabs.ai/marble/support/account-billing)把商业权列于 Pro，[TOS 3.3](https://www.worldlabs.ai/terms-of-service)则写 Free 输出仅个人非商业、Paid / API 输出可商业使用。两处口径不完全一致，实际选择套餐前需核对对应 Order Form；免费生成时的输出不会因后来升级自动改变许可。

以上是查阅时的权限重点，不是价格承诺。网页套餐、API 计费与模型版本会变化，开始付费试做时应重新核对。

## 路线 A：模块化 Low Poly 场景

1. 先选一套 KayKit / Kenney / Quaternius 资源作为轮廓、颜色与材质基准；避免同一关直接混入多套差异很大的树、建筑与道具。
2. 做一个可玩的灰盒：地面、墙、战斗面积、道路、刷怪点、出口、交互点与镜头遮挡。AI 可以生成布局数据和 Blender Python / Unity 编辑脚本，规则保存在文件中，便于重复生成与局部修改。
3. 用资源包的独立模块替换灰盒。墙、地板、树木按统一网格 / 尺度拼装；常见变体交给 Sloyd 参数化模板或 Blender 脚本修改。
4. 对缺少的少量特殊道具，按路线 B 单独生成；在 Blender 中校验原点、尺度、法线、UV、分件，做简化碰撞体再导出 FBX。
5. Unity 配置材质、Prefab、LOD / instancing、Collider 和导航，检查关卡可达性、镜头遮挡、角色与物件比例，再看真实目标设备帧耗。

可以使用 [mcp-for-blender](https://github.com/ahujasid/mcp-for-blender)让 AI 通过 Blender Python 创建 / 编辑模型、材质、场景并检查视图；它是第三方集成，旧地址 `ahujasid/blender-mcp` 已重定向。仓库集成多个资源平台和 3D 生成服务，并不表示这些付费生成自动免费，也不保证一轮提示完成好用关卡。本次没有安装 MCP、修改 Blender 或下载模型。

## 路线 B：参考图 → 特殊道具 mesh

1. 先做一张风格基准图：白底、单主体、清楚的三分之四视角，轮廓 / 配色与已有资产一致。可以用已有合法素材，或另行选择图像生成工具。建议以宝箱、路灯、祭坛、树桩等静态道具起步。
2. 优先使用 Meshy Smart Topology T2 / T2.5 + 风格化纹理；同一张图用 Tripo P1 做对照。示例试做预算：小道具 300–1,500 triangles，较大地标 2,000–5,000；它们只是初始目标，最终看实际镜头大小与屏幕同显数量。
3. 先检查裸网格，再决定贴图；不要让好看的贴图掩盖破损轮廓。单图看不见的背面属于生成推断；有结构问题时补参考图或直接修网格。
4. Meshy 当前 Smart Topology 只接单图或文本；若必须多图约束几何，应使用其标准 Multi-Image 模型，或 Tripo 对应多视角接口，再评估简化结果。不要把不同接口的参数拼在一起。
5. Blender / Blockbench 修比例、分件、原点与 UV，统一材质和贴图尺寸；导出 FBX（或经已配置的 glTF 导入路径）。Unity 不因收到 GLB 就自动具备所有 glTF 导入、碰撞与导航能力。
6. 做 Prefab 并记录 prompt、参考图、模型版本、seed（工具支持时）、目标面数、生成实际面数、许可与修改步骤。验收形状、侧背面、UV、法线、透明贴图、碰撞以及同屏多实例表现。

Meshy / Sloyd / Tripo 都可通过 API 自动化批量提交 → 轮询 → 下载 → 导入；适合先有风格基准和验收规则后再自动跑。异步任务超时不等于失败，不应盲目重提付费任务。本轮没有调用生成接口。

## 路线 C：像素纹理 + 低面几何

1. 用 [Blockbench](https://www.blockbench.net/)制作 cuboid / low-poly 几何，工具自带 UV、调色板、模型上绘制贴图与动画；免费开源。建筑、箱子、武器等规则形体很适合这种人工可编辑的路线，AI 可辅助规划形体与写建模脚本。
2. 用 [PixelLab](https://www.pixellab.ai/)准备像素参考图、物品或贴图元素：[consistent-style](https://www.pixellab.ai/docs/tools/consistent-style)支持多参考图统一风格（文档列 Tier 1、最大 512²）。其场景 / 地图工具是二维图像，例如 [create-map](https://www.pixellab.ai/docs/tools/create-map)为 top-down / side-scroll pixel map，不输出可编辑 3D 世界；生成结果是否无缝必须另验。
3. 把批准的图像绘制 / 整理为小尺寸 UV atlas（例如先试 64² / 128² / 256²），按实际世界尺寸统一 texel density。先处理接缝、透明边与像素排列，再贴到低面网格。
4. Unity 使用合适的 Point 采样。[官方 FilterMode.Point](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/filtermode/point)定义为纹理像素近看呈块状；是否禁用 mipmaps、压缩与其他采样选项要按镜头 / 闪烁 / 目标设备评估，不能一概关闭。
5. 需要整体像素化画面时再考虑低分辨率渲染与调色板限制，实测移动镜头下闪烁和可读性。后处理像素化与资产本身的像素纹理是两个可分别调节的层面。

[Sprytile](https://github.com/Sprytile/Sprytile) 可作为后续资料：它是按 tile 贴图构造 3D 场景的社区路线，但官方仓库仍提 Blender 2.8 port，当前现代 Blender 兼容没有验证，因此不作为本次首选。

## 完整场景 AI 的实际边界：Marble

- 它不只是视频：默认世界可导 SPZ / PLY Gaussian splats，也能导实际 collider / HQ mesh GLB。
- [官方规格](https://docs.worldlabs.ai/marble/export/specs)写 collider 约 100–200k triangles；HQ 有约 600k triangles + 贴图版本和约 1M triangles + 顶点色版本。HQ 导出可耗时至一小时；这些不是 Low Poly 场景预算。
- Collider mesh 用于简化物理，官方不建议拿它当视觉网格；HQ 是从世界重建，官方明确可能有洞、浮块、软塌表面与未覆盖区域瑕疵。
- [Unity 导出说明](https://docs.worldlabs.ai/marble/export/gaussian-splat/unity)依赖第三方 Gaussian Splatting 插件；能看到 splats 不表示能像独立家具 / 墙体一样编辑，更不表示已经有正确碰撞和寻路。官方说明 Unity 内没有它所支持的直接 splat → mesh 转换，建议用 Marble 自身 mesh 导出。
- 官方 Unity 页面混有社区历史排障信息，版本 / VR 描述不能直接用于证明本项目 Unity 6.3 兼容或不兼容。本次没有在本项目验证插件。

因此 Marble 值得用于“参考图快速形成可漫游空间、气氛和构图研究”，而可玩 Low Poly 场景仍优先模块化拼装；若要用其 mesh 正式制作，需另做清理、拆件、减面、碰撞与导航验收。

## 最小试做建议

先做一个约 20m × 20m 的森林 / 地牢样板：一套 KayKit 基础、3 个 AI 特殊道具、5–10 个可控模板变体。以同一参考图对比 Meshy / Tripo，以同一用途对比 Sloyd 模板。通过后再扩大批量生成；不先生成整张巨型场景。

这一样板能同时回答：美术是否统一、生成道具是否真的省修模时间、能否做有效碰撞 / 导航、在游戏实际镜头与目标设备上是否合适。当前结论是工作流筛选，没有资产质量实测。
