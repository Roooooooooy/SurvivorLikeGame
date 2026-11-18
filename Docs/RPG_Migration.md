# RPG 化改造方案（Phase 0）

> 版本：0.1 · 2026-11-05
> 适用工程：`SurvivorLikeGame/`（Unity 6000.3.16f1）
> 本文是 Phase 0 的实施依据。Phase 0 只搭骨架，不追求完整 RPG 内容。

---

## 1. 方向

目标形态：

- 地图按**章节 / 区域**组织，区域是手工制作的**箱庭式**场景。
- 玩家**自由探索**，不做自动寻路、不做挂机自动战斗。
- 普通区域是**传统即时动作战斗**：手动攻击、闪避、敌人有前摇/硬直。
- **幸存者玩法作为特殊区域的子玩法**：进入裂隙/特殊地图后才启用现有的刷怪、局内升级、波次和 Boss。
- 章节、区域、剧情可以依次解锁；区域可回访。

非目标（Phase 0 明确不做）：

- 不重写现有幸存者战斗、Boss、波次、武器进化。
- 不做任务系统、对话系统、商店、背包 UI。
- 不做自动寻路 / 自动战斗。
- 不做多章节内容，只做一张测试箱庭。
- 不把 `EnemySimulation` 改造成通用动作战斗 AI。

---

## 2. 现状诊断

| 现状 | 位置 | 问题 |
|---|---|---|
| 单场景同时是标题、战斗、结算 | `SampleScene.unity`、`System/GameManager.cs:16` | 没有模式边界，无法从 RPG 世界进入/退出 |
| Build Settings 为空 | `ProjectSettings/EditorBuildSettings.asset` | `RestartGame` 用 `buildIndex` 重载不可靠 |
| 玩家属性启动时一次性叠加 | `Player/PlayerController.cs:33-51` | 永久属性、角色属性、局内升级混在一起，进出战斗无法还原 |
| 局内状态是静态全局 | `Meta/RunStats.cs:72-106`、`Progress/PlayerProgress.cs` | `sceneLoaded` 自动重置，多场景会丢数据 |
| 战斗系统依赖全局单例 | `Enemy/EnemySimulation.cs:265-279` | 每帧读 `PlayerController.Instance` / `GameManager.isGameOver`，不适合动作区 |
| 存档只有 PlayerPrefs | `Meta/MetaProgress.cs:32-51` | 存不了角色、背包、区域状态 |
| UI 按路径找场景节点 | `UI/SceneUISkin.cs:35` | 多场景下容易改错对象 |

结论：**先拆生命周期和模式边界，再谈 RPG 内容。**

---

## 3. 目标架构

```
App（DontDestroyOnLoad）
  GameFlow · SaveSystem · SceneFlow
      │
      ├── Region 场景（章节 / 箱庭 / 自由探索）
      │     PlayerMotor + PlayerHealth + WorldInteractor + ActionCombat
      │     ActionEnemy + EnemyBrain（少量、有状态机）
      │     RegionRoot：出生点 / 传送门 / 宝箱 / 边界
      │
      └── Survivor 场景（特殊区域）
            PlayerController + WeaponManager（现有幸存者）
            EnemySimulation + Wave + Boss + RunSession
            RunResult → 回原区域
```

三层生命周期：

| 层 | 内容 | 重置时机 |
|---|---|---|
| Persistent | RPG 角色、等级、金币、背包、世界状态 | 新档 / 读档 |
| Region | 当前区域、玩家位置、区域内的宝箱/门/敌人状态 | 区域切换时保存/恢复 |
| Run | 局内等级、局内武器、临时 Buff、Boss 材料 | 每次进入/退出幸存者区 |

**核心约束：动作区和幸存者区共用“角色数据、伤害事件、HD-2D 表现”，但不共用敌人实现。**

---

## 4. 模式与区域契约

