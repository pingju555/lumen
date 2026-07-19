# KLWP / KWGT 设计全景整理（迁移候选参考 · 本地解包校正版）

> 整理时间：2026-07-17
> 目的：把 KLWP/KWGT 在「其他原子 / 属性配置 / 动作 / 动画 / 触发器 / 流程」六个侧面的设计逐项看全并整理，作为后续「逐个讨论迁移到 Lumen」的对照底稿。
> 状态：**仅整理，未讨论、未取舍、未写代码。**

---

## 0. 数据来源与验证标注

- **[一手] 本地解包**：项目目录内已有完整 APK 解包 `kwgt_aosp_release_382/`、`klwp_aosp_release_382/`（以及 `Downloaded/` 原始 APK、`Coding\Tare CN WorkSpace\Klwe rekus\` 另一处提取）。
  - `assets/widgets/*.kwgt`（27 个）、`assets/komponents/*.komp`（18 个 ×KWGT/KLWP 两套）、`assets/wallpapers/*.klwp.zip`（9 个）。
  - 每个 `.kwgt`/`.klwp.zip` 内都是 `preset.json`；每个 `.komp` 内是 `komponent.json`。
  - 已用 Python 脚本批量解析全部预设，提取真实 `internal_type`、属性字段名、枚举取值、动作/全局变量结构（脚本 `recon.py / extract*.py / scan*.py` 留在项目根，可复用）。
- **[文档] 网络资料**：Kustom 官方 docs、Kode Guide、社区教程。用于补充**官方样例未使用**的机制（动画 schema、完整动作清单、Flows 引擎等）。
- 下文中 **[一手]** = 已由解包预设验证；**[文档]** = 仅见于网络文档、官方样例未出现。

---

## 1. 原子（Atoms / Modules）设计

### 1.1 真实类型清单（internal_type，[一手] 解包统计）

| 类型 | Kustom 形态 | 关键属性（[一手] 真实字段） | Lumen 现状 |
|---|---|---|---|
| TextModule | 文本 | `text_expression`(公式) / `text_size` / `text_align` / `text_lines` / `text_width` / `text_filter` / `text_family` / `paint_color` / `fx_shadow*` / `touch_single` / `touch_single_music`(音乐触摸) / `text_rotate_mode` | ✅ TextAtom（公式文本、字体/颜色/对齐） |
| CurvedTextModule | 弧形文本 | 同 Text + `text_ratio` / `text_spacing`（弧线排版） | ❌ Lumen 无弧形文本 |
| ShapeModule | 形状 | `shape_type` / `shape_angle` / `shape_corners`(圆角) / `shape_width`/`shape_height` / `paint_color`/`paint_mode`/`paint_style`/`paint_stroke` + FX(`fx_gradient*`/`fx_shadow*`/`fx_mask`/`fx_bitmap_blur`) | ✅ ShapeAtom（Rect+radius / Ellipse + 14 纹理） |
| BitmapModule | 图像(=Image) | `bitmap_bitmap`(源,可公式长串) / `bitmap_width` / `bitmap_rotate` / `bitmap_alpha` / `bitmap_mode`(VECTOR) | ✅ ImageAtom 存在 |
| FontIconModule | 图标(=Icon) | `icon_icon` / `icon_set`(字体图标包) / `icon_size` / `fx_shadow*` | ✅ IconAtom 存在 |
| ProgressModule | 进度 | `progress_mode`(SHAPES) / `progress_progress` / `progress_max` / `color_mode` / `color_bgcolor`/`color_fgcolor`/`color_color` / `style_shape`/`style_style`/`style_size`/`style_width`/`style_height` | ✅ ProgressAtom 存在 |
| SeriesModule | 序列/图表 | `series_series`(CUSTOM/MINS/DAY_OF_WEEK/DAY_OF_MONTH_SHORT) / `series_count` / `series_formula` / `series_current` / `style_style`(CIRCLE) / `style_mode`(FIXED_SIZE) / `style_gmode`(CURRENT) / `style_grow`/`style_spacing`/`style_rotate`/`style_align` | ❌ Lumen 无图表类原子（缺口） |
| KomponentModule | 嵌套组件 | 引用外部 komponent；自带 `globals_list`(组件级参数) + `config_scale_*` + `internal_*`(作者/标题/锁定) | ⚠️ Lumen 有 Preset/Profile，但**无元素内嵌可复用组件** |
| RootLayerModule | 根层/背景 | `background_type`(IMAGE/SOLID) / `background_color` / `background_bitmap` + `globals_list` | ✅ 每页独立背景(BackgroundRef) |
| StackLayerModule | 堆叠层 | `config_stacking`(HORIZONTAL_*/VERTICAL_*) / `config_margin` / `config_scale_*` | ✅ ContainerAtom(叠放语义可强化) |
| OverlapLayerModule | 重叠层 | `config_fx`(混合特效如 LONG_SHADOW_BR) / `config_fx_fcolor` / `config_tiling`(REPEAT) / `touch_single` | ✅ ContainerAtom |

