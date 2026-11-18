# RPG 改造会话交接（2026-11-05）

> 新会话先读本文件，再读 `Docs/RPG_Migration.md`。
> 工作目录：`/Users/lexinshengwen/SurvivorLikeGame`
> Unity 工程：`./SurvivorLikeGame`（Unity 6000.3.16f1）
> 当前分支：`feat/hd2d-art-polish`

---

## 1. 用户目标

把现在的幸存者 like 转成：

- **章节 / 区域式箱庭地图**，玩家自由探索；
- **传统即时动作战斗**为主；
- **幸存者玩法降级为特殊地图子玩法**（进入裂隙后才启用现有 `EnemySimulation`、波次、局内升级、Boss）；
- 动作区复用幸存者的 **HD-2D 视角**和**同一只 8 方向像素猫**；
- 平A是**一套三段连招循环**，每段间隔、刀光、判定都不同；
- 刀光必须适配主角朝向，**刀光外缘与判定框前缘对齐**；
- 宝箱/传送门已换成 CC0 低模 3D 资源。

用户说中文，回复用中文。

---

## 2. 已完成（Phase 0 骨架 + 当前迭代）

### 2.1 场景/模式骨架

- `AppBootstrap` 创建常驻 `GameFlow`。
- `GameFlow` 负责区域加载、幸存者 Run 进出、存档、玩家死亡重生。
- `RegionRuntime` 在场景加载前设置 `Menu / ActionRegion / SurvivorRun`。
- 新增场景：`Boot`、`MainMenu`、`Region_Test`；Build Settings 已写入。
- `RegionDefinition` / `ChapterDefinition` / `RegionRoot` / `RegionCatalog` / `WorldState`。
- JSON 存档 `SaveSystem`（原子写入 + `.bak`）。
- `Portal` / `Chest` / `Interactable` / `WorldInteractor`。

### 2.2 动作战斗

- `PlayerMotor`：移动；和幸存者一样在 `Update` 写 `linearVelocity`，Rigidbody 插值 `Interpolate`。
- `PlayerHealth`：生命、护甲、无敌帧、死亡。
- `ActionCombatController`：三段连招、输入缓冲、闪避、刀光、判定框。
- `Hitbox` / `IDamageable` / `DamageInfo`。
- `ActionEnemy` + `EnemyBrain`：动作区少量敌人，状态机 `Idle → Chase → Windup → Attack → Recover`。
- `Hd2dPixelPlayer` 现在同时支持 `PlayerController`（幸存者）和 `PlayerHealth + ActionCombatController`（动作区）。
- `CameraFollow` / `Hd2dGroundFollow` / `Hd2dSpriteBridge` 增加动作玩家回退。
- 动作区关闭旧幸存者系统：`GameManager`、`PlayerController`、`EnemySimulation`、`WaveEventScheduler`、`ObstacleField`、`BossReward`、`BossHealthBar`、`EnemyPerfStress`。

### 2.3 连招（当前重点）

`ActionCombatController` 内嵌 `ComboStep[]`，默认三段：

| 段 | windup / active / recovery | 判定中心 / 尺寸 | 刀光 |
|---|---|---|---|
| 横斩 | 0.08 / 0.12 / 0.16 | 0.70 / 1.3×0.9 | Arc，暖金 |
| 回斩 | 0.06 / 0.10 / 0.10 | 0.75 / 1.5×1.0 | Wide，青蓝，flipY |
| 突刺 | 0.14 / 0.16 / 0.26 | 1.15 / 2.2×0.65 | Thrust，暖白 |

- 连续点击/按住 `J` 或鼠标左键在连招里循环；攻击中输入会缓冲到下一段；超过 0.85s 回第一段。
- 每段出手时锁定 `attackDirection`；判定框和刀光都按该方向旋转。
- 几何对齐：
  `判定框前缘 = hitboxCenterDistance + hitboxSize.x / 2`
  `刀光外缘 = hitboxCenterDistance + 贴图本地半径 × scale`
  本地半径：Arc 0.48、Wide 0.50、Thrust 0.95。
- 选中 Player 时 Scene 视图会画当前段判定矩形 + 刀光外缘圆。
- 刀光扩张 1.0→1.03，避免视觉明显超出判定。

### 2.4 像素图集修复

`enemy_slime_*.png` 是 320×64 的 **10 帧 × 2 行**图集：

- 顶部 row 1 = 死亡
- 底部 row 0 = 行走

`SpriteSheetFrameView` 现在从底部行切片；动作敌人播 10 帧行走，不再一直播死亡。

### 2.5 3D 宝箱/传送门

- 宝箱：**Treasure Chest 3**（CC0）
  `Assets/Resources/Props/CC0/TreasureChest/treasure_chest.obj`
- 传送门：**Low Poly Portal**（CC0，HippoDevelopment）
  `Assets/Resources/Props/CC0/LowPolyPortal/Portal.obj`
