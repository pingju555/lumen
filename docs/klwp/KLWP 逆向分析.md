# KLWP 逆向分析 · 原子化部件思路

> 取材：`klwp_aosp_release_382.apk`（82MB，19 个 dex）。解析 `assets/komponents/*.komp`（16 个内置组件预设）。
> 方法：未做全量 dex 反编译——`.komp` 内的 `komponent.json` 已完整暴露组件树与公式，足够还原模型。
> 目标：提取 KLWP 的"原子化部件"设计，映射到本项目的多层 / 部件 / 行为系统。

---

## 1. 文件与序列化格式

- `.komp` / `.klwp` = **ZIP**，内含 `komponent.json`（组件树）+ `komponent_thumb.jpg`（缩略图）。
- JSON 根结构：
  ```
  { "preset_info": {...}, "preset_root": { ... } }
  ```
- `preset_root` 关键字段：
  - `internal_type`：根类型（如 `KomponentModule`）
  - `globals_list`：全局变量表
  - `internal_formulas`：根级公式（可选）
  - `viewgroup_items`：**组件树（递归子节点数组）**
- 组件树 = `preset_root.viewgroup_items[]` 递归展开。

**借鉴**：我们的持久化 schema 可直接采用"根 + 树(items) + globals + formulas"四段结构（本地 JSON，已定 §9）。

---

## 2. 组件树与原子类型

每节点用 `internal_type` 标识原子种类。16 个预设汇总出的原子类型分布：

| 原子类型 | 数量 | 含义 |
|----------|------|------|
| `TextModule` | 99 | 文本（时钟/日历/便签） |
| `ShapeModule` | 86 | 形状（矩形/圆/背景块） |
| `OverlapLayerModule` | 61 | 绝对定位层（重叠/自由摆放） |
| `StackLayerModule` | 49 | 流式堆叠层（布局容器） |
| `FontIconModule` | 18 | 图标字体字形（天气/状态图标） |
| `KomponentModule` | 9 | 嵌套组件（组合复用） |

> ⚠️ **修正（2026-07-14）**：上表为**预设层面子集**（仅统计了 14 个 KLWP 内置预设，实际出现 6 种）。**代码级完整原子清单见 `Kustom 原子模型.md`**（KLWP + KWGT 双 APK 交叉验证）：用户可添加原子共 11 种（含 `Progress`/`Bitmap`/`CurvedText`/`Series`/`Movie` 等）；此前猜测的 `Gradient` / `Toggle` / `Form` **并非独立原子**——渐变 = Shape 填充属性，开关/表单 = 行为系统（§1.7）。

**核心结论**：部件 = **最小可放置原子的递归树**，不是"整块 widget"。这正是用户要的"原子化"。

---

## 3. 属性三元组模型（精髓）

每个原子节点的**每个可配置属性**都维护三态绑定：

| 字段 | 含义 |
|------|------|
| 静态值 | 直接写死，如 `paint_color: "#FF005090"` |
| `internal_globals` | 属性 → 全局变量名（绑定） |
| `internal_toggles` | 属性 → 开关（该属性是否走公式/全局） |
| `internal_formulas` | 属性 → 公式表达式（toggle 开时生效） |

**实测样例**：
- `TextModule`：`text_size` 绑定全局 `days`；`text_expression: "$df(d, a+(si(mindex,4))+d)$"`
- `ShapeModule`：`shape_width` 公式 `"$si(rwidth)$"`，绑定全局 `fullw`；`paint_color: "#FF005090"`（静态）
- `FontIconModule`：`icon_icon` 公式按天气 `if(wf(icon,...)=CLEAR, Sun, Moon)` 选字形

**一句话**：一个属性三选一——**静态 / 全局变量 / 公式**。这是原子可变性的本质，也是我们部件模型应直接采纳的。

---

## 4. 公式引擎

- **语法**：`$表达式$` 包裹，可嵌套（`$df(d, a+(si(mindex,4))+d)$`）。
- **函数集**（预设实测出现频次）：

| 函数 | 次数 | 类别 |
|------|------|------|
| `gv()` | 382 | 读全局变量（变量驱动核心） |
| `if()` | 210 | 条件分支 |
| `df()` | 190 | 日期/时间格式 |
| `wf()` | 140 | 天气字段 |
| `si()` | 102 | 系统信息（屏宽/索引…） |
| `tc()` | 50 | 文本处理（大小写/截断） |
| `ci()` | 44 | 颜色/日历 |
| `ai()` | 33 | 活动/昼夜 |
| `wi()` | 28 | WiFi |
| `bi()` | 2 | 电池 |
| `mu()` / `mi()` | 2/3 | 音乐 |
| `li()` | 2 | 位置 |
| `ce()` / `wg()` | 14/8 | 杂项 |

- `if()` 支持条件渲染（如按天气切换图标字形）。
- 数据源**全本地**（系统/天气/电池/音乐）→ 贴合我们"v1 不接外部 API"（§9）。

**结论**：公式引擎是 KLWP 灵魂——属性值可计算、可响应状态，而非写死。