```csharp
public enum RegionMode {
    ActionRPG = 0,    // 自由探索 + 即时动作战斗
    SurvivorRift = 1  // 幸存者子玩法
}
```

区域数据：

```csharp
[CreateAssetMenu(menuName = "RPG/Region")]
public class RegionDefinition : ScriptableObject {
    public string id;
    public string displayName;
    public string sceneName;
    public RegionMode mode;
    public Bounds bounds;
    public string defaultSpawnPointId;
    public List<string> tags;
}
```

场景内放一个 `RegionRoot`，提供运行时的出生点和边界：

```csharp
public class RegionRoot : MonoBehaviour {
    public string regionId;
    public RegionMode mode;
    public string defaultSpawnPointId;
    public List<SpawnPoint> spawnPoints;
    public Bounds bounds;
}
```

规则：

- 一个区域 = 一个 Unity 场景 + 一个 `RegionRoot`。
- 区域之间用 `Portal` 连接，不用无缝流式加载。
- 所有可交互对象（宝箱、门、Boss、传送点）必须有稳定字符串 id，写入 `WorldState`。
- 区域资产放在 `Assets/Resources/Regions/`，由 `RegionCatalog` 按 id 查找。

---

## 5. 玩家与战斗

### 5.1 玩家组合

动作区玩家由组件组合，不复制整套 `PlayerController`：

| 组件 | 职责 |
|---|---|
| `PlayerMotor` | 输入、移动、朝向、传送 |
| `PlayerHealth` | 生命、护甲、无敌帧、死亡 |
| `WorldInteractor` | 搜索并交互 `Interactable` |
| `ActionCombatController` | 轻攻击、闪避、攻击判定 |
| `PlayerModeRig` | 按区域模式启用/禁用能力 |

幸存者区继续使用现有 `PlayerController + WeaponManager`，Phase 0 不改。

### 5.2 动作战斗

新增共享战斗契约：

```csharp
public struct DamageInfo {
    public int amount;
    public Vector2 hitPoint;
    public Vector2 sourcePosition;
    public Vector2 knockback;
    public bool crit;
    public GameObject source;
}

public interface IDamageable {
    bool IsDead { get; }
    void TakeDamage(DamageInfo info);
}
```

- `Hitbox`：攻击帧开启的触发器，同一帧对同一目标只结算一次。
- `ActionEnemy`：生命、受击、击退、死亡，实现 `IDamageable`。
- `EnemyBrain`：`Idle → Chase → Windup → Attack → Recover` 状态机。
- 复用现有 `CombatEvents`、`DamageNumbers`、`HitSparks`、`CameraShake`。
- Phase 0 的敌人只做直线追击；绕墙导航在 Phase 1 用“区域路点图 + 局部避障”补上。

**不要复用 `EnemySimulation` 做动作敌人。** 它是为“大量、同质、直线涌向玩家”的幸存者场景设计的，没有逐个体状态机、前摇、转向和墙体导航。

### 5.3 视角与主角

动作区和幸存者区共用同一套 HD-2D 表现，避免两种模式视觉割裂：

- 相机：透视俯角，`FOV 35`、偏移 `(0, 9, -14)`、旋转 `(30, 0, 0)`，由 `CameraFollow.hd2d = true` 驱动。
- 主角：同一套 8 方向像素猫 `Hd2dPixelPlayer`，资源仍为 `Resources/Player/Cat8Dir`。
- 敌人：由 `Hd2dSpriteBridge` 把 2D Sprite 镜像到 XZ billboard。
- 宝箱/传送门：使用 CC0 低模 3D 资源，逻辑触发体仍在 XY，视觉模型放在 `(x, 0, y)`。
- 地面：`HD2D_GroundGame.mat` + `Hd2dGroundFollow` + `GroundTorch`。
- 后处理：`HD2D_GlobalVolume` / `HD2D_GameTuning` + `Hd2dAtmosphere`。

逻辑与碰撞仍在 XY 平面，视觉统一映射到 `(x, 0, y)`。

