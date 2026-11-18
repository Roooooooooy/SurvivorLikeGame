# 素材来源与署名

## 敌人 / 武器精灵（静态）与地面纹理
- **Kenney — Tiny Dungeon**（CC0，可自由使用无需署名）
  https://kenney.nl/assets/tiny-dungeon
  （角色精灵 + 地牢地面纹理由其石砖 tile 程序合成）

## 字体
- **VT323** / **Press Start 2P**（Google Fonts, OFL 1.1）
- **Cinzel**（Google Fonts, OFL 1.1）— 黑魂风衬线字体（Trajan Pro 平替）

## 敌人帧动画
- **Animated Slime** — by **Calciumtrice**（CC-BY 3.0）
  https://opengameart.org/content/animated-slime
  四色史莱姆行走动画（绿/黄/蓝/红），每个动作 10 帧，32×32/帧

- **Small Monsters (Animation)** — by **hansolo**（CC-BY 3.0）
  https://opengameart.org/content/small-monsters-animation
  火蚁行走动画（自爆怪），3 帧，48×24/帧

## UI 素材
- **Kenney — Pixel UI Pack**（CC0）
  https://kenney.nl/assets/pixel-ui-pack
  9-slice 面板 / 按钮；已按暗金主题染色
- **Kenney — Fantasy UI Borders**（CC0）
  https://kenney.nl/assets/fantasy-ui-borders
  装饰边框；部分 PNG 已导入
  `Assets/Resources/TitleUI/frame_double.png`、`divider_fade.png` 取自该包（标题页菜单底板与分隔线）
- **Kenney — Particle Pack**（CC0）
  https://kenney.nl/assets/particle-pack
  标题页动态背景使用 `glow_soft.png` / `ember_star.png` / `smoke_wide.png` / `smoke_wisp.png`

## 中文 UI 字体
- **Noto Serif SC**（SIL Open Font License 1.1）
  https://fonts.google.com/noto/specimen/Noto+Serif+SC
  由 Google Fonts 的 NotoSerifSC 变量字体生成项目用子集；许可证见 `Assets/Fonts/NotoSerifSC-OFL.txt`

## 主角 Spine（Catalyst 猫）
- **Catalyst demo cat** — 来自 `bendmorris/catalyst-demo`
  https://github.com/bendmorris/catalyst-demo
  原始数据为 Spine 3.6，已转换为 Spine 4.1 JSON；28 个动作，`custom` 基础皮肤 + `default` 帽子/眼镜/翅膀等附件。
- 该仓库没有附带 LICENSE 文件；看起来是 Spine/Catalyst 示例资源。**发布前请确认你的 Spine Editor 授权以及该猫素材的使用许可**。

## Spine 运行时
- **Esoteric Software — spine-runtimes 4.1**
  https://github.com/EsotericSoftware/spine-runtimes
  采用 Spine Runtimes License；许可证保留在 `Assets/Addons/Spine/LICENSE`。

## 动作区 CC0 3D 素材
- **Treasure Pack** — by **mehrasaur / Inborn Ninja**（CC0）
  https://opengameart.org/content/treasure-pack
  可开盖宝箱：`Assets/Resources/Props/CC0/TreasurePack/chest_body.obj`、
  `chest_lid.obj`、`chest_lock.obj`，以及金币 `coin-*.obj`。
  原模型为单个 `.obj`，这里按连通分量拆成箱体/盖子/锁，并把盖子挂到合页上做开盖动画。
  箱体/盖子的木纹贴图 `Assets/Settings/HD2D_ChestWoodGrain.png` 从 **Treasure Chest 3**
  （CC0，https://opengameart.org/content/treasure-chest-3 ）的 diffuse 中裁出；
  模型原本没有 UV，已用盒式投影补 UV。

- **Low Poly Portal** — by **HippoDevelopment**（CC0）
  https://opengameart.org/content/low-poly-portal
  用作传送门圆环：模型本身在 YZ 平面，绕 Y 转 90° 后正对相机，
  材质改为岩石贴图（`HD2D_PortalRingRock.mat`），内部叠加
  `HD2D/PortalVortex` 螺旋 Shader + `HD2D/GroundGlow` 地面辉光。

## 动作区刀光素材
- **Free Slash VFX** — by **MaykerStudio**（Unity Asset Store 标准 EULA，免费）
  https://assetstore.unity.com/packages/vfx/shaders/free-slash-vfx-309498
  当前平A三段连招为空手拳击：关闭主刀弧与弧面扭曲，使用同一预制体的
  `Impact` 命中层生成在拳头落点，并按段落开启 smoke / flying / impact / light。
  统一暖金色，三段共用同一形态；通过
  `Assets/Resources/FX/SlashVfxLibrary.asset` 的强度/速度/粒子层配置递进。
  原始资源位于 `Assets/Free Slash VFX/`。

- **Weapon Slash - Effect** — by **Cethiel**（CC0）
  https://opengameart.org/content/weapon-slash-effect
  旧版 2D 序列帧（Classic row 1 / Alternative 2 row 2 / Alternative 1 row 5），
  现作为 `SlashArcEffect` 在缺少 3D 刀光库时的兜底实现。
  帧已复制到 `Assets/Resources/FX/Slash/`。

## 备选刀光素材（尚未接入）
- **Slash Effect** — by **MetaShinryu**（CC0）
  https://opengameart.org/content/slash-effect
  单帧柔和月牙 + 长拖尾，可染色；预览见 `Docs/SlashOptions/`。

- **Pixel art sword slash effect** — by **tbbk**（CC0）
  https://opengameart.org/content/pixel-art-sword-slash-effect
  64×47 像素刀光，3×3 帧图集，适合与像素猫保持同一风格。

- **25 Special Effects rendered with Blender** — by **rubberduck**（CC0）
  https://opengameart.org/content/25-special-effects-rendered-with-blender
  128×128 冲击/爆发粒子，可叠在刀光上做命中特效。

## KayKit 遗迹庭院样板

- **KayKit Dungeon Pack 1.1 FREE** — by **Kay Lousberg**（CC0）
  https://kaylousberg.itch.io/kaykit-dungeon-pack
  使用包内 `Assets/fbx(unity)` 的 34 个模型与 `dungeon_texture.png`，
  位于 `Assets/Art/KayKitDungeon/`，原始许可证保留为 `License.txt`。
  `Region_KayKit` 的布局、组合 Prefab、碰撞与交互由本项目的搭建工具生成；
  资源本体为作者制作，未使用收费 AI 模型生成服务。

## 木箱与桌子的预切网格

- **OpenFracture geometry algorithms** — by **Daniel Greenheck**（MIT）
  https://github.com/dgreenheck/OpenFracture
  使用提交 `6d0ddff64173193f024638c2de2dbeb8a2525965` 的 11 个切割 / 三角化工具文件，
  仅在编辑器中生成木片，不包含其运行时破坏组件或 3D 物理系统。
  位于 `Assets/Editor/KayKitFractureGeometry/`；保留完整 `LICENSE.md` 和来源哈希 `SOURCE.json`。
  本地只做命名空间隔离并移除可选覆盖率标记。木片的原外表网格与贴图来自上述 KayKit CC0 包。

