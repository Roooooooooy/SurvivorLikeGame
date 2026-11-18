# SurvivorLikeGame 交接（2026-10-29）

新会话第一句：`先读 HANDOFF.md，接手继续`。
工作目录：`/Users/lexinshengwen/SurvivorLikeGame`，Unity 项目在 `./SurvivorLikeGame`。

> 2026-11-03 更新：主游戏 `SampleScene` 已迁移到 HD-2D（XY 逻辑 + XZ billboard 渲染）。
> 详细架构/验证/限制见 `Proto25D/PROGRESS.md` 的“2026-11-03 主场景 SampleScene HD-2D 迁移”。
> 可重复执行 `工具/HD-2D/7. 将 SampleScene 迁移到 HD-2D`；截图 `/tmp/game_hd2d_final1.png`、`/tmp/game_hd2d_title2.png`。
> 2026-11-03 晚：主角换成 Catalyst 猫 Spine（28 动作、帽子/眼镜/翅膀换装），障碍换成低模柱子/火盆/石棺/木箱；
> 见 `PROGRESS.md` 的“Spine 主角 + 低模障碍”。截图 `/tmp/game_spine_cat4.png`、`/tmp/game_spine_gear.png`。
> 猫素材来自 `bendmorris/catalyst-demo`，发布前确认 Spine/素材授权。

## 当前 git / 工作区

- `main` = `c61487c`（PR #11 已合并），与 `origin/main` 一致
- 工作区只剩本地文件：
  - `SurvivorLikeGame/ProjectSettings/SceneTemplateSettings.json`（会话前就 dirty，**保持不提交**）
  - `.codex/`（本地 MCP 配置，未跟踪）
  - `HANDOFF.md`（本文件，未跟踪）
- 发布用 worktree 已删除，临时补丁/任务书已清理

## 进行中（未提交）：UI v2（2026-10-30）

参考方向：**Hades**（升级卡牌、稀有度色条）+ **Vampire Survivors**（顶部通栏经验条、极简 HUD）。

### 升级面板：卡牌化
- `Scripts/UI/LevelUpCards.cs`
  - 停用场景原有的 `VerticalLayoutGroup`（**不能同时挂两个 LayoutGroup，基类互斥**），改为手动摆放三张横卡
  - 面板 `upgrade/bg (2)` 放大到 900×460；卡片 260×300，间距 22
  - 每张卡：顶部稀有度色条（进化=金 / 获得武器=青 / 普通=羊皮纸色）、72px 图标、名称、描述、热键角标
  - 选项少于 3 个时按实际数量居中（`EnsureLayout(root, optionCount)`）
  - 原竖向布局里的分隔线等装饰会被隐藏
- `GameManager.ShowLevelUp` 调用 `LevelUpCards.EnsureLayout` + `Apply`
- **注意**：`levelUpPanel` 指向的是整屏遮罩 `upgrade`，真正带布局组的是子物体 `bg (2)`

### HUD
- `Scripts/UI/HudV2.cs`（由 `SceneUISkin` 在换肤之后调用）
  - 顶部通栏经验条（18px，整屏宽）+ 左端 `Lv.N` 徽章；场景原有的第二条经验条被隐藏，`HUDController` 的 `expBarFill/levelText` 重定向到新元素
  - 血条下移，左侧加心形图标
  - 计时器/击杀数图标用 `TextIconFollower` 贴在文字左缘（按 `preferredWidth` 动态定位，数字变长也不会重叠）
- `Scripts/UI/TextIconFollower.cs`
- 删除已被取代的 `Scripts/UI/UpgradeIconView.cs`

### 验证
- 截图 `/tmp/ui_v2/`：`ui2_hud.png`、`ui2_levelup.png`（3 卡）、`ui2_two.png`（2 卡居中）
- Console 0 error；临时验证器已删除

### 待办
- 商店/选角/难度/结算仍是表格风，可继续统一成同一套卡牌语言
- 死亡界面、标题界面未改