`Hd2dPixelPlayer` 现在同时支持两种玩家：

```
幸存者区：PlayerController
动作区：  PlayerHealth + ActionCombatController
```

`CameraFollow`、`Hd2dGroundFollow`、`Hd2dSpriteBridge` 也增加了 `PlayerMotor` / `PlayerHealth` 回退，不再只认 `PlayerController`。

动作区的移动手感与幸存者对齐：

- `PlayerMotor` 在 `Update` 里直接写 `Rigidbody2D.linearVelocity`，和 `PlayerController` 同帧生效。
- `Rigidbody2D.interpolation = Interpolate`，与 `Player.prefab` 一致。
- `Hd2dPixelPlayer` 使用同一套朝向过渡参数（0.1 / 0.18 / 0.1）。

动作区的敌人使用 `SpriteSheetFrameView`：

- `enemy_slime_*.png` 是 **320×64 / 10 帧 × 2 行**的图集，直接赋给 `SpriteRenderer` 会把所有帧铺开。
- `SpriteSheetFrameView` 在运行时按 `32×32` 单帧切片。
- **行序：Unity 纹理底部是 row 0（行走），顶部是 row 1（死亡）**；之前按顶部行走切片会一直播死亡动画。
- 敌人播放 row 0 的 10 帧行走动画。
- 幸存者的 `EnemySimulation` 是不同的路径（着色器按实例选帧），不要把这个组件加到幸存者敌人上。

宝箱/传送门使用 CC0 3D 低模：

- **Treasure Chest 3**（CC0）`Assets/Resources/Props/CC0/TreasureChest/treasure_chest.obj`
- **Low Poly Portal**（CC0）`Assets/Resources/Props/CC0/LowPolyPortal/Portal.obj`
- 宝箱/传送门的逻辑对象（`BoxCollider2D` + `Chest`/`Portal`）放在 XY；视觉模型放在 `(x, 0, y)`。
- 传送门额外有螺旋发光面和紫色点光。

### 5.4 平A连招

`ActionCombatController` 内嵌 `ComboStep[]`，默认三段，按住或连续点击会在连招里循环：

| 段 | windup / active / recovery | 判定中心 / 尺寸 | 刀光 |
|---|---|---|---|
| 横斩 | 0.08 / 0.12 / 0.16 | 0.70 / 1.3×0.9 | Arc，暖金 |
| 回斩 | 0.06 / 0.10 / 0.10 | 0.75 / 1.5×1.0 | Wide，青蓝，flipY |
| 突刺 | 0.14 / 0.16 / 0.26 | 1.15 / 2.2×0.65 | Thrust，暖白 |

- 攻击中再次输入会进入输入缓冲，收招后自动接下一段；超过 `comboResetDelay` 没有输入则回到第一段。
- 判定框和刀光共用 `hitboxCenterDistance` / `hitboxSize`：
  `刀光外缘 = hitboxCenterDistance + hitboxSize.x / 2`
  `SlashArcEffect.GetLocalRadius` 返回贴图本地半径，`ActionCombatController` 用它反推 scale。
- 每段把 `attackDirection` 锁定在出手瞬间，判定框和刀光都按这个方向旋转；角色转向后下一段再取新方向。
- 选中 Player 时，Scene 视图会画出当前段的判定矩形和刀光外缘圆，方便核对。
- 备选 CC0 刀光（尚未接入）：OpenGameArt **Weapon Slash - Effect**（Classic/Purple/Blue/Fire 四套序列）、**Pixel art sword slash effect**（64×47 像素图集）。

### 5.5 模式门禁

`RegionRuntime.Current` 由 `GameFlow` 在场景加载前设置：

```
MainMenu     → Menu
Region.mode  → ActionRegion / SurvivorRun
EnterSurvivorRun → SurvivorRun
```

以下旧系统在 `ActionRegion` 下会被关闭，避免把幸存者玩法带进动作区：

