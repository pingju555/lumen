# Kustom 原子模型（KLWP + KWGT 逆向）

> 数据来源：用户提供的官方 APK 解包目录
> `klwp_aosp_release_382/`（Kustom Live Wallpaper，动态壁纸）
> `kwgt_aosp_release_382/`（Kustom Widget，桌面小部件）
> 二者共享同一 Kustom 渲染引擎，原子模型一致。KWGT 形态更贴近本项目的"桌面覆盖层部件"。

## 0. 修订说明（重要）

此前在 `KLWP 逆向分析.md` 里的原子类型谱系是**基于预设递归统计的子集**，并不完整：

| 来源 | 统计范围 | 出现的原子种类数 |
|------|---------|----------------|
| KLWP 内置预设（14 个 `.komp`） | 递归 `internal_type` | 6 种 |
| KWGT 内置预设（38 个 `.kwgt`+`.komp`） | 递归 `internal_type` | 9 种 |
| **引擎代码枚举 `EditorModuleType`** | dex `field_id` 表 | **11 种（权威）** |
| **dex 字符串池 `internal_type`** | 引擎识别的原子层 | **14 种（含内部层）** |

**结论：原子在"代码层"才拿全。预设只暴露了被用到的子集。**

---

## 1. 数据来源与方法

三来源交叉验证，互不依赖：

1. **`EditorModuleType` 枚举（最权威）**
   从 KWGT `classes*.dex` 的 `field_id` 表提取枚举常量，得到引擎定义的**全部用户可添加原子**。
   结果（11 个，PascalCase）：
   `BITMAP, CURVED_TEXT, FONT_ICON, KOMPONENT, MOVIE, OVERLAP_LAYER, PROGRESS, SERIES, SHAPE, STACK_LAYER, TEXT`

2. **dex 字符串池 `internal_type`（引擎识别的原子层）**
   在 dex 字节里提取所有 `*Module` / `*LayerModule` 字面串（序列化标识混淆不住）。
   结果（14 种）：
   `BitmapModule, CurvedTextModule, FontIconModule, GlobalsLayerModule, KomponentModule, OverlapLayerModule, PaintModule, ProgressModule, RootLayerModule, SeriesModule, ShapeModule, StackLayerModule, TextModule, TimeModule`
   + `MovieModule`（出现在全部 token 列表，对应 `EditorModuleType.MOVIE`）。

3. **预设递归统计（覆盖面验证）**
   KWGT 38 预设实际出现 9 种（Text/Shape/Overlap/Stack/FontIcon/Komponent/Progress/Series/CurvedText），证明预设是子集。

---

## 2. 完整原子清单

合并 ① 与 ②，Kustom 原子 = **11 种用户可添加原子** + **4 种内部/特殊层**。

### 2.1 用户可添加原子（11 种）

| # | Kustom 原子 | internal_type | EditorModuleType | 类别 | 说明 | 本项目映射 |
|---|------------|--------------|-----------------|------|------|-----------|
| 1 | Text 文本 | `TextModule` | `TEXT` | 基础 | 文本 + 公式 | `Text` 原子 |
| 2 | CurvedText 弧形文本 | `CurvedTextModule` | `CURVED_TEXT` | 基础 | 沿路径/弧线排布文本 | `Text` 的**子模式**（沿路径） |
| 3 | Shape 形状 | `ShapeModule` | `SHAPE` | 基础 | 矩形/圆/线/弧 | `Shape` 原子 |
| 4 | Bitmap 图片 | `BitmapModule` | `BITMAP` | 基础 | 位图/图片 | `Image` 原子 |
| 5 | Progress 进度 | `ProgressModule` | `PROGRESS` | 基础 | 进度条/圆环 | `Progress` 原子 |
| 6 | FontIcon 图标 | `FontIconModule` | `FONT_ICON` | 基础 | 字体图标集 | `Icon` 原子 |
| 7 | Movie 视频 | `MovieModule` | `MOVIE` | 基础 | 视频/动图 | v1 暂不做（需媒体解码） |
| 8 | Komponent 嵌套 | `KomponentModule` | `KOMPONENT` | 容器/引用 | 引用外部预设（原子树复用） | **组合预设机制**（非基础原子） |
| 9 | Overlap 重叠层 | `OverlapLayerModule` | `OVERLAP_LAYER` | 容器 | 绝对定位叠加 | `Container`（重叠布局） |
| 10 | Stack 堆叠层 | `StackLayerModule` | `STACK_LAYER` | 容器 | 流式堆叠 | `Container`（流式布局） |
| 11 | Series 序列层 | `SeriesModule` | `SERIES` | 容器 | 序列/翻页/轮播 | `Container`（序列布局） |

### 2.2 内部 / 特殊层（4 种）

| 层 | internal_type | 角色 |
|----|--------------|------|
| Root 根层 | `RootLayerModule` | 每个预设唯一根，承载 `globals_list` + `viewgroup_items` |
| Globals 全局层 | `GlobalsLayerModule` | 全局变量定义层 |
| Paint 绘制层 | `PaintModule` | 罕见，自由绘制（部分版本） |
| Time 时间层 | `TimeModule` | 罕见，时间相关特化 |

### 2.3 重要修正：以下**不是**独立原子

此前在 `功能需求.md` 假设过 `Gradient / Toggle / FormControl` 为原子。经代码确证，**KWGT/KLWP 这版无 `GradientModule`/`ToggleModule`/`FormControlModule` 作为 `internal_type`**：