### 1.2 两个重要校正（[一手] 推翻此前网络说法）

- **形状种类实际只有 5 种**：`shape_type` 真实枚举 = `RECT / SQUARE / TRIANGLE / CIRCLE / SLICE`（SLICE=扇形/饼）。**没有独立的 Line / Arc / Hexagon / Polygon / Diagonal**。线=RECT 某边极小、弧/饼=SLICE。→ 此前的"7 种形状"说法应修正。**Lumen 现有 Rect+radius / Ellipse 已覆盖主体；真实缺口仅 Triangle + SLICE(扇形/饼)**。
- **按钮不是独立原子**：Kustom 全类型清单中**无 ButtonModule**。按钮 = 任意元素 + `touch_single` / `internal_events` 点击动作。→ 此前的"Button 是独立原子"说法应修正。

### 1.3 小结

Lumen 的 6 类原子已覆盖 Kustom 高频基础件；**主要缺口 = Series 图表、Komponent 嵌套复用、弧形文本(CurvedText)、Triangle/SLICE 形状**。图像/图标/进度/文本/形状/容器均已有对应。

---

## 2. 属性配置侧（Property Configuration）

### 2.1 核心范式：每个属性的三态绑定（[一手] 真实机制）

解包证实每个可配置属性有三种来源，由 `internal_toggles`(标志位) + `internal_globals`(属性→全局名映射) + `internal_formulas`(属性→公式字符串) 三件套控制：

- **固定值**：直接写死；
- **全局变量**：`internal_globals` 把属性绑到某个 global（用户可在 UI 调）；
- **公式**：`internal_formulas` 写 `$...$` 实时求值。

[一手] 真实可公式/全局绑定的属性（解包实证）：`paint_color` / `shape_corners` / `shape_width`/`shape_height` / `text_size`/`text_width` / `progress_level`/`progress_max` / `background_color` / `fx_gradient_color` / `fx_shadow_direction` / `config_rotate_offset` / `config_visible` / `icon_*` / `position_padding_*`。**即"任意属性皆可公式"被样例坐实**。

### 2.2 真实属性分组与字段（[一手]）

| 分组(Tab) | 真实字段（解包实证） |
|---|---|
| Paint 绘制 | `paint_color` / `paint_mode`(CLEAR/DARKEN/OVERLAY) / `paint_style`(STROKE) / `paint_stroke`；**渐变** `fx_gradient`(HORIZONTAL/VERTICAL/RADIAL/SWEEP/BITMAP) + `fx_gradient_color`(多色) + `fx_gradient_offset`/`_x`/`_y`/`_width` |
| FX 特效 | **外阴影** `fx_shadow`(OUTER) + `fx_shadow_blur`/`_color`/`_direction`/`_distance`（参数化）；**遮罩** `fx_mask`(BLURRED/CLIP_ALL/CLIP_NEXT)；**模糊** `fx_bitmap_blur` |
| Position 位置 | `position_anchor` / `position_offset_x`/`_y` / `position_padding_*`(四向) |
| Layer 图层 | `config_scale_value`/`_mode` / `config_visible`(可公式) / `config_rotate_offset` / `config_rotate_mode`(MANUAL/CLOCK_HOUR(_SMOOTH)/CLOCK_MINUTE(_SMOOTH)/CLOCK_SECOND/DEG90/180/270) / `config_stacking`(Stack 排列) / `config_fx`+`config_fx_fcolor`(Overlap 混合) / `config_tiling`(REPEAT) |
| Background 背景(Root) | `background_type`(IMAGE/SOLID) / `background_color` / `background_bitmap` |