### 列表屏 / 标题 / 死亡（第二批）
- `Scripts/UI/CardRow.cs`：统一的卡行装饰——左侧强调条 + 图标底座（深色圆角块 + 细金边）
- **商店** `MetaShopScreen.CreateRow`：强调条按属性语义取色（伤害/暴击=血色、移速/拾取=青色、其余=金色）、图标底座、价格前加金币图标
- **选角** `CharacterSelectScreen.CreateRow`：图标改用 72px 底座（原有选中竖条保留）
- **难度** `DifficultySelectScreen.CreateRow`：同上
- **结算** `SettlementScreen`：四项统计从单行改成 **2×2 统计卡**（图标 + 小标签 + 大数值）
- **标题** `SceneUISkin.AddTitleRules`：标题上下各一条细金线
- **死亡**：`YOU DIED` 改为血色（`bloodHi`）+ 上下金线 + 重开按钮加金色描边
- **HUD 显隐**：`SceneHudVisibility.extras` 接管 Canvas 下的顶部经验条，标题页/死亡时一并隐藏

### 验证
- 截图 `/tmp/ui_v2/`：`t_title.png`、`t_death.png`、`s_shop.png`、`s_characters.png`、`s_difficulty.png`、`s_settlement.png`、`ui2_levelup.png`、`ui2_hud.png`、`ui2_two.png`
- Console 0 error；临时验证器已删除

### 坑
- Unity 不允许同一物体挂两个 LayoutGroup（基类互斥）→ 停用而不是替换
- 验证脚本必须彻底删除：残留的 `UiScreensCheck` 会在每次 Play 自动开界面，导致截图全错
- 当前编辑器可能停在 `Proto25D`（**用户自己的未跟踪内容**：`Scenes/Proto25D.unity`、`Proto25D/`、`Editor/Proto25DSetup.cs`），不要动也不要提交；跑验证前先确认 `manage_scene get_active` 是 `SampleScene`

### 待办
- 商店的 BUY 按钮、选角/难度的操作按钮还是扁平样式，可再加描边
- 结算数值字号可再大一档

## 进行中（未提交）：主角换成银虎缅因猫（2026-10-29）

- **背景**：用户的原型图 `Assets/Textures/gray.png`（449×366）就是**他家银虎缅因猫的照片**，一直当占位。**用户要求保留这个占位资源，不要删**
- 生图通道（重要）：
  - Codex 的本地 relay `127.0.0.1:18765` **只能文本**（只支持 `/v1/responses`，`image_generation` 工具会被剥离）
  - `OPENAI_API_KEY` 无效（401）；Codex CLI 未登录
  - **可用**：`https://llm-proxy.learnings.ai/v1/images/generations`，`Authorization: Bearer $LEARNINGS_API_KEY`
    - 模型 `gemini-3-pro-image`（Nano Banana Pro）；**Gemini 系必须带 `"response_format": "b64_json"`**，否则返回空 `data`
    - 支持 `image` 传入参考图（单张字符串或多张数组），用它保住猫的花纹
  - 图生视频通道仍然没有 → 官方 `video2dsprite` 流水线不可用
  - forge 的 API 层写死了 OpenAI 端点，无法指到这个代理；只借用了它的离线后处理思路
- 产出：`Assets/Resources/Player/player_cat_sheet.png`（4 帧 × 64×48，PPU 48）
  - 4 帧行走（contact / down 交替），待机复用帧 0
  - 每帧统一裁边、抠品红、按脚底对齐、44px 高、限色 24 色、1px 深色描边、左上轮廓光
  - 枢轴放在脚底线（`groundLine=46`），角色原点即落脚点
- 代码：`Scripts/Player/PlayerAnimation.cs`（默认值已改为猫的 4 帧配置；运行时 `Sprite.Create` 切片）
- 工具：`Tools/Art/BuildCatSheet.py <raw_sheet.png> <out_dir>`（把 2×2 生成图切成游戏帧）
- 原始生成图留档：`/tmp/player_cat/source_sheet.png`；游戏内截图 `/tmp/player_cat/cat_idle.png`、`cat_walk.png`
- 经验：让模型一次出 **2×2** 网格最稳；2×3 会变成 3×2、逐帧单独生成尺度会漂
- 尚未提交/发布

