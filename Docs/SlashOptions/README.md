# 平A刀光备选

## 当前实现（2026-11-06 起：空手拳击）

主角一开始没有刀剑，三段连招已切到拳击模式，素材仍来自 **[Free Slash VFX](https://assetstore.unity.com/packages/vfx/shaders/free-slash-vfx-309498)**（MaykerStudio，免费）：

- `SlashVfxLibrary` 的 `punchMode` 关闭主刀弧和弧面扭曲，只保留同一预制体的 `Impact` 命中层
- 命中特效生成在判定框中心（拳头落点），不再以角色为中心铺开
- 第一段只有 Impact 冲击；第二段加烟和飞散火花；第三段再开 Impact 全层 + 点光源，强度/缩放递进
- 统一暖金色；运行时按 `ComboStep.slashColor` 对 HDR `_Color` 染色
- 刀光模式（`punchMode=false`）仍保留水平弧面 + 以角色为中心的逻辑，以后重新拿武器可以直接切回
- 方案 A 的 2D 序列帧仍保留为兜底（缺少 3D 库资产时自动启用）

下面的方案 A–D 是当初的 2D 备选调研记录。

| 方案 | 素材 | 授权 | 类型 | 预览 |
|---|---|---|---|---|
| A（当前） | [Weapon Slash - Effect](https://opengameart.org/content/weapon-slash-effect) | CC0 | 6 帧动画，金色 / 蓝色 / 紫色 / 火焰四套 | `04_weapon_slash_sheet_preview.png`，实机 `01`–`03` |
| B | [Slash Effect](https://opengameart.org/content/slash-effect) | CC0 | 单帧柔和月牙，带长拖尾，可染色 | `05_soft_crescent.png`、`06_soft_crescent_blue.png` |
| C | [Pixel art sword slash effect](https://opengameart.org/content/pixel-art-sword-slash-effect) | CC0 | 像素风斩击，和像素猫更统一 | `07_pixel_art_slash_sheet.png` |
| D（辅助） | [25 Special Effects rendered with Blender](https://opengameart.org/content/25-special-effects-rendered-with-blender) | CC0 | 冲击/爆发粒子，可叠在任意刀光上做命中特效 | 需下载压缩包查看 |

## 实机截图（方案 A）

- `01_in_game_gold.png`：横斩（暖金）
- `02_in_game_blue.png`：回斩（青蓝）
- `03_in_game_purple.png`：突刺（紫）

## 备注

- 方案 A 的原始帧是 6 帧转满一圈；当前实现只用其中 4 帧（突刺用 6 帧），避免看起来像“转圈”。
- 方案 B 是单帧图，需要自己补“出现→放大→淡出”的节奏，但形状更干净、拖尾更长。
- 方案 C 是像素图集，和主角猫的像素风格最一致，但分辨率低，放大到 HD-2D 里可能偏糊。
- 所有素材均为 CC0，可商用；接入后需要在 `CREDITS.md` 补来源。