- 逻辑触发体在 XY（`BoxCollider2D` + `Chest` / `Portal`）；3D 视觉在 `(x, 0, y)`。
- 材质：`Assets/Settings/HD2D_Chest.mat`、`HD2D_PortalFrame.mat`、`HD2D_PortalSpiral.mat`。
- 传送门有螺旋发光面 + 紫色点光。
- 来源已写入 `CREDITS.md`。

---

## 3. 关键文件

```text
Docs/RPG_Migration.md                 架构/契约/Phase 0 说明
Docs/RPG_Session_Handoff.md           本文件
Assets/Editor/Phase0SkeletonBuilder.cs 一键建场景/区域/3D 宝箱/传送门
Assets/Scripts/App/GameFlow.cs
Assets/Scripts/App/RegionRuntime.cs
Assets/Scripts/Action/ActionCombatController.cs
Assets/Scripts/Action/SlashArcEffect.cs
Assets/Scripts/Action/SpriteSheetFrameView.cs
Assets/Scripts/Action/ActionEnemy.cs
Assets/Scripts/Action/EnemyBrain.cs
Assets/Scripts/Player/PlayerMotor.cs
Assets/Scripts/Player/PlayerHealth.cs
Assets/Scripts/World/RegionRoot.cs
Assets/Scripts/World/Portal.cs
Assets/Scripts/World/Chest.cs
Assets/Scenes/Region_Test.unity
```

---

## 4. 当前验证状态

- 最后一次 Unity 编译通过，Console 无 error。
- 运行时 Hierarchy 已确认：
  - 2 只 `Enemy_Slime`（带 `SpriteSheetFrameView`）
  - `Chest_Visual` / `Chest`
  - `Portal_Visual` / `Portal_Rift` / `PortalSpiral` / `PortalLight`
  - 没有 `EnemySimulation` / `ObstacleField` / `GameManager`
- 连招输入和刀光视觉**还没有人工确认**（MCP 不能模拟按键）。
- 3D 宝箱/传送门的朝向、大小、材质观感**还需要 Play 里确认**。

---

## 5. 接手后先做

1. **先停止 Play 再刷新**（Unity Play 模式会挂起脚本编译）。
   - MCP：`manage_editor stop` → `refresh_unity` → `read_console`。
   - 确认 `external_changes_dirty=false`。
2. 只打开 `Region_Test`（关闭 `HD2D_EnemyPerf` 等其它场景；Unity Play 会加载所有打开的场景）。
3. Play，连按/按住 `J` 或鼠标左键，检查：
   - 三段连招是否循环；
   - 每段间隔/刀光颜色/形状是否不同；
   - 刀光是否随朝向旋转；
   - 选中 Player 时 Scene Gizmo 的判定矩形与刀光外缘是否一致；
   - 3D 宝箱/传送门是否可见、朝向/大小是否合理。
4. 根据实际画面调整：
   - 宝箱：`BuildChest` 的 `localRotation`（当前 Y=180）、`localScale=0.015`、Y offset。
   - 传送门：`BuildRiftPortal` 的 `localScale=0.35`、`PortalSpiral` 位置/缩放、点光强度。
   - 连招：`ComboStep` 的时序、`hitboxCenterDistance`、`hitboxSize`、`slashStyle/Color/Lifetime`。
5. 不要提交、不要回滚用户已有改动；提交前问用户。

---

## 6. 已知坑

- Unity MCP 在域重载后可能短暂断开；重试 `refresh_unity`，必要时让用户点 `Start Session`。
- Play 模式下改脚本不会编译；先 stop。
- `Packages/manifest.json` 里有 Unity 自己加的 AI 包，不是本次改动，提交前确认。
- 工作区有大量此前未提交的美术/UI 改动（Cat8Dir、Spine、SceneUISkin 等），不要误回滚。
- 旧的 `HANDOFF.md` 是 2026-10-29 的内容，已过时；以本文件 + `Docs/RPG_Migration.md` 为准。

---

## 7. 下一步路线（Phase 1）

1. `StatSheet` + 装备/天赋，动作区和幸存者区共享属性来源。
2. 区域路点图 + `EnemyNavigator`，让动作敌人绕墙、进门、脱战。
3. 正式 `RunController`，把幸存者局内等级/武器/Boss 材料从旧静态状态迁出。
4. 对话、任务、背包、商店、存档点。
5. 第一章 3–5 个箱庭区域 + 章节 Boss。

---

## 8. 备选素材（尚未接入）

- 刀光：OpenGameArt **Weapon Slash - Effect**（CC0，Classic/Purple/Blue/Fire 四套 13 帧）
  https://opengameart.org/content/weapon-slash-effect
- 刀光：OpenGameArt **Pixel art sword slash effect**（CC0，64×47 像素）
  https://opengameart.org/content/pixel-art-sword-slash-effect
- 宝箱：OpenGameArt **Low-poly pixel art chest**（CC0，但只有 .blend）
  https://opengameart.org/content/low-poly-pixel-art-chest
- 宝箱：Quaternius **Fantasy Props MegaKit**（CC0）
  https://quaternius.com/packs/fantasypropsmegakit.html
- 3D 地牢：Kenney **Mini Dungeon**（CC0）
  https://kenney.nl/assets/mini-dungeon
