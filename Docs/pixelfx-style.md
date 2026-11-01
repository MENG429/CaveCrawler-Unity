# NeonFault — pixelfx 视觉风格与正式调色板规范

**Task ID:** VFX-STYLE-01
**角色：** 美术方向 · 林绘澄
**适用范围：** Unity 2022.3 · URP 2D · 洞窟 Roguelike *NeonFault*（工程 `D:/unity111/NeonFault`）
**上游库：** patch-gg/pixelfx（MIT，244 个程序化像素特效，渲染到精灵表 PNG+JSON，支持 `palette` 改色）
**交付目标：** 定义 NeonFault 正式调色板 + 尺度/风格规范，供主程序统一**重烘焙**（spike 阶段用的是占位霓虹调色板，与本规范无关）。

---

## 0. 本规范边界（约束回顾）

- ✅ 仅做视觉方向 / 调色板 / 尺度与风格审计。**不写任何 C#，不改工程代码。**
- ✅ 调色板以 6 阶「暗→亮」hex 数组给出，可直接填入 `node tools/render.mjs <fx> --sheet --opts '{"palette":[...]}'`。
- ✅ 本文件是美术圣经附录；与现有精灵共处、与 URP 2D 发光/混合相关的落地守则见第 4、6 节。
- ⚠️ 尺度审计中的「原生占比」是基于 pixelfx **默认 320×180 画布**与仓库内置角色（24×38px）的估算区间；正式数值请以一次烘焙探针（见第 6 节）后的实测帧盒为准。

---

## 1. 现有工程约定（只读结论，未改动任何代码）

从 `Assets/CB/Core/CBArt.cs` 与 `CBConfig.cs` 读出以下对特效落地有约束的事实：

| 项 | 结论 | 对 pixelfx 的影响 |
|---|---|---|
| 像素过滤 | `CBArtImporter` 强制 **point filter / 无压缩 / 无 mipmaps**，像素硬边 | pixelfx 导出 PNG 必须沿用同套导入设置，否则发光边缘发虚、与精灵不一致 |
| 着色 | `CBAnimPlayer` 接管时 `m_sr.color = Color.white`——**颜色烧进 PNG**，运行时不再 tint | 改色 = 重烘焙（本任务的核心目的）；不要依赖运行时 `SpriteRenderer.color` 改色 |
| 美术分辨率 | 瓦片 ≈ **16px**，角色 ≈ **16–24px**（见 `roomWidth/Height` 与角色尺寸） | 特效最终屏占应落在 **16–48px** 区间，爆炸/重击等上限 64px |
| 已有 VFX 资产 | 工程已含 `vfx_impact.png`、`portal.png`、`projectiles.png`、`ui_hud.png` | 新烘焙特效需与这些既有精灵的明暗与抖动风格对齐，避免「两套美术」 |

---

## 2. NeonFault 正式调色板（7 条色阶）

**设计原则（洞窟霓虹基调）：**
- 深底 + 发光高光：每阶最暗端是「带色相的近黑」，最亮端是「带色相的近白」，保证在洞窟深背景上即便暗部也读得出色相。
- **shadow 例外**：亮端**封顶在中紫**，绝不给白核——阴影是「吸光」，不是「发光」（详见第 4 节 light buffer 守则）。
- 统一 6 阶，匹配 pixelfx 文档示例的 ramp 长度；渲染器把特效内部明暗索引映射到这条 ramp 上。

### 2.1 色阶表

| 类别 | ramp 名 | 暗 → 亮（6 阶 hex） |
|---|---|---|
| fire / explosion | `neon_fire` | `#1a0800` · `#4d1400` · `#a83200` · `#ff6a00` · `#ffb02e` · `#fff0bf` |
| holy / heal | `neon_holy` | `#012a26` · `#03534a` · `#0a9b86` · `#2fe0c0` · `#8ff7e6` · `#e8fff9` |
| arcane / magic | `neon_arcane` | `#2a0026` · `#5e0058` · `#a3139b` · `#e83fd6` · `#ff8ff0` · `#ffe6fb` |
| ice | `neon_ice` | `#021830` · `#064a7a` · `#0c87c4` · `#46c4f0` · `#aee8ff` · `#eafaff` |
| shadow | `neon_shadow` | `#05010a` · `#16082b` · `#2c1147` · `#451c6b` · `#5e2e8c` · `#7a45ab` |
| blood / crit | `neon_blood` | `#2a0000` · `#6e0505` · `#b31212` · `#ef2b2b` · `#ff6b6b` · `#ffd6d6` |
| coin / ui | `neon_coin` | `#2a1a00` · `#6b4400` · `#b87a00` · `#ffb627` · `#ffe08a` · `#fff6d6` |

### 2.2 可直接粘贴的 JSON 数组