- **Gradient（渐变）** = `Shape`（及背景）的**填充属性类型**（纯色/渐变/图片三选一），非原子。
- **Toggle（开关）** = 触摸动作 / 全局变量布尔切换，属于**行为层**（对应本项目 §1.7 触发器/按钮），非原子。
- **FormControl（表单控件：按钮/开关/输入框）** = 未在 internal_type 出现，归为**行为/交互**而非原子。

> 对 `功能需求.md §5/§6` 的影响：v1 原子集不应含 Gradient/Toggle/FormControl；渐变并入 Shape 填充属性，开关/表单归入行为系统。

---

## 3. 容器原子的三种布局

容器（Overlap/Stack/Series）是 Kustom 原子化的关键——对应本项目 `Container` 原子的三种布局模式：

| 布局 | Kustom 原子 | 排布语义 | 典型用途 |
|------|------------|---------|---------|
| 重叠 Overlap | `OverlapLayerModule` | 子原子**绝对定位**，按 z 轴叠加，自由度最高 | 自由拼贴、时钟+背景重叠 |
| 堆叠 Stack | `StackLayerModule` | 子原子**流式排列**（横/纵），自动换行 | 启动坞网格、监控面板行 |
| 序列 Series | `SeriesModule` | 子原子**序列**，翻页/轮播/选项卡 | 多页内容切换、轮播图 |

> 本项目 `Container` 原子 = 单一类型 + `layout` 属性（overlap/stack/series）。与 Kustom 三原子同构。

---

## 4. 属性三元组（原子可变性的本质）

每个原子的**每个属性**三选一绑定（已用 KWGT `Donut.kwgt` 验证字段）：

- **静态值（static）**：写死常量。
- **全局变量（global）**：引用 `globals_list` 中的变量（`gv()` 读取）。
- **公式（formula）**：`$...$` 表达式，由公式引擎求值。

序列化字段（在原子 JSON 节点上）：
- `internal_toggles`：控制每个属性走 静态/全局/公式 哪个分支。
- `internal_globals`：该原子引用的全局变量列表。
- 公式内容以独立属性键存放（如 `text_formula`），与 `internal_toggles` 的开关对应。

> 映射到本项目：原子属性 schema = `{ value, source: static|global|formula, formula?, globalRef? }`，与 `功能需求.md §5.2` 一致。

---

## 5. 全局变量（`globals_list`）

结构（KWGT `Donut.kwgt` 实测）：
```json
{
  "color1": { "index": 6, "type": "COLOR", "title": "color1", "value": "#FF00AB99" }
}
```
- `type`：类型化（COLOR / NUMBER / FONT / TEXT / SWITCH ...）
- `value`：静态值，**自身也可为公式**（Kustom 允许全局变量由公式派生）。
- 读取：`gv(name)`（在预设里出现 382 次，是 KLWP 变量驱动灵魂）。

> 映射到本项目：全局变量 = 项目级/页级命名变量，原子属性可引用；持久化进 JSON 根。

---

## 6. 序列化格式

`.komp`（KLWP）与 `.kwgt`（KWGT）结构**完全一致**，仅 JSON 文件名不同：

- KLWP：`komponent.json`
- KWGT：`preset.json`

统一根结构：
```json
{
  "preset_info": { "version": ..., "author": ..., "name": ... },
  "preset_root": {
    "internal_type": "RootLayerModule",
    "config_scale_value": 1.0,
    "globals_list": { "varName": { "type":..., "value":..., "index":... } },
    "viewgroup_items": [ /* 原子树，递归 */ ]
  }
}
```

原子树节点：
```json
{
  "internal_type": "TextModule",
  "position_padding_...": ...,
  "internal_toggles": { "...": 0/1/2 },
  "internal_globals": [ ... ],
  "viewgroup_items": [ /* 若容器，则子原子 */ ]
}
```

> 映射到本项目：持久化 schema 直接借鉴——`{ meta, globals, root: { type, props, children: [] } }`。

---

## 7. 映射本项目原子化方案（v1 校准）

对齐 Kustom 真实清单，本项目 `功能需求.md §5/§6` 的 v1 原子集应校准为：

| v1 原子 | 对应 Kustom | 说明 |
|---------|------------|------|
| `Text` | Text + CurvedText(子模式) | 文本，含沿路径子模式 |
| `Shape` | Shape（填充含渐变/图片） | 形状，渐变作为填充属性 |
| `Icon` | FontIcon | 字体图标 |
| `Image` | Bitmap | 位图（Kustom 明确为基础原子，v1 纳入） |
| `Progress` | Progress | 进度 |
| `Container` | Overlap + Stack + Series | 单一类型 + `layout` 三态 |

- **Komponent** → 组合预设机制（原子树存为可复用预设，从库拖入），非基础原子。
- **Movie** → v1 暂不做。
- **渐变/开关/表单** → 分别归入 Shape 填充属性 / 行为系统，不单列原子。

---

## 8. 对本项目文档的影响

- `功能需求.md §5.0` FR-ATOM-01/02：v1 原子补 `Image`；Container 明确三布局；CurvedText 作 Text 子模式。
- `功能需求.md §6.0` 表格：增 `Image` 行；Container 行注明重叠/堆叠/序列。
- `KLWP 逆向分析.md §3`：此前"原子类型谱系"为预设子集，需指向本文档完整清单。
- `公式引擎设计.md`：函数集 `df tf si bi gv if tc mi mu`（v1 子集）不变；Kustom 公式语法已验证一致。