### 2.3 与 Lumen 对照（[一手] 校正）

| 能力 | Kustom([一手]) | Lumen 现状 |
|---|---|---|
| 渐变填充 | ✅ 5 种方向 + 多色 + 偏移 | ❌ 仅纯色/纹理，缺渐变 |
| 参数化外阴影 | ✅ OUTER + blur/color/direction/distance | ⚠️ Shape 有 _gloss 浅描边，无独立阴影属性 |
| 遮罩/Mask | ✅ BLURRED(透出桌面模糊)/CLIP_NEXT/CLIP_ALL | ❌ 纹理为程序化质感，无真·DWM 模糊/透出桌面 |
| 图层混合 | ⚠️ 样例见 `config_fx`(LONG_SHADOW_BR)+`config_fx_fcolor`；XOR/Difference 仅[文档] | ❌ |
| 旋转模式 | ✅ MANUAL + **时钟指针驱动**(CLOCK_HOUR/MINUTE/SECOND_SMOOTH) + 固定角度 | ❌ Lumen 形状/容器无"跟随时钟旋转" |
| 公式驱动任意属性 | ✅ 颜色/尺寸/旋转/可见性/内边距皆可公式 | ⚠️ 主要文本/内容公式化，几何与外观多为静态 EditField |
| 可见性公式 | ✅ `config_visible` 可公式 | ⚠️ 触发器可控制显隐，但非每元素公式 |
| 内边距/锚点 | ✅ position_padding_*/anchor | ⚠️ 有对齐，缺独立 padding/anchor 属性 |

**小结**：最大差距 = **渐变、外阴影、遮罩/窗口级毛玻璃、公式化任意属性、时钟驱动旋转** 五项。

---

## 3. 动作（Actions / Touch）

### 3.1 真实动作模型（[一手] internal_events 结构）

每个元素可挂事件：`{ "type": 触发方式, "action": 动作类型, ...参数 }`。

- **触发方式 `type`**：样例仅见 `SINGLE_TAP`（[文档] 另有 DOUBLE_TAP / LONG / 触摸区等）。
- **动作类型 `action`**（[一手] 解包实见 4 种）：
  - `KUSTOM_ACTION` + `kustom_action: "WEATHER_UPDATE"`（系统动作子类型）— [Agenda.komp]
  - `OPEN_LINK` + `url: "$wg(gv(source), rss, gv(index), link)$"`（打开链接，URL 可套公式）— [TopNews.kwgt]
  - `SWITCH_GLOBAL` + `switch: "hide"`（切换全局变量）— [Agenda.komp]
  - `MUSIC`（媒体控制）— [MusicMicro.kwgt]
- 文本模块另有快捷字段 `touch_single_music`（触摸即播乐）。

### 3.2 更全动作清单（[文档]，官方样例未出现但引擎支持）

Launch App（可 `$gv(pkg)$` 动态包名）/ Launch Shortcut / Toggle setting(WiFi/蓝牙/闪光灯/DND，部分需 Tasker) / Set global(Switch Global 的 toggle/Go To Next 循环/公式写回) / Compose/Call/SMS / Open file / Open notification / Plugin/Tasker(Intent/广播)。

### 3.3 全局变量写入（[一手] SWITCH_GLOBAL 证实）

`SWITCH_GLOBAL` 动作把某 global 在多个值间切换（toggle 开关 / 列表循环），配合 `gv()` 公式即构成状态机。这是 Kustom 交互联动的核心。

### 3.4 与 Lumen 对照