```json
"neon_fire":   ["#1a0800","#4d1400","#a83200","#ff6a00","#ffb02e","#fff0bf"],
"neon_holy":   ["#012a26","#03534a","#0a9b86","#2fe0c0","#8ff7e6","#e8fff9"],
"neon_arcane": ["#2a0026","#5e0058","#a3139b","#e83fd6","#ff8ff0","#ffe6fb"],
"neon_ice":    ["#021830","#064a7a","#0c87c4","#46c4f0","#aee8ff","#eafaff"],
"neon_shadow": ["#05010a","#16082b","#2c1147","#451c6b","#5e2e8c","#7a45ab"],
"neon_blood":  ["#2a0000","#6e0505","#b31212","#ef2b2b","#ff6b6b","#ffd6d6"],
"neon_coin":   ["#2a1a00","#6b4400","#b87a00","#ffb627","#ffe08a","#fff6d6"]
```

> 单特效烘焙示例（桶爆 → fire ramp）：
> `node tools/render.mjs explosions/barrel-explosion --sheet --opts '{"palette":["#1a0800","#4d1400","#a83200","#ff6a00","#ffb02e","#fff0bf"]}'`
> 占位霓虹调色板请**整体替换为上表**后再重烘焙。

---

## 3. 尺度审计

**参照系：** pixelfx 默认 `createFrame(320,180)`；内置角色 24×38px。NeonFault 瓦片 16px、角色 16–24px，故「原生 1px ≈ 游戏美术 1px」时，1 个画布 ≈ 20×7.5 个瓦片——**全画布类特效对 16px 瓦片而言是「屏幕级」体量，不能直接当精灵格用。**

### 3.1 六个 spike 特效 · 推荐烘焙缩放

| spike 特效 | 原生画布占比（估） | 推荐烘焙缩放 | 目标帧格 | 备注 |
|---|---|---|---|---|
| `explosions/barrel-explosion` | 大，团状 ~110–140px 直径 | **downscale ×0.45** | **64×64** | 仍比瓦片大以保冲击感；爆炸/大爆统一上限 64 |
| `holy/heal`（healing ring/halo） | 中，~50–70px | 保持原生 / ×0.9 | 48×48 | 环与光晕需完整，勿过度缩 |
| `ui/coin-collect` | 小，~24–36px | **保持原生** | 32×32 | UI 类本就小，勿放大 |
| `projectiles/arrow` | 细长，~8×28px | **保持原生**（见守则） | 24×32 | ⚠️ 箭头建议**保留既有物理精灵**，不强行霓虹化（第 4 节） |
| `impacts/critical-hit` | 小–中，~40–60px | 保持原生 / ×0.9 | 48×48 | crit 红白核，缩放过度会失焦 |
| `melee/heavy-cleave` | 中，弧光 ~70–110px 宽 | 保持原生 / ×0.85 | 64×64 | 弧光需跨度，给到 64 上限 |

### 3.2 整体偏大、需谨慎的大类

- **weather**（blizzard / aurora / clouds / falling leaves / fireflies）：**全画布或近全画布**，绝不可烘焙成精灵格。→ 走**屏幕空间 overlay**（全屏 quad / UI 层），不进精灵图集管线。
- **transitions**（blinds / burn away / iris / glitch wipe / crt off）：**全屏擦除/过场**，同上为屏幕空间，且与 URP 2D 过场/淡入淡出共用一套，不占用战斗特效预算。
- **moves**（12 个带角色的连招）：内含 24×38 角色，天然是「角色级」体量，单帧很大；除非做演出，否则**不建议整段烘焙**，优先拆出其中的纯特效部分。
- **explosions（big-explosion / firework）**、**sci-fi（emp blast / force field）**、**nature（earth spike / tornado）**：单体外扩大，按 64px 上限处理，必要时 ×0.5 起。

### 3.3 统一基准帧尺寸建议

- **多数战斗特效：32×32 ~ 48×48 帧格**（命中、拾取、状态、投射物）。
- **爆炸 / 重击弧光 / 大型 AOE：上限 64×64**。
- **weather / transitions：不进帧格，走屏幕空间。**
- 帧格一律取 **2 的幂**（32/64），便于图集与 mip 无关的点采样。
- 导出后实测帧盒若超出目标格 >10%，回退调 `--scale`/外部分辨率再烘一次。

---

## 4. 风格和谐守则（落地优先 2–3 条）

### 守则 A — 色温一致性：所有发光体共用「深底→带色相白核」结构
七条 ramp 的暗端都是「近黑+色相」、亮端都是「带色相近白」，因此无论火/冰/圣/奥，在洞窟深背景上都会被读成「自发光体」而非「被照亮的物体」，视觉语言统一。
- **例外且必须守住：shadow 永不给白核**（最亮封顶 `#7a45ab`）。阴影是吸光，给白核会与「发光体」语法冲突，立刻穿帮。

### 守则 B — 抖动（dithering）风格必须对齐，否则噪点割裂
pixelfx 在 color buffer 用有序抖动做半透明过渡；而工程精灵是 `point filter` 的硬边像素（`CBArtImporter` 无压缩无 mip）。
- **风险：** 抖动颗粒若比游戏精灵的过渡更细/更密，特效会像「叠了一层噪点」浮在精灵上。
- **落地：** 优先用 **additive light buffer 做辉光**，减少 color buffer 的大面积半透明抖动填充；如需半透明，确认 pixelfx 抖动阶数与游戏精灵过渡一致（建议一次并排截图比对 `vfx_impact.png` 与一盏 neon 火）。