## 已发布内容（2026-10-29，PR #11）

### 背景美术重制
- 资源 `Assets/Textures/Background/`（PPU 32 / Point / 无压缩 / Sprite）：
  - `bg_stone_floor.png` 512² 冷色石板地面（行高与块宽随机、低对比、带倒角与裂纹）
  - `bg_grunge_large.png` 512² 大尺度明暗/暖色余烬污渍，渲染时放大 8 倍（128 世界单位周期）
  - `decal_*` 10 种装饰：碎石、裂纹、余烬裂隙、污渍、骨骸、灰尘、大碎石堆
- 场景接线（`SampleScene.unity`）：
  - `Background` 换用新地面，`BackgroundFollow(tileSize=16)`
  - `BackgroundGrunge`：`BackgroundFollow(tileSize=128)`，sortingOrder -9
  - `BackgroundDecals`：`Scripts/System/BackgroundDecals.cs`，4 单位网格确定性哈希、世界空间无限散布（密度 0.7，z=4.9）
- 编辑器工具：`Assets/Editor/BackgroundSetup.cs`（菜单 `工具/幸存者游戏/应用背景美术`，可重复接线）

### 尘埃
- 保持**世界空间模拟**（相对于地面不动，相机移动时自然掠过）。曾尝试按镜头位移做视差补偿（`dustParallax`），用户反馈方向不对，已撤销，**不要重复加**。

### 地图障碍系统
- 资源 `Assets/Resources/Obstacles/`（7 种）：断裂石柱 ×2、碎石堆、火盆、雕像基座、石棺、木箱
- `Scripts/System/ObstacleField.cs`：`cellSize=7`、`density=0.7`、`activeRadius=20`、`startClearRadius=5`、`playerClearRadius=3.5`；确定性哈希、对象池、破坏后离开再回来会重生
- `Scripts/System/Obstacle.cs`：HP / 受击闪白 / 破坏掉金币（`EnemySimulation.SpawnCoins`）
- `EnemySimJobs.cs`：`MoveJob` 新增 `obstacles` 圆形数组，移动后硬排斥 + 切向偏置绕行
- `EnemySimulation.cs`：`SyncObstacles()`（Version 变化才拷贝）、`SpawnCoins()`
- `DamageDealer.cs`：先结算障碍，投掷物被挡住后不再穿透；持续武器可同时打障碍和敌人
- `EnemyProjectile.cs`：敌方子弹撞障碍直接回收
- 障碍 z=1.5，sortingOrder 0
- 验证：19 个障碍生成、碰撞体可查、弹道阻挡 true、20 只敌人塞进障碍中心 0.8s 后全部推出、破坏后 19→18 并掉金币

### 相机改正交（重要）
- `Main Camera`：`orthographic=true`、`orthographicSize=6`
- 根因：之前是透视相机，地面 z=5、障碍 z=1.5、敌人 z=1、玩家 z=0 深度不同，镜头平移时屏幕位移速度不同 → “障碍/怪物相对地面漂移”
- 验证：镜头移 1 单位，z=0/1/1.5/5 的固定世界点屏幕位移全部 −53.25px
- 视觉尺度：正交后地面砖块约大 1.5 倍，障碍/敌人约大 10–15%；要更远/更近调 `orthographicSize`（6 ≈ 视野高 12 单位）