> 完整函数词表（含 Kustom 官方文档交叉验证的 `df/tf/si/bi/gv/if/tc/mi/mu` 子命令、运算符文法、Rust 求值器架构、v1 子集）见独立文档 **`公式引擎设计.md`**。决策：**v1 即包含公式求值引擎**，属性三元组的"公式"分支自 v1 生效。

---

## 5. 全局变量 `globals_list`

- 每个全局变量：`{ index, type, title, description, min, max, value }`。
- `type`：`NUMBER` / `COLOR` / `FONT` / `TEXT`（可扩展 `ACTION`）。
- `value` 可是静态，也可是**公式**（如 `BatteryBar.amount = "$bi(level)$"`）。
- 读取：`gv(name)`。
- 作用：用户可调参数 + 跨原子共享状态 + 公式派生。

**对应**：我们的"变量驱动"——全局变量 = 可暴露给用户配置 + 公式可读写。

---

## 6. 布局容器：Overlap vs Stack

- `OverlapLayerModule`：绝对定位/重叠（自由摆放，类似我们的网格自由单元）。
- `StackLayerModule`：流式堆叠，`config_stacking`（如 `VERTICAL_CENTER`）+ `position_anchor`（如 `TOP`）。

KLWP 用 overlap/stack 而非网格；我们项目已定**网格**(§3)。可保留网格，同时借鉴其"容器原子"思想：网格层本身即一种布局容器原子。

---

## 7. 触摸 / 动作（映射 §1.7 触发器/按钮）

- 节点可带 `touch_single` / `touch_single_music` / `kustom_action`。
- 值是指令枚举：`MUSIC` / `PREVIOUS` / `NEXT` …（命令式动作）。
- 这正是"按钮行为/触发器动作"雏形：点击 → 执行指令。

**映射到我们 §1.7**：动作对象（`type + params`）；KLWP 指令枚举可扩展为我们的动作集（切换层 / 切页 / 运行程序 / 发快捷键 / 触发动画 / 开 URL…）。

---

## 8. 对本项目的映射与设计建议

### 8.1 原子化部件模型（取代"整块部件"）
定义桌面端原子类型：
- `TextAtom` 文本（时钟/日历/便签）
- `ShapeAtom` 形状（背景块/分隔/进度底）
- `IconAtom` 图标（启动坞/系统状态）
- `ProgressAtom` 进度（电量/内存/CPU 条）
- `ImageAtom` 位图（壁纸/封面）
- `ContainerAtom` 容器（网格层/分组/堆叠）——对应 Overlap/Stack
- `KomponentAtom` 嵌套组合（复用）

每个原子 = `{ type, id, gridPos, props(三元组), 行为绑定 }`。

### 8.2 属性三元组 → 配置 schema
```json
{ "static": "#FF005090", "global": "fullw", "formula": "$si(rwidth)$" }
```
三选一，直接采用 KLWP 模型。

### 8.3 公式引擎
- **决策：v1 即包含公式求值引擎**（用户拍板）。属性三元组的"公式"分支自 v1 生效，不再推迟到 v1.x。
- Rust 自研轻量表达式求值器（Lexer→Parser→AST→Registry→Provider），函数集对接原生系统指标（`si`→系统信息、`bi`→电池、`mu`/`mi`→音乐、`df`/`tf`→时钟）。详见 `公式引擎设计.md`。

### 8.4 全局变量
`globals`：typed(NUMBER/COLOR/FONT/TEXT/ACTION)，持久化 + 用户配置面板。

### 8.5 动作/触发
复用 §1.7 动作对象；KLWP 的 `touch` 指令枚举扩展为动作集。

### 8.6 持久化 schema（借鉴 KLWP）
```json
{
  "root": {
    "type": "GridLayer",
    "globals": [ { "name":"fullw", "type":"NUMBER", "value": "...", "min":0, "max":720 } ],
    "formulas": { "shape_width": "$si(rwidth)$" },
    "items": [ { "type":"ShapeAtom", "gridPos":[0,0,1,1], "props": { ... } }, ... ]
  }
}
```
本地 JSON（已定 §9）。

### 8.7 与多层架构衔接
- 原子放在**网格部件层**；容器原子可跨层组合。
- 公式 / 全局变量为**全局作用域**，跨层共享。

---

## 9. 结论

KLWP 的**「原子 + 公式 + 全局变量 + 容器 + 动作」**五件套，精确对应我们要做的原子化桌面部件。建议路线：

1. 部件 = **原子树**（非整块）；v1 先实现 `Text / Shape / Icon / Progress / Container` 五类原子。
2. **属性三元组**模型直接采用（静态/全局/公式）。
3. 公式引擎 **v1 即包含**（用户决策），见 `公式引擎设计.md`。
4. 全局变量 + 动作复用 §1.7，映射 KLWP 的 `gv()` / `touch` 指令。

> 注：完整壁纸预设（`.klwp`，在 `assets/wallpapers/`）含 Progress/Bitmap/Image 等更多原子，必要时可再解包佐证。