### 守则 C（⚠️ 头号风险）— light buffer 过曝
pixelfx 的「热」感来自**叠加式 light buffer**（composite 时把光加在最上层）。在洞窟深背景上，这很漂亮；但**多个发光体同屏**（火 aura + 圣疗 + 奥术弹 + 拾取闪光）叠加会直接糊成白团，洗掉角色精灵。
- **落地：**
  1. 给全局「辉光预算」：同屏高光来源 ≤ 3，且彼此错峰。
  2. 非白热类（shadow 必关、holy/arcane 适度）压低亮端亮度；只有 fire/ice/crit 允许接近白热。
  3. 烘焙前确认 `--sheet` 是否把 light buffer 烧进 PNG；若已烧入，则**亮端 hex 亮度 = 实际控制旋钮**，shadow 亮端务必压到中紫。
  4. 若渲染工具暴露 `light` 强度选项，整体下调 0.7–0.85 再烘，宁可欠曝也别过曝。

---

## 5. 映射表（6 spike 特效 + 各大类 → 色阶）

> 未单独标注者，整类套用该 ramp。带 * 者为「异质子类需分色」，见备注。

| 特效 / 大类 | 映射 ramp | 备注 |
|---|---|---|
| **spike: explosions/barrel-explosion** | `neon_fire` | ×0.45 → 64 格 |
| **spike: holy/heal** | `neon_holy` | 青绿疗愈光 |
| **spike: ui/coin-collect** | `neon_coin` | 金色拾取闪 |
| **spike: projectiles/arrow** | （保留既有精灵） | 物理箭，不霓虹化；能量弹另算→`neon_arcane` |
| **spike: impacts/critical-hit** | `neon_blood` | 红白 crit 核 |
| **spike: melee/heavy-cleave** | `neon_coin` | 白热金属弧光；魔武另算→`neon_arcane` |
| fire | `neon_fire` | — |
| explosions | `neon_fire` | 暗爆（dark explosion）例外→`neon_shadow` |
| holy | `neon_holy` | — |
| arcane | `neon_arcane` | — |
| ice | `neon_ice` | — |
| shadow | `neon_shadow` | **关/压 light buffer** |
| lightning | `neon_ice` | 电蓝白，无专属 ramp，就近用 ice；如需更「电」的青白可后续扩 ramp |
| nature* | `neon_holy` | 绿系（healing bloom/poison）套 holy 绿端；土石（boulder/earth spike）偏中性棕，建议后续补一条 `neon_earth` 或保持低饱和 |
| status* | 分色 | burning→`neon_fire`；frozen→`neon_ice`；poisoned→`neon_holy`(绿)；bleeding→`neon_blood`；cursed→`neon_shadow`；confused→`neon_arcane` |
| pickups | `neon_coin` | 金币/宝石/开箱金；heart pickup→`neon_blood` 或 `neon_holy` |
| ui | `neon_coin` | 成就/连击/计数金色白字辉光 |
| projectiles* | 分色 | 能量弹/法盘→`neon_arcane`；炮击→`neon_fire`；子弹曳光→`neon_ice`(青白)；**arrow 保持物理精灵** |
| melee* | 分色 | 默认 `neon_coin`(白热钢)；魔武→`neon_arcane` |
| impacts* | 分色 | crit→`neon_blood`；通用白热命中→`neon_coin`；钝击尘→`neon_coin` 低饱和 |
| spawn-death* | 分色 | coin drop→`neon_coin`；溶解/消散→`neon_shadow` 或 `neon_arcane`；respawn beam→`neon_holy`/`neon_arcane`；poof→`neon_coin` |
| **weather**（blizzard/aurora…） | — | **屏幕空间，不进帧格/不套 ramp**（全画布） |
| **transitions**（iris/glitch…） | — | **屏幕空间过场，不进帧格** |

---

## 6. 交主程序的手顺（重烘焙清单）

1. 把第 2.2 节的 7 条 JSON 数组替换掉 spike 阶段的占位霓虹调色板，逐特效重烘。
2. 缩放按第 3.1 表：爆炸 ×0.45、其余保持/×0.85–0.9；帧格取 32/48/64。
3. 导出 PNG 沿用 `CBArtImporter` 导入设置（point / 无压缩 / 无 mip），与 `vfx_impact.png` 等比。
4. **shadow 类关 light buffer**；fire/ice/crit 允许白热；其余压亮端。
5. 跑一次烘焙探针：记录 6 个 spike 特效实测帧盒尺寸，回填第 3.1「原生占比」列，固化缩放系数。
6. 并排截图比对既有精灵与一盏 neon 火，确认抖动（守则 B）不割裂；确认多发光体同屏不过曝（守则 C）。

---
*— 林绘澄 / 美术方向 · VFX-STYLE-01*