- `GameManager`（刷怪、标题、时间缩放）
- `PlayerController`（旧幸存者玩家）
- `EnemySimulation` / `EnemySimRenderer`
- `WaveEventScheduler` / `ObstacleField`
- `BossReward` / `BossHealthBar`
- `EnemyPerfStress`

> 测试注意：Unity 进入 Play 时会加载**所有打开的场景**。测 `Region_Test` 前请关闭 `HD2D_EnemyPerf`；否则它的 `PerfStress` 会刷 1000 只怪。代码里已经加了保护，但仍建议只保留目标场景。
>
> 另外，脚本从外部编辑器改完后，Unity 需要先 Refresh/编译再 Play；否则跑的仍是旧程序集。可以用 MCP 的 `refresh_unity` 确认 `external_changes_dirty=false`。

### 5.6 属性

Phase 0 先由 `RpgCharacter` 提供基础值：

- `baseMaxHealth` → `PlayerHealth.maxHealth`
- `baseMoveSpeed` → `PlayerMotor.moveSpeed`
- `baseAttack` → `ActionCombatController.damage`
- `baseDefense` → 受伤减免

Phase 1 再引入完整 `StatSheet`：

```
角色基础 + 装备 + 天赋 → 动作区/幸存者区各自的战斗属性快照
```

**RPG 角色等级和局内等级必须分开命名**：`RpgCharacter.level` vs `RunSession.level`（后者 Phase 1 接入）。

---

## 6. 存档

Phase 0 使用版本化 JSON，不再把新数据写进 PlayerPrefs。

```csharp
[Serializable] public class SaveData {
    public int schemaVersion;
    public long savedAtUnix;
    public RpgCharacter character;
    public WorldState world;
}
```

```csharp
[Serializable] public class WorldState {
    public string chapterId;
    public string regionId;
    public string spawnPointId;
    public List<RegionStateEntry> regions; // 不能用 Dictionary：JsonUtility 不支持
    public List<string> flags;
}
```

保存时机：

- 区域切换前
- 打开宝箱 / 击败 Boss 后
- 幸存者 Run 结束回到区域前
- 退出游戏时（Phase 1 补）

文件：`Application.persistentDataPath/rpg_save_0.json`，写入时先写 `.tmp`，再替换，并保留 `.bak`。

旧 `survivor.meta.*` 的迁移在 Phase 1 实现；Phase 0 两套存档并存。

---

## 7. 场景流程

```
Boot
 └ MainMenu
    └ Region_Test（ActionRPG）
       ├ 自由探索
       ├ 宝箱 / 传送门 / 动作战斗
       └ Portal（survivorRift）
            └ SampleScene（现有幸存者场景，临时作为 Survivor_Arena）
               └ RunSession / SurvivorRunBridge
                  └ RunResult → 回 Region_Test
```

Phase 0 用现有 `SampleScene` 充当幸存者区，避免重做刷怪和 UI。`GameFlow` 负责：

1. 加载场景
2. 找到 `RegionRoot` / `PlayerModeRig`
3. 应用模式、传送到出生点
4. 幸存者场景加载后绑定 `SurvivorRunBridge`

---

## 8. Phase 0 任务清单