| 能力 | Kustom | Lumen 现状（P5 行为系统） |
|---|---|---|
| 点击启动应用 | ✅ | ✅ 已有点击动作之一 |
| 切换系统设置 | ✅([文档]) | ❌ 桌面层可后续接 Win 设置/PowerShell 脚本 |
| 媒体控制 | ✅ MUSIC / touch_single_music | ⚠️ mi/mu 公式可读取，动作侧未接播放控制 |
| 全局变量写入/循环 | ✅ SWITCH_GLOBAL([一手]) | ✅ gv 公式可读；**写回/循环动作缺失** |
| Tasker/外部自动化 | ✅([文档]) | ❌ |
| 多步 Flow | ✅([文档]) | ❌ |

**小结**：Lumen P5 已有 9 种点击动作雏形，但 **全局写入/循环(SWITCH_GLOBAL)、系统切换、媒体控制动作、多步 Flow** 是明显缺口（SWITCH_GLOBAL 已被一手数据证实为真实需求）。

---

## 4. 动画（Animations）

### ⚠️ 重要说明（[一手]）

批量解析 **27 widget + 18×2 komp + 9 wallpaper** 后，**所有预设的 `internal_animations` 计数均为 0**——官方样例预设无一使用动画。动画机制确实存在（字段名 `internal_animations` 已知），但具体 schema 仅能从 [文档] 获得，且**未经样例验证**。

### 4.1–4.5 动画机制（[文档]，待后续用一手样例或官方 schema 校验）

- **ReactOn 触发**（约 8 类）：Default / Touch / Scroll(壁纸滚动) / Music playing / Gyro / Global switch / Visibility / Unlock。
- **动画类型**：Fade / Slide(X/Y, scroll/scroll inverted) / Scale / Rotate / Flip(3D XZ/YZ) / **Complex（类 CSS 关键帧，0%→100% 任意属性插值）**。
- **缓动 Pacing**：正常(加速+减速) / 反转 / Bounce(刚性回弹) / Overshoot(弹性回弹) / Linear(匀速)。
- **参数**：Center / Speed / Amount(0–100%) / Rule(before/after center,on/off) / Anchor / Duration / Delay。
- **颜色动画**：色相旋转 / invert / sepia / brighten / saturate 随状态变化。

### 4.6 与 Lumen 对照

| 能力 | Kustom([文档]) | Lumen 现状 |
|---|---|---|
| 进场/循环动画 | ✅ | ✅ P5 已有 enter/loop |
| 触发多样(touch/scroll/gyro/visibility/unlock) | ✅ | ⚠️ 主要点击/常驻 |
| 复杂关键帧 | ✅ Complex | ❌ |
| 缓动变体(bounce/overshoot/linear) | ✅ | ⚠️ 单一缓动 |
| 属性级动画(位置/缩放/旋转/颜色) | ✅ | ⚠️ 较粗粒度 |

**小结**：Lumen 动画骨架在，但 **触发种类、关键帧、缓动变体、属性粒度** 需补强；且这些均缺一手验证，迁移讨论时应先确证 schema。

---

## 5. 触发器（Triggers）

Kustom **没有独立「触发器」面板**，其「触发」由三部分拼成（[一手]+[文档]）：

1. **动画 ReactOn**（§4）— 何时播放动画；
2. **可见性公式** — `config_visible` 可写 `$if(条件, true, false)$` 控制显隐（[一手] 证实 `config_visible` 可公式）；
3. **全局开关 + 触摸动作** — `SWITCH_GLOBAL` 点击切换 global → 公式/动画随之反应，构成状态机（[一手] Agenda.komp 即 `SWITCH_GLOBAL(hide)`）。

公式条件能力：`if()` 多分支、比较符 `= != > >= < <=`、逻辑符 `&`(AND) `|`(OR)、括号优先级。

| 维度 | Kustom | Lumen（P5 触发器） |
|---|---|---|
| 触发模型 | 公式可见性 + 全局开关 + ReactOn | 显式「条件 → 动作 Once/While」评估器 |
| 条件表达 | `if()` + 运算符，嵌在属性公式 | 独立触发器条件 + 公式引擎 |
| 状态机 | 全局开关驱动 | gv 全局变量可承载 |