### UI 图标
- 资源 `Assets/Resources/Icons/`（27 个 32×32 图标）：14 stat + 6 武器 + 1 进化 + 6 界面（金币/骷髅/时钟/三档难度盾）
- `Scripts/UI/UpgradeIcons.cs`：按 `UpgradeStat` / `WeaponTarget` 解析，`Resources.Load` 缓存；武器类数值升级优先用武器图标
- `Scripts/UI/UpgradeIconView.cs`：运行时给升级按钮补左侧 48px 图标，有图标时文字左对齐并让位
- `Scripts/UI/RowIcon.cs`：列表行图标 + 商店/角色/难度/结算映射
- 已接入：升级面板、永久商店、选角、难度、结算
- `UpgradeData` 新增可选 `icon` 字段（留空自动解析）
- **坑**：`Resources.Load<Sprite>` 要求 meta 的 `textureType: 8` + `spriteMode: 1`。新复制进 Unity 的 PNG 自动生成的是默认 Texture（textureType 0），`Load<Sprite>` 会静默返回 null；批量加图后必须检查 meta

### 工具与工程
- 生成器 `Tools/Art/`：`GenerateBackground.py`、`GenerateObstacles.py`、`GenerateIcons.py`（输出到 `Tools/Art/out/`）
- `.gitignore` 新增 `Tools/Art/out/`、`__pycache__/`、`*.pyc`

### 发布记录

- 分支 `codex/map-art-obstacles-icons`，远端 `ddf66ff`，squash 合并为 `c61487c`，PR #11 已合并
- 校验：124 文件（112 新增 / 12 修改，无删除）、tree `ae97d10…` 与本地一致、124/124 blob sha 一致、+9175/−14、提交消息一致
- 网页分批上传会产生 **3 个同 message 的 commit**（59+60+5）；用 Squash and merge 收成 1 个 commit，最终 tree 与本地一致
- 发布通道：Codex + Orca 内置浏览器上传页。本机 `gh` 未登录、keychain 无凭据、SourceTree 令牌 401、SSH 无可用 key，**不要重复尝试这些通道**

## 已发布内容（2026-10-28，PR #10）

- **UI Phase 1**：`UITheme` / `UIFactory` / `SceneUISkin`；Kenney CC0 9-slice + Fantasy Borders；Noto Serif SC 中文子集；标题/HUD/升级/死亡/商店/选角/难度/结算统一换肤；HUD 左上、标题页隐藏 HUD、升级金边、死亡按钮下移。
- **Shader/特效**：`InstancedSprite` 受击闪红 + 精英/Boss 描边 + 死亡溶解；`ScreenFX` 暗角/颗粒/扫描线/色差/暖色；`EmberParticles`；伤害数字/暴击/顿帧/震屏。
- **移动/死亡漂浮修复**：`CameraFollow` 去掉 Lerp 滞后；`CombatFeedback` 普通命中/普通怪击杀不再震屏顿帧；`EnemySimRenderer` 删除错误的死亡帧竖直补偿（原表取自图集下半张，采样行不对）。

### 发布记录

- 分支 `codex/ui-shader-feedback`，网页提交 `c0de933`，squash 合并为 `feacf06`
- 校验：单 commit、63 文件（49 新增 / 14 修改，无删除）、tree `d519058…` 与本地一致、63/63 blob sha 一致、提交消息一致
- 发布通道：Codex + Orca 内置浏览器在 GitHub 上传页提交。本机 `gh` 未登录、keychain 无凭据、SourceTree 令牌 401、SSH 无可用 key，**不要重复尝试这些通道**

### 修复验证证据（2026-10-28）

- 临时验证器（已删除）在 Play 中注入真实输入并逐帧采样：
  - 移动 3509 帧：`maxCamPlayerDelta=0.00000`、`maxCenterDeviationPx=0.000`、`framesCenterOver1Px=0`。
  - 普通史莱姆击杀（exp=1）：`maxShake=0`、`minTimeScale=1.000`、`hitStopFrames=0`。
  - 精英击杀正对照（exp=20）：`maxShake=0.07865`、`minTimeScale=0.060`、`hitStopFrames=29`。
  - 死亡脚底：修复后 10 帧全部 397/399 像素。
- 输出：`/tmp/survivor_verify/result.json`、`result_death.json`、`clean_*.png`、`move_diag_end.png`、`slime_death_*.png`。