- [x] 方案文档
- [x] `RegionMode` / `RegionDefinition` / `RegionRoot` / `RegionCatalog` / `ChapterDefinition`
- [x] `SaveData` / `WorldState` / `RpgCharacter` / `SaveSystem`（JSON + 原子写入）
- [x] `GameFlow` / `AppBootstrap` / `BootEntry` / `MainMenuController`
- [x] `PlayerMotor` / `PlayerHealth` / `PlayerModeRig` / `WorldInteractor`
- [x] `DamageInfo` / `IDamageable` / `Hitbox`
- [x] `ActionCombatController` / `ActionEnemy` / `EnemyBrain`
- [x] `Interactable` / `Portal` / `Chest`
- [x] `RunConfig` / `RunResult` / `RunSession` / `SurvivorRunBridge`
- [x] `CameraFollow2D` / `DebugHud`
- [x] 动作区复用 HD-2D 视角与 8 方向像素猫
- [x] `CameraFollow` / `Hd2dGroundFollow` / `Hd2dSpriteBridge` 支持动作区玩家
- [x] 动作攻击/闪避的像素主角反馈（无攻击帧时用缩放脉冲）
- [x] 三段平A连招、输入缓冲、每段独立时序/判定/刀光
- [x] 刀光外缘与判定框前缘几何对齐 + Scene Gizmo
- [x] CC0 3D 宝箱/传送门接入
- [x] 编辑器菜单：`工具/角色扮演/构建 Phase 0 骨架`，一键建场景、区域资产、Build Settings
- [ ] 动作敌人绕墙导航（Phase 1）
- [ ] 战斗属性 `StatSheet` 与装备（Phase 1）
- [ ] 幸存者局内等级/武器与 `RunSession` 的正式同步（Phase 1）
- [ ] 对话/任务/背包/商店（Phase 2）

---

## 9. 验收标准

在 Unity 中执行 `工具/角色扮演/构建 Phase 0 骨架`，然后从 `Boot` 场景 Play：

1. 进入主菜单，点击 `Start Chapter 1` 进入 `Region_Test`。
2. 画面应为与 `SampleScene` 一致的 HD-2D 透视俯角；主角为 8 方向像素猫。
3. WASD 自由移动；J / 鼠标左键攻击；空格闪避；E 交互。
4. 打死测试史莱姆，出现命中数字/火花，敌人死亡。
5. 打开宝箱，金币和经验写入存档；再次进入区域时宝箱保持已开。
6. 走进裂隙传送门，进入 `SampleScene` 幸存者玩法；死亡或按 F8 结束后回到 `Region_Test`。
7. 结算的金币/经验进入 `RpgCharacter`，不是只进入旧的 `MetaProgress`。
8. 退出 Play 再进入，角色等级/金币/宝箱状态保留。

---

## 10. 文件清单

新增：

```
Assets/Scripts/App/AppBootstrap.cs
Assets/Scripts/App/GameFlow.cs
Assets/Scripts/App/BootEntry.cs
Assets/Scripts/App/MainMenuController.cs

Assets/Scripts/World/RegionMode.cs
Assets/Scripts/World/RegionDefinition.cs
Assets/Scripts/World/ChapterDefinition.cs
Assets/Scripts/World/RegionRoot.cs
Assets/Scripts/World/RegionCatalog.cs
Assets/Scripts/World/Interactable.cs
Assets/Scripts/World/Portal.cs
Assets/Scripts/World/Chest.cs
Assets/Scripts/World/CameraFollow2D.cs

Assets/Scripts/Player/PlayerMotor.cs
Assets/Scripts/Player/PlayerHealth.cs
Assets/Scripts/Player/PlayerModeRig.cs
Assets/Scripts/Player/WorldInteractor.cs

Assets/Scripts/Combat/DamageInfo.cs
Assets/Scripts/Combat/IDamageable.cs
Assets/Scripts/Combat/Hitbox.cs

Assets/Scripts/Action/ActionCombatController.cs
Assets/Scripts/Action/ActionEnemy.cs
Assets/Scripts/Action/EnemyBrain.cs
Assets/Scripts/Action/SpriteSheetFrameView.cs
Assets/Scripts/Action/SlashArcEffect.cs

Assets/Resources/Props/CC0/TreasureChest/treasure_chest.obj
Assets/Resources/Props/CC0/TreasureChest/Treasurechest_DIFF.png
Assets/Resources/Props/CC0/LowPolyPortal/Portal.obj
Assets/Resources/Props/CC0/LowPolyPortal/portal_texture.png
Assets/Resources/Props/CC0/LowPolyPortal/spiral_portal.png
Assets/Settings/HD2D_Chest.mat
Assets/Settings/HD2D_PortalFrame.mat
Assets/Settings/HD2D_PortalSpiral.mat

Assets/Scripts/RPG/RpgCharacter.cs
Assets/Scripts/Save/SaveData.cs
Assets/Scripts/Save/WorldState.cs
Assets/Scripts/Save/SaveSystem.cs

Assets/Scripts/Runs/RunConfig.cs
Assets/Scripts/Runs/RunResult.cs
Assets/Scripts/Runs/RunSession.cs
Assets/Scripts/Runs/SurvivorRunBridge.cs

Assets/Scripts/UI/DebugHud.cs

Assets/Editor/Phase0SkeletonBuilder.cs
```