**对照判断**：Lumen 的「显式触发器（条件→动作）」比 Kustom 的「隐式公式联动」更清晰易用；Kustom 优势是**把触发直接缝进每个属性**。迁移时可保留 Lumen 显式模型，同时吸收「属性级公式触发」的细粒度。

---

## 6. 流程（Flow）

### 6.1 Komponent = 嵌套可复用组件（[一手] 证实）

`KomponentModule` 真实含自己的 **`globals_list`（组件级参数）**——如 `Agenda.komp` 自带 `fonts/days/icons/wfonts/eventc/backc/hide(SWITCH)` 等 NUMBER/COLOR/FONT/SWITCH 参数。导入到任意 widget 后，宿主可通过这些参数定制组件外观，**比整页 Preset 更细粒度的复用**。组件还可 `internal_locked` 锁定、`internal_author` 署名。

`.komp` 文件 = `komponent.json` + `komponent_thumb.jpg` + 字体/图标资源（[一手] 解包确认）。

### 6.2 Flows = 多步自动化引擎（[文档]）

「Flows」Tab 编排多步任务（下载→解析→出图；触摸延时触发动画等），本质是可视化宏/自动化（类快捷指令）。官方样例未含（无一手验证）。

### 6.3 Preset / Profile 整体复用

整体设计导出为 `.klwp` / `.kwgt` 分享；全局变量可跨重启持久化。

| 维度 | Kustom | Lumen 现状 |
|---|---|---|
| 多步自动化 Flow | ✅([文档]) | ❌ 行为系统为单动作 |
| 嵌套可复用组件 | ✅ Komponent(自带参数) [一手] | ⚠️ Preset(场景/外观)+Profile，但无「元素内嵌可复用块」 |
| 整体导出分享 | ✅ .klwp/.kwgt | ✅ Profile 导出/导入(P7b) |
| 全局持久化 | ✅ 跨重启 | ✅ GvStore 持久化 |

**小结**：Lumen 的 Profile/Preset 已覆盖「整体复用/分享」；**缺口 = 多步 Flow 自动化引擎、Komponent 式嵌套复用组件（带组件级参数）**。

---

## 7. 公式函数目录（对照 Lumen 19 函数，[文档]+[一手] 印证）

Kustom 函数以双字母前缀分类（约 33 类）。[一手] 样例公式印证了 `ai()`(天文,如 `ai(sunrise)` 驱动 shape_angle) / `bi()`(电池) / `df()`(日期) / `gv()`(全局) / `wg()`(网络,如 RSS `wg(gv(source),rss,...,link)`) / `rm()`(资源,如 `rm(mfree)`) / `tc()`(文本) 的真实使用。

| 前缀 | 含义 | Lumen 对应 |
|---|---|---|
| AI | 天文 | ✅ `ai` |
| AQ | 空气质量 | ❌ |
| BI | 电池 | ✅ `bi` |
| BP | 位图调色板 | ❌ |
| BR | 跨 App 广播变量 | ❌ |
| CD | 手表并发症 | ❌ |
| CE/CM | 颜色编辑/生成 | ⚠️ 缺 |
| CI | 日历事件 | ❌ |
| DF/DP | 日期格式化/解析 | ⚠️ 弱 |
| FD | 健身数据 | ❌ |
| FL | 循环 for | ❌ |
| GV | 全局变量 | ✅ `gv` |
| IF | 条件 | ✅ |
| LI | 位置 | ❌ |
| MI/MU | 音乐信息/数学 | ✅ `mi`/`mu` |
| MQ | 音乐队列 | ❌ |
| NC/NI | 网络连通/通知 | ❌ |
| RM | 资源监视 | ⚠️ `si` 部分覆盖 |
| SH | Shell | ❌ |
| SI | 系统信息 | ✅ `si` |
| TC | 文本转换 | ⚠️ 弱 |
| TF | 时间跨度 | ❌ |
| TS | 流量 | ❌ |
| TU | 计时器 | ❌ |
| UC | 未读计数 | ❌ |
| WF/WI | 天气 | ❌ |
| WG | 网络获取(HTTP/RSS/XML) | ❌ |
| AN | (Lumen 启动坞) | ✅ `an` |

