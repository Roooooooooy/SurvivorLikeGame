# KayKit 木箱、桌子的受击碎裂方案

调研日期：2026-11-10。项目现场：Unity `6000.3.16f1`、URP `17.3.0`，实际工程目录为 `SurvivorLikeGame/`。只查询官方网页、官方源码和本地资产清单；没有购买、安装或运行下面的碎裂插件，不能把官方兼容表当成本项目实测结果。

## 建议采用的路线

先使用现有 KayKit 模型，制作**完整模型 → 受损裂缝 → 预碎木板散落**的表现。木箱沿板缝拆开，桌面裂成少量长片，桌腿和横梁断开；小木屑、灰尘和音效补足冲击感。先做可控的预碎资源，运行时只播放短动画或启用少量碎片，不需要每次受击重新计算网格切割。

这是本项目的美术与工程建议：对于木箱和桌子，沿木纹与构件断裂更容易看出“木头裂开”；随机、近等体积的 Voronoi 碎块容易显得像石头或混凝土。不是说碎裂工具不能做木头——RayFire 2 就有 `Splinters` 和 `Slabs`，但此样板暂不需要为少量道具引入整套付费破坏系统。[1]

本地 `Assets/Art/KayKitDungeon/Models/` 文件名清单含 `crates_stacked.fbx`、`table_medium.fbx`，以及破地砖和破墙；**未发现专用碎木箱或碎桌模型**。不能据此断言作者整个完整资源库都没有碎木。当前免费包的 `License.txt` 明确为 CC0，允许个人、教育、商业使用；适合从现有资源制作本项目的碎片变体。[5]

## 已核实的候选

