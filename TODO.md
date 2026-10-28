# 玩法拓展 TODO（第三档：大系统）

> 前置已完成：升级系统数据驱动化（`Resources/Upgrades`）、属性中枢 `PlayerStats`、武器进化（`Resources/Evolutions`）、敌人差异化（普通/迅捷/坦克/自爆）、敌人仿真 Jobs+Burst 化（`EnemySimulation`，同屏 5000+，编辑器内按 F9 压测）、素材动画化与打击反馈（像素风素材、敌人行走/死亡帧动画、击退/闪红/死亡反馈）。
> 以下各项建议按顺序推进，每项做完都可独立发布一版。

## 1. 波次事件表

把现在纯时间轴的线性加难度，改成有节奏的事件表。

- [x] 新增 `WaveEventData`（ScriptableObject）：触发时间、事件类型、参数
- [x] 事件类型：精英怪（大体型高血、必掉高级经验）、包围潮（一圈敌人同时刷新）、限时精英（当前奖励经验，进化材料待接）
- [x] 节奏草案：精英 30/90/150/210/270s、包围潮 60/120/180/240s、Boss 占位 300s
- [x] 事件调度器与现有刷怪表并行（实现为 `WaveEventScheduler`，运行时自动创建，不改 `GameManager`）
- [x] 事件触发时的屏幕提示（`WaveBanner` 运行时 Canvas，FIFO 淡入淡出）
- [ ] Boss 多阶段实现（当前 `BossPlaceholder` 只是单只超大精英）
- [ ] 限时精英改为掉落进化材料（当前奖励经验）

## 2. Boss 战

- [x] `BossController`：大体型、多阶段（实现为 `EnemyBehavior.Boss` + `BossCombat`，融入 DOTS 仿真）
- [x] 阶段行为：冲锋（向玩家突进）、弹幕（环形，复用 `EnemyProjectile`；阶段 2/3 触发）
- [x] Boss 血条 UI（`BossHealthBar`，运行时 Canvas，读取 `BossStatus`）
- [x] 击杀奖励：大量经验（`expValue` 50 + 经验宝石）
- [x] 击杀奖励：专属进化材料（`PlayerProgress.BossMaterials`，局内生效；持久化留给 Meta 成长）
- [x] 与武器进化联动：`EvolutionRecipe.requiredBossMaterials`，材料不足时进化选项不出现，选择后扣材料（演示配方 `evo_boss_core_orbs`）
- [ ] 阶段 3 差异化（当前阶段 2/3 使用同一套冲锋/弹幕参数）
- [ ] Boss 专属外观/动画（当前复用普通敌人帧）
- [ ] 将 Boss 材料接入 Meta 持久化存档（当前每局清零）

## 3. Meta 成长（局外永久成长）

这是"再来一局"的核心动力。

- [x] 局内货币：金币掉落（普通 20% / 精英 75% / Boss 必掉，复用磁吸拾取与对象池）
- [x] 结算界面：本局时长 / 击杀 / 获得金币 / 累计金币（运行时 Canvas，死亡演出后进入）
- [x] `PlayerPrefs` 持久化累计金币（永久属性、已解锁内容待 Meta-B）
- [x] 永久商店 UI：金币购买永久属性（`MetaShopScreen`，标题页 TAB 打开，7 项升级）
- [x] 永久属性写入 `PlayerStats` 初始值（`MetaProgress.ApplyPermanentStats`，新局 `PlayerController.Start` 应用）
- [x] 解锁项：新角色（均衡默认；玻璃大炮 30 金币、坦克 50 金币）
- [x] 解锁项：难度档位（困难 30 金币、噩梦 80 金币）
- [ ] 解锁项：新武器（待武器 id 门槛接入）
- [x] 难度档位：普通/困难/噩梦，敌人 HP/伤害/速度与金币倍率；标题页按 D 选择
- [ ] 更高难度/无尽模式
- [ ] 商店视觉打磨与截图留档
- [ ] Meta 存档迁移逻辑（版本键已写入，待需要时实现）
- [ ] 结算界面的 R 键路径单独验证（当前按钮路径已验证）
- [ ] 非零 `gameTime` 的时长入账验证（自动测试停留在首页）
- [ ] 金币拾取暂用经验音效占位，后续可换专属音效
- [ ] Meta 存档版本号与迁移（永久属性/解锁加入后）

