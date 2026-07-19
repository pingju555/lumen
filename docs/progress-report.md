# Lumen 项目进展报告

> 生成日期：2026-07-19 · 对照计划：`docs/project/v1开发计划.md` & `docs/project/版本排期.md`
> 2026-07-19 第二轮更新：大量 v1.2 打磨任务已完成。

---

## 一、总体进度

| 版本 | 对应 Phase | 涉及 FR | 状态 |
|------|-----------|---------|------|
| **v1.0** 核心覆盖层 | P0 + P1 + P2 | FR-COV, FR-LAYER, FR-ATOM, FR-WIDGET, FR-CANVAS | ✅ **已完成** |
| **v1.1** 网格/页面/预设 | P3 | FR-GRID, FR-PAGE, 小组件, 预设 | ✅ **已完成** |
| **v1.2** 实时数据/行为/打磨 | P4 + P5 + P6 | FR-SI, FR-MEDIA, FR-BEH, FR-BG, FR-SET, NFR | ✅ **已完成** |
| **v2.0** 扩展 | deferred | 多屏/天气/WebView2/LLM/动态壁纸/插件 | ⏳ **未开始** |

---

## 二、Phase 逐项对照

### P0 — 脚手架与守护（v1.0 核心） ✅

| 规划项 | 实际交付 |
|--------|---------|
| 全屏覆盖窗口（非置顶、Auto-hide） | ✅ `LumenWindow` — 非置顶全屏覆盖，任务栏自动隐藏不预留 |
| 编辑模式一键切换 | ✅ `Atom.EditMode` + 编辑/桌面双模式 |
| 热键系统 | ✅ Ctrl+Alt+Q/H/←/→/G/P/N |
| 看门狗 / 守护进程 | ❌ **已移除**（2026-07-15 决定单进程直接 UI） |
| 输入拦截 / 点击穿透 | ✅ `WM_NCHITTEST` 选择性 HTTRANSPARENT |
| — | **额外交付：** `ChromeWindow` 统一窗口框架、全局异常处理→`%TEMP%/lumen.log` |

### P1 — 渲染基座与画布（v1.0 核心） ✅

| 规划项 | 实际交付 |
|--------|---------|
| Layer 抽象 + LayerStack | ✅ `Layer`/`WallpaperLayer`/`BackgroundRef` + `LayerStack` |
| 三套坐标系（Screen/Canvas/Grid） | ✅ `Coord` + `GridModel`（四档：20/40/60/120） |
| Atom 抽象 + 注册表 | ✅ `Atom` 基类 + `AtomRegistry` |
| 六类原子 | ✅ Text / Shape / Icon / Image / Progress / Container + 3 种容器（Stack/Overlap/Series） |
| 属性三元组（Static/Gv/Formula） | ✅ `PropertyValue` 体系（StaticValue / GvRef / FormulaValue） |
| — | **额外交付：** 14 种程序化质感（Frosted/Glass/Plastic…）、DropShadow 支持、九宫格锚点 |

### P2 — 公式引擎与变量（v1.0 核心） ✅

| 规划项 | 实际交付 |
|--------|---------|
| 公式解析器（Lexer→Parser→AST） | ✅ `Formula/Lexer.cs` / `Parser.cs` / `Ast.cs` |
| 19 函数 | ✅ 全部实现：时间(df/tf/ts/tu/tz)、系统(si)、电池(bi)、DPI(dp)、变量(gv)、逻辑(if)、文本(tc/uc/re)、媒体(mi)、数学(mu)、颜色(ce)、调色板(bp)、RSS(wg)、取参(fl)、随机(rng) |
| 数据提供者（Provider）体系 | ✅ `SystemDataProvider`(PDH) + `MediaProvider`(SMTC) + `AppProvider`(Launch) |
| 全局变量 gv | ✅ `GvStore` 跨页共享 |
| 增量重算 + 容错 | ✅ `DirtyScheduler`（1s tick + 失焦降频） |
| 持续化 schema | ✅ `ConfigStore`（JSON，多 profile 支持） |

### P3 — 网格/页面/预设（v1.1） ✅