新增资源（由编辑器菜单生成）：

```
Assets/Resources/Regions/Region_Test.asset
Assets/Resources/Chapters/Chapter_01.asset
Assets/Scenes/Boot.unity
Assets/Scenes/MainMenu.unity
Assets/Scenes/Region_Test.unity
```

修改的现有文件：

```
Assets/Scripts/System/CameraFollow.cs          // 增加 PlayerHealth / PlayerMotor 目标回退
Assets/Scripts/Rendering/Hd2dGroundFollow.cs   // 同上
Assets/Scripts/Rendering/Hd2dSpriteBridge.cs   // 动作区玩家也识别为 Player
Assets/Scripts/Rendering/Hd2dPixelPlayer.cs    // 同时支持 PlayerController 与 PlayerHealth
Assets/Scripts/Action/ActionCombatController.cs // 暴露 AttackStarted / DodgeStarted 事件
Assets/Editor/Phase0SkeletonBuilder.cs          // Region_Test 改为 HD-2D 视角与像素猫
```

---

## 11. Phase 0 的临时桥接（必须在 Phase 1 清掉）

1. `SampleScene` 临时作为幸存者区。它仍带标题页和旧的 `GameManager`；`SurvivorRunBridge` 会在加载后自动调用 `StartGame()`。
2. `RunSession` 的金币在结束时从旧 `RunStats.Current.CoinsEarned` 读取，因此同一笔金币同时进入 `MetaProgress` 和新的 `RpgCharacter`。Phase 1 必须只保留一条经济链。
3. `ActionEnemy` 击杀时复用 `CombatEvents.RaiseKill`，会被旧的 `RunStatsTracker` 计为击杀。Phase 1 需要按模式拆分事件。
4. 动作区已接入 `Hd2dPixelPlayer`（8 方向像素猫）。由于 Cat8Dir 目前只有待机/跑步帧，没有攻击帧，攻击时用轻微缩放脉冲提示；Phase 1 可以补攻击帧，或改成 Spine 主角。

---

## 12. Phase 1 预告

1. `StatSheet` + 装备 + 天赋，动作区和幸存者区共享属性来源。
2. 区域路点图 + `EnemyNavigator`，敌人绕墙、进门、脱战。
3. 幸存者 Run 的正式 `RunController`，把局内等级、武器、Boss 材料从旧静态状态迁出。
4. 对话、任务、背包、商店、存档点。
5. 第一章 3–5 个箱庭区域 + 章节 Boss。

---

## 13. 风险

| 风险 | 应对 |
|---|---|
| 动作战斗手感是长板 | 先在一个房间里把攻击/闪避/受击调顺，再铺内容 |
| 箱庭内容量被低估 | 先做 1 个区域验证成本，再定章节规模 |
| 幸存者旧逻辑泄漏到动作区 | 用 `RegionMode` 显式开关；`WeaponBase`、`EnemySimulation`、`Hd2dSpinePlayer` 的自动攻击逻辑不在动作区启用 |
| 存档 id 不稳定 | 宝箱/门/Boss/区域全部使用手写稳定 id，禁止用对象名或下标 |
| 两套经济/等级并存 | Phase 1 第一件事就是合并到 `RpgCharacter` + `RunResult` 单链路 |