## 4. 角色差异

- [x] `CharacterData`（ScriptableObject）：初始武器、初始属性加成、被动描述
- [x] 选角界面（标题页按 C 打开，运行时 Canvas，与 TAB 商店互斥）
- [x] `PlayerController` 按所选角色初始化（`MetaProgress` 永久属性之后、满血之前应用）
- [x] 至少 3 个角色：均衡（Orb）、玻璃大炮（Projectile + 伤害/暴击）、坦克（高护甲 + 拾取范围）
- [x] 角色解锁接入 `MetaProgress.IsUnlocked`（Meta-B2：锁定角色显示价格，金币解锁后可选）
- [ ] 更多角色与角色专属初始武器（当前仅 Orb/Projectile）
- [ ] 角色立绘/头像与更强的机制型被动（当前以数值加成为主）

## 5. 体验与工程缺口（补充）

### 5.1 前置：武器获取机制

当前 `WeaponManager.Start` 把 `activeWeapons` 固定为玩家子物体，局内无法获得新武器，`UpgradeStat` 也没有"获得武器"的效果类型。这是后续所有新武器内容的前置。

- [x] 新增 `WeaponGrant` 类升级效果：升级时把新武器实例挂到玩家身上并注册进 `activeWeapons`（第一波已完成）
- [ ] 升级池支持"武器槽上限"与已满时的替换/跳过逻辑
- [ ] 新武器在升级界面显示图标与描述

### 5.2 暂停与设置

- [ ] Esc 暂停菜单：继续 / 重开 / 退出到桌面（`timeScale = 0`，与升级界面互斥）
- [ ] 设置界面：主音量、分辨率、全屏、键位
- [ ] 移动端虚拟摇杆（可选，视发行平台；现只有键盘与手柄）

### 5.3 音效

已有 6 个占位音效（`Resources/Audio/SFX`，可直接替换为正式素材）；`AudioMixer` 尚未接入。

- [ ] 引入 `AudioMixer` 与 Master/SFX 总线
- [x] 命中、击杀、拾取经验、升级、受击、武器发射音效（占位素材，第一波已完成）
- [x] 音效播放池（`AudioManager`，10 路 AudioSource，全局命中节流）

### 5.4 反馈

- [ ] 伤害数字：对象池 + 飘字，暴击用不同颜色/字号
- [ ] 顿帧 hit-stop：受击/击杀瞬间短暂放慢或冻结，与 `Time.timeScale` 的暂停逻辑协调
- [ ] 升级选项显示 `description`（`GameManager.ShowLevelUp` 目前只显示 `displayName`）
- [ ] 精英/Boss 出现时的镜头震动提示

### 5.5 性能

敌人已 DOTS 化，但飞弹与掉落物仍是 `Instantiate`/`Destroy`，后期数量上升会有 GC 峰值。

- [x] 飞弹对象池（`SmallObjectPool`：玩家飞弹、敌方子弹、4 类武器特效；75 秒实测复用率 86.4%）
- [x] 经验宝石对象池（掉落道具对象池待掉落道具实现）
- [ ] 闪电链的临时 `LineRenderer` 电弧未池化（无 prefab 键），后续可改为预生成电弧对象池
- [ ] 长时间运行（10 分钟以上）的 Profiler 抽查：GC Alloc、Draw Call、仿真耗时

## 顺手可做的小项（第一档遗留）

- [x] 新武器：穿透箭 / 闪电链 / 新星爆发 / 回旋镖（各为一个 `WeaponBase` 子类 + 若干升级资产，第一波已完成）
- [ ] 为新武器补进化配方（`Resources/Evolutions`，第一波未创建）
- [ ] 掉落道具：回血包、全屏磁铁、清屏炸弹（复用 `ExpGem` 磁吸模式）
- [x] 远程敌人：保持距离发射子弹（`Ranged` 行为 + `EnemyProjectile`，已加入刷怪表：150s 解锁、权重 4）