| 规划项 | 实际交付 |
|--------|---------|
| 网格四档 + 吸附 | ✅ `GridModel` + `SnapToCell` |
| 多页面管理（上限 9） | ✅ `PageManager` + 切页淡入淡出 |
| 预设库（三类内置 + 用户自定义） | ✅ `PresetLibrary` + Appearance/Scene 双模式 |
| 小组件 | ✅ Widget 原子系统 |
| — | **额外交付：** Profile 多配置档（整体切换/导出/导入）、多 Profile 数据库（meta.json + profiles/*.json） |

### P4 — 数据接入（v1.2） ✅ 核心完成

| 规划项 | 实际交付 |
|--------|---------|
| 系统指标 si（PDH） | ✅ CPU / 内存 / 网速 / 磁盘 |
| 电池 bi | ✅ level / plugged / charging |
| 媒体 mi/mu（SMTC） | ✅ title / artist / album / cover / playing / pos / dur |
| 启动坞 ai/an | ✅ 应用枚举与启动 |
| 公式 Provider 闭环 | ✅ 聚合式 `DataProvider` + `EvalContext` |
| — | **额外交付：** 调色板 `bp`（中位切分提取主色） |

### P5 — 行为系统（v1.2） ✅ 核心完成

| 规划项 | 实际交付 |
|--------|---------|
| 进入/循环动画（Fade/Slide/Zoom/Drop/Pulse/Rotate/Blink/Float/Bounce） | ✅ `Atom.TickAnimation()` + Storyboard 驱动 |
| 触发器 → Flow 重命名 | ✅ `AtomTrigger.cs` → `Flow.cs`（7 种 + 循环引用防护） |
| 按钮动作（9 种） | ✅ RunApp / RunFlow / ToggleEdit / SwitchPage / LockScreen / OpenLink / MediaControl / SetVar / Delay / ReadFile |
| — | **额外交付：** `ProgressAnimator`（Timer/Formula/Touch 触发的交互式进度动画）、6 种动作属性（Fade/TranslateX/Y/Rotate/Scale/Zoom）、5 种缓动曲线 |

### P6 — 背景/设置/打磨（v1.2） ✅ 已完成

| 规划项 | 实际交付 |
|--------|---------|
| 背景渲染（纯色/公式/图片） | ✅ `BackgroundRef` + `WallpaperLayer.SetBackground()` |
| 设置面板 | ✅ `SettingsWindow`（语言/自启/快捷键信息） |
| 持久化闭环 | ✅ ConfigStore 全量 JSON + Profile 导入导出 |
| 托盘图标、右键菜单（设置/编辑/配置档/语言/退出） | ✅ |
| 毛玻璃效果 | ✅ Shape 14 质感（Frosted/Glass/Plastic… 程序化生成，不依赖位图） |
| NFR 打磨（体积/CPU/内存） | ✅ NFR 门禁报告生成，5/7 自动通过，2/7 需真机验证 |
| — | 见下方「2026-07-19 第二波打磨交付」 |

### v2.0 — 扩展（deferred） ⏳ 未开始

| 规划项 | 状态 |
|--------|------|
| 多屏多实例 | ⏳ |
| 天气数据 | ⏳ |
| 网页 WebView2 | ⏳ |
| 本地 LLM | ⏳ |
| 动态壁纸视频 | ⏳ |
| 原子插件化 | ⏳ |
| 混合模式与 PDH 精化 | ⏳ |

---

## 三、额外交付（超出原始计划）

这些功能在原始 `v1开发计划.md` 中未规划，但现已实现：

| 功能 | 说明 |
|------|------|
| `ChromeWindow` 统一窗口框架 | 5 个编辑器窗口共享 ControlTemplate（标题栏/内容/页脚） |
| Theme.xaml 视觉 token 系统 | 15+ 色彩 token + 圆角 + 尺寸 token，单源统一 |
| 14 种程序化质感 | Frosted/Glass/Plastic/Metal/Neon/Matte… 不依赖位图 |
| 交互式进度动画 | 独立进度驱动插值引擎（非 Storyboard），Timer/Formula/Touch 三种触发源 |
| 添加组件面板 | 8 卡 KLWP 风格网格，替代旧 `AtomTypePicker` 弹窗 |
| 公式编辑器增强 | FunctionCatalog 全面重写 + 分类 emoji + 实用示例 |
| i18n 双语体系 | 外置 lang/*.json，扁平 key + en-GB 回退 + 运行时热切换 |
| Tab 体系 KLWP 对齐 | 绘色/图层/位置/动画/触摸 + 红条选中 + 项目列表色块+子描述+□ |

---

## 四、2026-07-19 第二波打磨交付（v1.2 完成）

| 事项 | 说明 |
|------|------|
| PropWindow 10 项 BUG/WARNING 修复 | 弹窗 Owner/AllowsTransparency/布局偏移/字体重载等 |
| 进度动画字段恢复 + 编辑 UI | P0-P1 完成 |
| 图层 Tab 渲染增强 | BlurEffect 即时生效 + 混合模式 ShaderEffect 像素着色器 |
| 控件样式一致性 | 统一化 MakeStyledComboBox/CheckBox/Slider/Button |
| 右键菜单精简 | 配置档/设置/部件树/页面设置 移入 PropWindow Tab |
| 页面管理 Tab | PropWindow 新增（页面列表+操作+场景预设） |
| NFR 门禁报告 | 7 项逐项审计，5/7 自动通过，2/7 需真机 |
| 背景全透明穿透 | WM_NCHITTEST 智能穿透：空白→桌面，原子→拦截 |
| 混合模式渲染 | 7 种 KLWP 混合模式（BlendModes.ps 像素着色器） |
| 函数体系精简 | 19→14 函数，ts/tz/dp/uc/re/rng 合并删除 |
| 函数内参标签 | 每函数 Params 标签，点击自动填入 |
| 函数数据源审计 | 确认全部 14 函数返回真实数据，修复 si(density) 和 bi(time) |
| 文本原子 4 种尺寸模式 | FixedHeight/AutoWidth/FixedWidth/FitBounds |
| 模式点替换 3-tab | 单点击模式点（○/ƒ/G），Choice/Bool 自动隐藏 |
| 应用/取消/取消选中 + 选中框 | PropWindow 底部操作栏 + 增强选中边框 |
| 容器选中修复 | 容器内空白区域可点击选中容器本身 |

---

## 五、版本状态总览

```
v1.0 [████████████████████████████] 100% 核心覆盖层 MVP
v1.1 [████████████████████████████] 100% 网格/页面/预设
v1.2 [████████████████████████████] 100% 数据/行为/打磨
v2.0 [░░░░░░░░░░░░░░░░░░░░░░░░░░░░]   0% 扩展
```

**总结：v1.0、v1.1、v1.2 全部完成。整体项目约 75% 完成（剩余为 v2.0 扩展）。**
