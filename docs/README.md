# 桌面覆盖层 · 文档中心

> 项目文档导航。覆盖层 = 全屏非置顶覆盖原生桌面的原子化部件框架（C# / WPF / .NET 8，仅 Windows）。
> 所有设计文档以 **C#/WPF** 为技术栈（2026-07-14 由 Rust/Tauri/Slint 切换定稿），术语已统一。

## 文档地图

### ① 设计核心（先读这三类）
| 文档 | 作用 | 关键内容 |
|------|------|----------|
| `project/功能需求.md` | **做什么**——全量功能需求（FR 编号） | 覆盖层/多层/背景/布局(Px+网格)/页面/原子框架/部件/设置/工作流；§9 待决参数已全 ✅ |
| `project/项目规格.md` | **是什么**——定位、决策清单、架构草案 | §2 决策表、§3 任务栏 Auto-hide、§4 多层模型、§6 技术栈定稿(C#/WPF)、§7 决策清单全 ✅ |
| `project/技术栈选型.md` | **用什么做**——技术选型 | §1 选型总表、§5 横切技术(NuGet 白名单/deferred 分支)、§6 模块矩阵(全 FR 映射) |

### ② 实现规划
| 文档 | 作用 |
|------|------|
| `project/v1开发计划.md` | Phase 拆解 P0–P6（每阶段交付/验收/技术/FR 映射）；Phase→版本见 `版本排期.md` |
| `project/版本排期.md` | 发布版本路线图：v1.0(核心MVP) / v1.1(网格·小组件·页面) / v1.2(实时数据·行为·打磨) / v2.0(deferred 分支) |

### ③ 引擎专项
| 文档 | 作用 |
|------|------|
| `project/公式引擎设计.md` | 公式引擎架构（C# Lexer→Parser→AST→FunctionRegistry→DataProvider） |
| `project/公式函数参考.md` | 19 函数参考（A 档 11 + B 档 8）与运算符 |
| `project/渲染架构设计.md` | 渲染架构：输入与点击穿透(方案 A/B)、合成、降频 |

### ④ 参考（KLWP 逆向，只读）
| 文档 | 作用 |
|------|------|
| `klwp/KLWP 逆向分析.md` | KLWP/KWGT 动态壁纸与部件模型逆向，原子化思路来源 |
| `klwp/Kustom 原子模型.md` | Kustom 原子模型（11 种原子、属性三元组等），六类原子清单依据 |

### ⑤ 历史
| 文档 | 作用 |
|------|------|
| `project/任务归档.md` | 2026-07-14 过渡快照，**已 superseded**；任务跟踪现由 `版本排期.md` 接管 |

### ⑥ 按 Phase 拆分（开发细化）
> 将 `v1开发计划.md` 的 P0–P6 与 `版本排期.md` 的 v2.0 deferred 分支，细化到 `phases/` 目录：**每个 Phase 一个文件夹，每个模块/功能一个 md**。开发按文件夹逐 Phase 推进。

| 文件夹 | Phase | 模块 md |
|--------|-------|---------|
| `project/phases/P0_脚手架与守护/` | P0 脚手架+守护 | [覆盖窗口](project/phases/P0_脚手架与守护/P0-01_覆盖窗口.md) · [守护进程](project/phases/P0_脚手架与守护/P0-02_守护进程.md) |
| `project/phases/P1_渲染基座与画布/` | P1 渲染基座+画布 | [多层模型](project/phases/P1_渲染基座与画布/P1-01_多层模型.md) · [坐标系](project/phases/P1_渲染基座与画布/P1-02_坐标系.md) · [Atom抽象与注册](project/phases/P1_渲染基座与画布/P1-03_Atom抽象与注册.md) · [Px画布与Text原子](project/phases/P1_渲染基座与画布/P1-04_Px画布与Text原子.md) · [临时持久化](project/phases/P1_渲染基座与画布/P1-05_临时持久化.md) |
| `project/phases/P2_原子全集与公式引擎/` | P2 原子全集+公式引擎 | [六类原子](project/phases/P2_原子全集与公式引擎/P2-01_六类原子.md) · [属性三元组](project/phases/P2_原子全集与公式引擎/P2-02_属性三元组.md) · [公式引擎](project/phases/P2_原子全集与公式引擎/P2-03_公式引擎.md) · [全局变量gv](project/phases/P2_原子全集与公式引擎/P2-04_全局变量gv.md) · [增量重算与容错](project/phases/P2_原子全集与公式引擎/P2-05_增量重算与容错.md) · [持久化](project/phases/P2_原子全集与公式引擎/P2-06_持久化.md) |
| `project/phases/P3_网格小组件页面/` | P3 网格+小组件+页面 ✅ 已实现 | [网格](project/phases/P3_网格小组件页面/P3-01_网格.md) · [小组件](project/phases/P3_网格小组件页面/P3-02_小组件.md) · [页面](project/phases/P3_网格小组件页面/P3-03_页面.md) · [预设库](project/phases/P3_网格小组件页面/P3-04_预设库.md) |
| `project/phases/P4_数据接入/` | P4 数据接入 | [系统指标si](project/phases/P4_数据接入/P4-01_系统指标si.md) · [媒体mi·mu](project/phases/P4_数据接入/P4-02_媒体mi_mu.md) · [启动坞ai·an](project/phases/P4_数据接入/P4-03_启动坞ai_an.md) · [公式Provider闭环](project/phases/P4_数据接入/P4-04_公式Provider闭环.md) |
| `project/phases/P5_行为系统/` | P5 行为系统 | [动画](project/phases/P5_行为系统/P5-01_动画.md) · [触发器](project/phases/P5_行为系统/P5-02_触发器.md) · [按钮行为](project/phases/P5_行为系统/P5-03_按钮行为.md) |
| `project/phases/P6_背景设置与打磨/` | P6 背景+设置+打磨 | [背景渲染](project/phases/P6_背景设置与打磨/P6-01_背景渲染.md) · [设置面板](project/phases/P6_背景设置与打磨/P6-02_设置面板.md) · [持久化闭环](project/phases/P6_背景设置与打磨/P6-03_持久化闭环.md) · [打磨与NFR](project/phases/P6_背景设置与打磨/P6-04_打磨与NFR.md) |
| `project/phases/v2_扩展与多屏/` | v2.0 deferred | [多屏多实例](project/phases/v2_扩展与多屏/v2-01_多屏多实例.md) · [天气](project/phases/v2_扩展与多屏/v2-02_天气.md) · [网页WebView2](project/phases/v2_扩展与多屏/v2-03_网页WebView2.md) · [本地LLM](project/phases/v2_扩展与多屏/v2-04_本地LLM.md) · [动态壁纸视频](project/phases/v2_扩展与多屏/v2-05_动态壁纸视频.md) · [原子插件化](project/phases/v2_扩展与多屏/v2-06_原子插件化.md) · [混合模式与PDH精化](project/phases/v2_扩展与多屏/v2-07_混合模式与PDH精化.md) |

> 模块 md 内部统一含：目标 / 范围（含 / 不含）/ 关键设计 / 接口草图 / 技术选型引用 / FR 映射 / 验收 / 依赖与顺序 / 风险与开放。

### ⑦ 源码（`src/overlay/`，WPF/.NET8 单 exe）
> 工程从零搭建（用户 2026-07-15 决定 v1 代码从零开始，不回退旧 HUD 实现）。按 Phase 推进，P0–P3 已实现。

| 路径 | 内容 | 对应 Phase |
|------|------|-----------|
| `src/overlay/Overlay.csproj` | WPF/.NET8 工程（net8.0-windows + UseWPF + PerMonitorV2 manifest） | — |
| `src/overlay/App.xaml(.cs)` | 应用入口：**单进程直接运行 UI**（守护已移除 2026-07-15）；`--install`/`--uninstall` 自启调试入口 | P0 |
| `src/overlay/OverlayWindow.xaml(.cs)` | 覆盖窗口 + **P3 集成**：PageManager 驱动多页、共享 LayerStack 按页重组、网格重绘、指示器、状态 HUD、扩展热键（切页/换档/套预设/新页）、切页淡入淡出、全局「添加原子」菜单 | P0-01/P1/P2/P3 |
| `src/overlay/Native/NativeMethods.cs` | Win32 P/Invoke（窗口样式 / z 序 / 热键 / RegisterApplicationRestart / `GetSystemPowerStatus`） | P0-01/P2 |
| `src/overlay/Native/NativeWindow.cs` | 原生封装：加/剥扩展样式、插入 Progman 之上（不置顶、不 SetParent） | P0-01 |
| `src/overlay/Core/Autostart.cs` | 注册表自启（HKCU\Run，`--install`/`--uninstall`；设置开关 `SetEnabled`） | P0-02 |
| `src/overlay/Core/Logger.cs` | 诊断日志 `%TEMP%/overlay_p0.log` | P0 |
| `src/overlay/Render/Layer.cs` | `Layer` 抽象 + 三类预设（Wallpaper/Grid/Canvas）；`GridLayer` 可 `Draw` 网格线 | P1-01/P3-01 |
| `src/overlay/Render/LayerStack.cs` | 层栈：ZIndex 重排 + 挂载宿主 + 预设工厂 | P1-01 |
| `src/overlay/Render/Coord.cs` | 三套坐标系换算 + `GridSize`（活动档）+ `Snap`（吸附到 cell） | P1-02/P3-01 |
| `src/overlay/Render/GridModel.cs` | 网格模型：四档 `PRESETS{20,40,60,120}` / `Gcd` / `SnapToCell` / `CellsFor` / `PxToCell` / 占用冲突 `Overlaps`·`Mark` | P3-01 |
| `src/overlay/Render/BackgroundRef.cs` | 背景引用（P6 预留：Kind/Source） | P3-04/P6 |
| `src/overlay/Atoms/Atom.cs` | 原子抽象基类（三元组 + 公式重算 + 拖拽 + 持久化 + `Clone`/`SyncPosition`/`SetVisible` + 静态 `ContextMenuFactory` + 虚 `EditFields`） | P1-03/P2/P3-02 |
| `src/overlay/Atoms/AtomRegistry.cs` | 原子运行时注册表（按 Type 分发工厂，六类注册） | P1-03/P2 |
| `src/overlay/Atoms/TextAtom.cs` · `ShapeAtom.cs` · `IconAtom.cs` · `ImageAtom.cs` · `ProgressAtom.cs` · `ContainerAtom.cs` | 六类原子（均支持属性三元组 + `Update` 增量重算 + `Clone`） | P2-01 |
| `src/overlay/Atoms/PropertyValue.cs` | 属性三元组（Static/GvRef/FormulaValue + 文本内联 `$...$` + 序列化） | P2-02 |
| `src/overlay/Atoms/AtomHost.cs` | 原子宿主（`Compose` 注入 `EvalContext` + `Flatten` 展平供调度） | P1-04/P2 |
| `src/overlay/Atoms/EditField.cs` | 部件可编辑字段元数据（`EditKind` 枚举 + `EditField`）；六类原子各重写 `EditFields()` 描述可编辑属性 | P3 |
| `src/overlay/AtomTree.cs` | 原子树工具：`FindParentList` 跨「页顶层 / 容器嵌套子」定位原子父列表（置顶/置底/复制/删除用） | P3 |
| `src/overlay/Ui/PropertyEditorPanel.xaml(.cs)` | 部件属性编辑器（WPF `Popup` 浮层，锚定原子旁、点外部关闭）：按 `EditFields` 渲染 Text/Color(色块预览)/Number/Choice/File(浏览) 编辑器，确定→`SetProps`+重组+保存 | P3 |
| `src/overlay/Pages/Page.cs` | 页面模型（名称 / `GridSize` / `ShowGrid` / `Background` / `Atoms` / `AllAtoms`） | P3-03 |
| `src/overlay/Pages/PageManager.cs` | 多页面管理器（`ObservableCollection` + `SwitchTo` 循环/夹紧 + 增删改名排序 + 上限 9 + `Select`） | P3-03 |
| `src/overlay/Presets/Preset.cs` | 预设模型（`Layers` / `GridSize` / `Background`） | P3-04 |
| `src/overlay/Presets/PresetLibrary.cs` | 预设库（三类内置 WallpaperOnly/GridWorkspace/CanvasFree + 用户增删改 + JSON 导入导出 + `Apply`） | P3-04 |
| `src/overlay/Formula/Value.cs` | 公式值类型（Num/Str/Color/Bool/Null，宽松互转） | P2-03 |
| `src/overlay/Formula/Lexer.cs` · `Parser.cs` · `Ast.cs` | 词法 / 语法 / 抽象语法树（递归下降） | P2-03 |
| `src/overlay/Formula/FunctionRegistry.cs` | 19 函数注册表（A 档 11 + B 档 8） | P2-03 |
| `src/overlay/Formula/EvalContext.cs` · `FormulaEngine.cs` | 求值上下文 + 引擎门面（容错：异常→Null） | P2-03 |
| `src/overlay/Formula/DataProvider.cs` | `IDataProvider` + `SystemDataProvider`（时间/电池/屏幕/暗色真实；mi/mu/ai/an 留 P4） | P2-03/P4 |
| `src/overlay/Globals/GvStore.cs` | 全局变量 `gv`（类型化 + 变更通知 + 跨层共享） | P2-04 |
| `src/overlay/Engine/DirtyScheduler.cs` | 增量重算调度器（脏标记 + 容错 + 每秒 tick + 失焦降频） | P2-05 |
| `src/overlay/Persistence/ConfigStore.cs` | 全量持久化 schema v1：`{version, userPresets, pages:[{atoms, gridSize, showGrid, background}]}`，旧 screens schema 迁移 + 坏文件兜底 | P2-06/P3 |

> 构建：`dotnet build -c Release`（输出 `src/overlay/bin/Release/net8.0-windows/overlay.exe`）。
> 观察热键（P6 设置面板将替换为正式 UI）：`Ctrl+Alt+Q` 退出 / `→`·`←` 切页 / `G` 换网格档 / `P` 套用下一预设 / `N` 新页。
> **右键菜单**：右键覆盖层空白处弹出全局操作菜单（页面 / **添加原子**（文本·形状·图标·图片·进度条·容器）/ 网格 / 套用预设 / 退出）；**右键任意原子弹出其专属部件菜单**（编辑属性 / 置顶 / 置底 / 复制 / 删除）。属性编辑器支持 `$公式$` 与 `gv:名称` 语法。
> **尺寸调整**：鼠标悬停任意原子显示 8 个白色缩放手柄（角/边），拖动即改变 `Bounds` 并实时同步内容尺寸（图形/进度条走 `SyncSize`，文本/图标/图片/容器走拉伸）；最小尺寸 = 当前网格档。编辑属性弹窗为锚定在原子旁的 `Popup` 浮层（非模态窗口），点外部自动关闭。

> 根目录 `修改.md` 为用户修改输入稿（A/B 段修改来源），属工作输入而非设计文档，不入上述体系。

## 推荐阅读顺序（新成员）
1. `项目规格.md` §1–§3（定位 + 任务栏模型）
2. `功能需求.md` §0–§1、§3.0、§5（范围 + 覆盖层 + 布局 + 原子框架）
3. `技术栈选型.md`（全量技术决策）
4. `v1开发计划.md` + `版本排期.md`（实现与版本）
5. 按需：`公式引擎设计.md` / `公式函数参考.md` / `渲染架构设计.md`
6. 溯源：`klwp/*`

## 现行核心决策（一页速览）
- **技术栈**：C# / WPF (.NET 8)，无 WebView2（v1），**单 exe 单进程**（守护进程已移除，2026-07-15）。
- **覆盖模型**：非置顶全屏覆盖层（Topmost=False），任务栏系统自动隐藏、铺 WorkArea 不预留、弹出浮于其上；输入拦截（方案 B 透明穿透备选）。
- **层级**：三类预设层（壁纸层 / 网格化层 / 原子化 Px 画布层）+ 效果/交互层可扩展。
- **原子**：六类（Text/Shape/Icon/Image/Progress/Container）+ 属性三元组（静态/gv/公式）+ 公式引擎 19 函数。
- **布局**：Px 画布（原子自由定位，拖拽吸附到 cell）+ 网格层（四档 20/40/60/120，原子吸附 cell）；多页面（默认上限 9，独立网格/背景/原子）。
- **持久化**：本地 JSON，按「每屏 × 每层」`{ screens:[{id, layers:[...]}] }`。
- **版本**：v1.0 核心 / v1.1 组件化 / v1.2 实时数据+行为 / v2.0 多屏+deferred 分支（网页→WPF WebView2、本地LLM→HttpClient+XAML）。