### “Play 卡在 playmode_transition” 结论

不是真卡。Game 视图实际在正常 Play（标题页 564 FPS），MCP 的 `editor_state.play_mode.is_changing` 在 Play 期间不复位、状态 `staleness=true`。后续以 Game 视图/截图判断是否进入 Play，不要只看 `is_changing`。

## 新会话首要动作

1. **主角存在感**：静态单帧、面积小、颜色暗，被亮绿史莱姆抢焦点 → 需要走路/受击帧 + 轮廓光。
2. **敌人/Boss 辨识度**：8 种敌人里 5 种共用史莱姆贴图，Boss 复用绿色史莱姆放大 3 倍；仓库里有 4 张未使用贴图（`enemy_skeleton` / `enemy_goblin` / `enemy_redknight` / `enemy_scout`）。
3. **伤害数字在 5000 敌人下重叠过密**：节流 / 聚合 / 距离限制。
4. **补精英/Boss 描边截图**并调强度（代码已发布，缺独立视觉证据）。
5. 之后玩法：掉落道具、Boss 阶段 3、Meta 解锁等（见 `TODO.md` 5.6 / 1 / 2 / 3 / 4）。
6. 若仍看到“行走时飘”：行走行（图集下半张）第 2~6 帧本身有 2~4 图集像素的美术弹跳；如需脚底完全钉死，需另做行走行补偿。

## 发布流程（沿用之前）

1. `git add -A -- . ':(exclude)SurvivorLikeGame/ProjectSettings/SceneTemplateSettings.json' ':(exclude).codex' ':(exclude)HANDOFF.md'`
2. `git diff --cached --binary --full-index > /tmp/survivor-batch.patch`，`git reset`
3. 在基于 `origin/main` 的临时 worktree 里 `git apply --check --binary`
4. `orca worktree create --repo id:f87b817d-41fb-4fbc-b621-53af2fc96820 --name <task> --no-parent --base-branch origin/main --agent codex --prompt "$(cat <brief>)" --json`
   - 让 Codex 应用补丁、本地提交、用 GitHub 网页上传到 `codex/<topic>`、开 PR 到 `main`
   - 要求校验：单 commit、文件数、tree sha、逐文件 blob sha
5. 合并（网页 Squash and merge，标题去掉 `(#N)`，正文用本地 commit message）后：
   - `git fetch origin main`
   - 对比 `origin/main^{tree}` 与本地发布 commit 的 tree
   - 备份 `SceneTemplateSettings.json` → `git reset --hard origin/main` → 恢复该文件
   - 删除发布 worktree；不要 `git clean -fd`（会误删 `HANDOFF.md` / `.codex/`）

## 关键文件

- UI：`Assets/Scripts/UI/UITheme.cs`、`UIFactory.cs`、`SceneUISkin.cs`、`SceneHudVisibility.cs`、`WaveBannerView/BossHealthBar/SettlementScreen/MetaShopScreen/CharacterSelectScreen/DifficultySelectScreen`
- Shader：`Assets/Shaders/InstancedSprite.shader`、`ScreenFX.shader`；`Assets/Scripts/Enemy/EnemySimRenderer.cs`
- 反馈：`Assets/Scripts/System/CombatFeedback.cs`、`CameraShake.cs`、`DamageNumbers.cs`、`CameraFollow.cs`
- 资源：`Assets/Textures/UI/`、`Assets/Fonts/NotoSerifSC-Subset.ttf`、`Assets/Resources/UITheme.asset`
- 文档：`TODO.md`、`CREDITS.md`（含 Kenney CC0 / Noto OFL）

## 环境注意

- Unity **6.3 LTS (6000.3.16f1)**，**Built-in Render Pipeline**，不要用 URP Shader/Volume 资产
- Artifact 在线发布被当前 `ANTHROPIC_AUTH_TOKEN` 认证拒绝；UI 提案本地：`/tmp/survivor-ui-mockup.html`