**小结**：Lumen 的 `si/bi/gv/mi/mu/ai/an` + 19 函数覆盖核心系统/媒体/天文/全局子集；**明显缺口 = 天气(WI/WF)、日历(CI)、文本转换(TC)、颜色编辑(CE/CM)、网络获取(WG)、循环(FL)、日期(DF/DP)、通知(NC/NI)、资源扩展(RM)、位置(LI)**。

---

## 8. 迁移候选总表（基于一手数据校正，供逐个讨论，未取舍）

| # | 候选 | 类型 | 价值 | 可行性 | 一手校正备注 |
|---|---|---|---|---|---|
| A | 渐变填充(5 方向+多色) | 属性 | 高 | 高 | [一手] fx_gradient 5 种方向已证实 |
| B | 参数化外阴影+发光 | 属性 | 中高 | 高 | [一手] fx_shadow=OUTER+blur/color/dir/dist 已证实 |
| C | 图层混合模式 | 属性 | 中 | 中 | [一手] 仅见 config_fx(LONG_SHADOW_BR)；XOR/Difference 仅[文档] |
| D | Series 图表原子 | 原子 | 高 | 中 | [一手] SeriesModule 极丰富，确为缺口 |
| E | Komponent 式嵌套组件(带参数) | 结构 | 高 | 中高 | [一手] KomponentModule 自带 globals_list 证实 |
| F | 全局写入/循环(SWITCH_GLOBAL) | 动作 | 高 | 高 | [一手] Agenda.komp SWITCH_GLOBAL(hide) 证实 |
| G | 系统切换动作 | 动作 | 中 | 中 | [文档] 桌面层需等价映射(PowerShell/脚本) |
| H | 多步 Flow 引擎 | 流程 | 中高 | 中 | [文档] 无一手验证 |
| I | 动画关键帧+缓动变体 | 动画 | 中高 | 中 | [文档] 全样例无动画，需先确证 schema |
| J | 动画触发多样 | 动画 | 中 | 中 | 桌面 gyro 无，scroll→页面切换可映射 |
| K | 天气/日历/网络获取 provider | 公式 | 高 | 中 | WG/WI/WF/CI 确为缺口 |
| L | 文本/颜色函数(TC/CE/CM) | 公式 | 中 | 高 | 纯函数易加 |
| M | 循环 FL | 公式 | 中 | 中 | 引擎需支持 |
| N | 窗口级毛玻璃/Mask 透出桌面 | 属性 | 高 | 低中 | [一手] fx_mask=BLURRED/CLIP_NEXT 正是此方向证据 |
| O | 通知栏原子 | 原子 | 低中 | 中 | 桌面层价值有限 |
| P | 形状补 Triangle+SLICE(扇形/饼) | 原子 | 中 | 高 | [一手] shape_type 仅 5 种，Line 已并入 Rect，真实缺口仅此两项 |
| Q | 弧形文本 CurvedText | 原子 | 中 | 中 | [一手] CurvedTextModule 存在，Lumen 无 |
| R | 时钟驱动旋转(CLOCK_*模式) | 属性 | 中 | 中 | [一手] config_rotate_mode 含 CLOCK_HOUR/MINUTE/SECOND_SMOOTH |

---

## 9. 待讨论问题（留白，不在本次回答）

- 哪些先做、哪些暂缓？（建议从已被一手证实且可行性高者起步：A 渐变 / B 外阴影 / F 全局写入 / P 形状补全）
- 哪些用「Lumen 原生模型」吸收、哪些需改造范式（如 Kustom「属性级公式」是否引入）？
- 桌面覆盖层语境下，Kustom 的「系统切换/陀螺仪/壁纸滚动」如何映射为等价能力？
- 动画机制缺一手验证，迁移前是否先用官方 schema 或自建样例确证？
- 公式 provider 扩展优先级（天气/网络/日历）？

（以上留待后续逐个议题讨论。）