| 方案 | 官方能力与边界 | 成本、许可与兼容证据 | 对当前样板的选择 |
| --- | --- | --- | --- |
| **本地预碎木板 + 原材质** | 把箱板、桌面、桌腿制作成独立闭合网格，完整时隐藏碎片，死亡时切换。切割形状、UV、数量都由本项目控制。它是实施路线，不是现成插件。 | KayKit 当前包 CC0；不增加外部插件。URP 继续使用现有材质，但具体变体仍须进 Play 看效果。[5] | **首选**。最容易控制木质感、战斗命中、存档和碎片是否阻挡。 |
| **[Blender Cell Fracture](https://extensions.blender.org/add-ons/cell-fracture/)** | 在 Blender 离线生成碎片，再导出给 Unity；官方扩展页称 `Fractured Object Creation`。可用于给木板做少量不规则断面，整张桌子仍建议先按构件拆分。 | 免费；扩展 GPL-3.0-or-later，当前页版本 `0.2.1`、Blender `4.2 LTS and newer`。页面明确写着从 Blender 4.1 bundled add-ons 移出后按现状提供、支持有限。Blender 官方另明确生成的 artwork/.blend/其他输出数据归创作者使用，软件 GPL 不要求模型输出一并开源。[4] | 可选离线制作工具。无需 Unity 运行时切割；本次没安装扩展、没验证 FBX/UV 输出。 |
| **[OpenFracture](https://github.com/dgreenheck/OpenFracture)** | 编辑器预碎与运行时切割；外表 UV 沿切边保留，内部切面可用独立材质和 UV 缩放/偏移；可保存碎片网格制作 Prefab。必须闭合、无自交；仅支持输入单子网格；不支持按命中点局部碎裂，随机递归切割。其异步运行时处理仍为单线程。[3] | MIT，免费。`package.json` 当前 `1.0.2`、声明 Unity `2021.1`；这不是 Unity 6.3/URP 官方实测声明。碎片源码自动建立 3D Rigidbody 与 convex MeshCollider，示例材质还须检查/替换。[3] | 若后面要批量离线预碎石柱，可在隔离样例工程试用。桌腿/横梁相交的模型先拆开；不建议直接对整个桌子运行时切。 |
| **[DinoFracture](https://assetstore.unity.com/packages/tools/physics/dinofracture-a-dynamic-fracture-library-26599)** | 编辑器预碎、运行时切割、自定义切面、按选定点聚集碎片，支持 skinned mesh；官方 docs 说明支持平台上使用多线程。Quickstart 可把碎片保存成 Prefab，默认每片有 convex MeshCollider + Rigidbody。[2] | 当前商店 `$40`、Extension Asset、Standard Unity Asset Store EULA、版本 `2.11.4`。商店列 `6000.0.78f1`、`6000.5.1f1`、`2022.3.62f3` 的 Built-in/URP/HDRP 均 Compatible；未单列本项目 `6000.3.16f1`。官网称兼容所有 render pipelines。[2] | 商业候选中相对轻量，若后面确需运行时切割可评估；当前不用购买。 |
| **[RayFire 2](https://assetstore.unity.com/packages/tools/game-toolkits/rayfire-2-342492)** | `Splinters`、`Slabs`、Voronoi、Slice 等碎片形状；`Reference` 可替换预制破坏模型，`Precache` 预备网格，`Prefragment` 预备并隐藏碎片；还有连通、集群、灰尘与碎屑系统。注意 Awake 预备不等同于制作期离线烘焙，仍可能有启动成本。[1] | 当前商店 `$190`、Extension Asset、Standard Unity Asset Store EULA、版本 `2.23`；商店兼容表列 Unity `2022.3.62f2` 的 Built-in/URP/HDRP Compatible，作者产品页另声明 Unity 6 支持。**旧 `148690` 包已经废弃并被 RayFire 2 替代**，不能再按旧包选型。[1] | 对大片建筑可破坏更有价值。木箱、桌子仅少量道具时，成本和接入范围偏大；本次不引入。 |

价格与版本是上述日期的网页读回，可能变化。Extension Asset 的席位授权按商店 EULA，不能当 CC0 素材使用。

## 当前 2D 战斗与 3D 碎片的接法

现有 KayKit 样板是**XY 方向的 2D 角色/战斗/碰撞，XZ 方向显示 3D 模型**，不是完整的 3D 物理玩法。现场依据：`Docs/KayKit_Region_Workflow.md` 记录 Collider2D 和 XY/XZ 映射。OpenFracture/DinoFracture 默认生成的 3D Rigidbody、MeshCollider 不会自动接入现有 Collider2D 命中和墙体，也不会自动落在现在的 2D 地面上。[2][3]

建议分开处理：

1. 完整物体保留现有 2D 阻挡，由现有攻击命中通路扣除道具生命；一次攻击对同一个道具只结算一次。不要依赖身体碰撞力度触发破坏，否则敌人或玩家挤压也会拆桌子。
2. 未死亡的命中给短暂摇动、木屑，达到受损阶段再显露一条裂缝；裂缝可以是细小暗色几何或预制的受损外观，无需实时更改网格拓扑。
3. 生命归零后停用完整模型的阻挡与命中目标，切换至几块断木。碎片按攻击方向弹开，限制飞散距离，再收拢至落地状态或淡出。
4. 对本项目优先使用脚本轨迹表现 3D 飞散、旋转、落地，或把短暂 3D 碎片模拟放在独立碰撞层。碎木不要参加玩家/怪物的主体阻挡，避免破坏后又把主角推走。
5. 道具破坏状态和已有门/宝箱状态分别存储。普通木箱的可破坏性不自动意味着有交互的奖励宝箱也应被摧毁；有掉落的道具需明确防止重复发奖。

这些是建议的集成要求，不是本调研的运行验证结果。

## 制作与性能注意点

- **木箱/桌子先按构件拆分。** 初版可约 6–10 个主要碎片配少量纯视觉小木屑，数量只是调参起点，不是测量结论。木板断口可做斜切/锯齿，避免均匀方块。给同一类道具准备几种固定种子变体，运行时选择即可。
- **闭合几何和 UV。** 切面需要封口，外部 atlas UV 继续保留，断面使用同色系浅木色材质或 atlas 内的小色块。运行时任意切割最容易暴露内部面、法线和多子网格问题；OpenFracture 官方文档专门展示了桌凳横梁与腿部相交导致切面重建错误的例子。[3]
- **预碎节省切割工作，不代表没有成本。** 网格和碰撞可制作期缓存，但启用每片 GameObject/Rigidbody、碰撞、独立渲染仍有成本。避免成百碎片无限存留；使用数量上限、短时模拟、休眠/回收，实际预算用本项目镜头和目标设备测定。
- **避免每帧重新生成 MeshCollider。** Unity 6.3 官方说明 MeshCollider 一般比 primitive collider 成本更高；运行时改变其 Mesh 会要求重新计算碰撞几何，开销显著。移动碎木若需要物理，少量 BoxCollider 或 convex/compound 近似更易控制；动态非凸 MeshCollider 也有支持限制。[6]
- **URP 要检查材质，不只检查碎裂算法。** 官方“兼容所有管线”的商品声明可以作为试用依据，但导入后仍须看外表/断口材质、阴影、atlas UV、游戏镜头下的裂缝可读性。当前项目没有安装上述工具，不能保证零修改导入即用。

## 来源与查证范围

[1] RayFire：

- [当前 RayFire 2 商店页](https://assetstore.unity.com/packages/tools/game-toolkits/rayfire-2-342492)：官方商品描述包含 Demolition Types 和 Fragment Types；兼容表、价格、版本、授权。
- [旧 RayFire for Unity 页](https://assetstore.unity.com/packages/tools/game-toolkits/rayfire-for-unity-obsolete-148690)：明确 deprecated、replacement `RayFire 2`。
- [作者 Unity 产品页](https://rayfirestudios.com/products/rayfire-for-unity/)：支持 Unity 6，平台与席位要求。页面仍链接旧商品，所以下载选型以当前商店 replacement 为准。

[2] DinoFracture：

- [作者官网](https://www.dinofracture.com/)：预碎、运行时、自定义碎片模板和 all render pipelines 声明。
- [官方 Overview](https://www.dinofracture.com/doc/latest/index.html)：编辑器/运行时、自定义切面、命中点、多线程及平台说明；当前文档标题版本 `2.11.1`，与商店 `2.11.4` 有差异。
- [官方 Quickstart](https://www.dinofracture.com/doc/latest/_quickstart.html)：输出 FractureMeshes Prefab；默认 convex mesh colliders 和 rigid bodies。
- [官方商店页](https://assetstore.unity.com/packages/tools/physics/dinofracture-a-dynamic-fracture-library-26599)：价格、版本、授权和管线兼容表。

[3] OpenFracture：

- [官方仓库 README](https://github.com/dgreenheck/OpenFracture)：MIT、UV、自定义内部材质、单线程异步、几何/子网格限制、算法。
- [官方 GettingStarted](https://github.com/dgreenheck/OpenFracture/blob/main/Documentation~/GettingStarted.md)：Read/Write 导入要求、相交桌凳示例、运行时复杂度与预碎建议。
- [官方 Prefracture 文档](https://github.com/dgreenheck/OpenFracture/blob/main/Documentation~/Prefracture.md)：磁盘保存、冻结碎片、触发和内部 UV。
- [官方 Prefracture.cs](https://github.com/dgreenheck/OpenFracture/blob/main/Runtime/Scripts/Prefracture.cs)：3D Rigidbody、convex MeshCollider、两材质和父节点需等比例缩放。
- [官方 package.json](https://github.com/dgreenheck/OpenFracture/blob/main/package.json)：当前 `1.0.2`、Unity `2021.1`。

[4] Blender：

- [官方 Cell Fracture 扩展页](https://extensions.blender.org/add-ons/cell-fracture/)：版本、GPL、4.2+、支持有限声明和下载入口。官方旧 manual 页面本次返回 HTTP 403，未用无法读取的详细参数支持结论。
- [Blender 官方 License / Your Artwork](https://www.blender.org/about/license/)：生成作品、.blend 和其他输出数据的使用权。

[5] KayKit：

- [作者 Dungeon Remastered](https://kaylousberg.com/game-assets/dungeon-remastered)：200 个 stylised 3D Dungeon assets and props、CC0。
- 本地包 `SurvivorLikeGame/Assets/Art/KayKitDungeon/License.txt`、`Models/` 文件名清单。本次没有另下载来源未经核实的所谓 CC0 碎木包，也没有声称已找到可直接替换当前桌子/木箱的成套碎片。

[6] [Unity 6.3 Introduction to Mesh colliders](https://docs.unity3d.com/6000.3/Documentation/Manual/mesh-colliders-introduction.html)：primitive/compound 对比、convex/concave 动态限制、运行时修改网格的开销。该链接会重定向到当前 Unity 官方 docs.unity.com 6000.3 页面。

