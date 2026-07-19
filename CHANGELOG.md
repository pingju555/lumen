# Changelog

所有重要变更均记录在此文件。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

---

## [v1.3.2] — 2026-07-20

> 修两个 v1.3.1 回归（内置手册不刷新 / 属性窗口未统一）+ 配置默认位置改为便携（随 exe）。

### 修复
- **内置「使用手册」启动即刷新** — `ConfigStore.LoadActive()` 原先仅在首次运行/无配置档时调用 `InstallBuiltinManual()`；已安装用户走另一分支直接 `Load(active)`，**从不重新安装**，导致旧 `profiles/使用手册.json` 永不被 v2.1 覆盖（表现为「新手册没加载」）。现每次启动先 `RefreshBuiltinManual()`（仅覆盖文件、不切激活档），激活档若为手册则自然读到最新版；语言切换路径 `RefreshManualIfActive()` 仍按 `Loc.Cur` 覆盖写入对应语言版
- **属性窗口（PropWindow）接入 ChromeWindow 统一框架** — PropWindow 此前是唯一的自定义 inline chrome 窗口（标题栏/底部操作栏各自实现），与设置/配置档/页面背景三窗口风格割裂。现继承 `ChromeWindow`：共享标题栏（14×14 蓝色方块 + 13px 标题 + 关闭钮）、内容区 16,12 留白、底部 `FooterContent` 操作栏（取消选定/取消/应用，按钮样式对齐 ProfileWindow）。同步为 `ChromeWindow` 补**无边框缩放**支持（`WM_NCHITTEST` 命中测试，仅 `ResizeMode=CanResize` 启用，NoResize 窗口不受影响），使属性窗口既可统一外观又保留可调整大小
- **配置默认位置改为便携（随 exe）** — `Core/LumenPaths.DefaultDataDir` 由 `%LocalAppData%/Lumen` 改为**程序（exe）所在文件夹**。即 zip 解压到哪，配置（profiles/config/settings/lang）与日志就落在哪（exe 旁），实现真正的便携运行，无需安装、不写系统目录。`lumen.location` 指针文件仍位于 exe 旁，用于重定向到任意其它文件夹（设置 → 数据存储位置一键迁移）。`Core/Logger` 回退由 `%TEMP%/lumen.log` 改为 `<exe 文件夹>/lumen.log`，与便携默认根一致。`App.MaybeAutoMigrate` 增加旧 `%LocalAppData%/Lumen` → 便携默认（或指针位置）的一次性迁移提示，从 v1.3.1 及更早升级不丢旧数据

---

## [v1.3.1] — 2026-07-20

> 性能监控实时化 + 帮助手册重做 + 数据存储位置可配置/可迁移。

### 改进
- **PDH 性能数据实时刷新** — `DirtyScheduler` 新增 `UsesPerf`（检测公式含 `si(`），Tick 对含 PDH 的公式标脏；此前 `SystemDataProvider` 后台采样不反向标脏，所有性能仪表盘首帧后静止。修复后 CPU/内存/磁盘/网络等随真实时刷新（引擎级缺口）
- **帮助手册推倒重做 v2.1** — `artifacts/gen_manual.py` 数据驱动双语生成；封面 13 主题卡 + 11 陈列页（质感画廊 / 形状画廊 / 性能仪表盘 `$si$` 实时 / 音乐控制器 / 世界时钟 / 动画长廊 / 布局实验室 / 公式游乐场 / 变量联动墙 / 场景预设墙 / 图标图像Gif），共 **25 页 / 583 原子**；手册页脚版本 `v1.3` → `v1.3.1`
- **数据存储位置可配置 + 可迁移** — 新增 `Core/LumenPaths.cs` 统一解析数据根（默认 `%LocalAppData%/Lumen`，指针文件 `lumen.location` 可重定向）；`ConfigStore`/`AppSettings`/`Loc` 三处路径改走 `LumenPaths`（默认位置行为不变，向后兼容）。设置 → 数据存储位置：浏览选目标文件夹 + 迁移按钮，复制全部数据到新位置并写指针，重启生效（旧位置留作备份）。首次启动若指针指向空自定义位置而默认位置有旧数据，弹双语提示询问迁移
- **诊断日志跟随数据根** — `Core/Logger.cs` 日志路径改为 `<数据根>/lumen.log`（本地独立读取指针定位，无指针时回退 `%TEMP%/lumen.log`），随数据存储位置设置与迁移一并跟随，无需单独配置

---

## [v1.3.0] — 2026-07-19

> 页数上限解放 + 全局快捷键精简。

### 改进

- **页数上限 9 → 99** — `PageManager.MaxPages` 由硬编码 9 提升至 99；切页热键、页面列表、网格渲染、配置保存/加载原本即按 `Pages.Count` 遍历，全程支持多页
- **全局快捷键精简** — 仅保留 `Ctrl+Alt+Q`（退出）与 `Ctrl+Alt+E`（切换编辑 / 桌面模式，新增）；上一页 / 下一页改为**空占位**（不绑默认键，需在 `LumenWindow.RegisterHotKeys` 取消注释并自选 VK 后启用）；删除 `Ctrl+Alt+G/P/N/H`（换网格档 / 套预设 / 新页 / 显隐），对应功能保留、改由右键菜单 / 托盘调用
- **文档同步** — README 三语言快捷键段重写；帮助手册（双语）快捷键页重建为 4 张卡（Q 退出 / E 切模式 / ⇄ 换页占位 / 说明）；手册页脚版本号 `v1.2` → `v1.3`

---

## [v1.2.1.2] — 2026-07-19

> 公式引擎：运算符语义全面审计 + 浮点显示去噪。

### 修复 / 改进

- **公式运算符语义审计** — `+` 改为「两侧皆数字则数值相加，否则拼接」（修复 `1+2="12"`）；新增 `%`（取模，零保护）、`&&`/`||`（逻辑短路别名）、`==`（等同 `=`）、一元 `+` 透传；修复 `==` 分词 bug（双等号被拆成两个单等号 token）导致比较失效
- **浮点显示去噪** — `0.1+0.2` 不再显示 `0.30000000000000004`；结果字符串化时自动去掉二进制尾数误差位（取最短干净表示，如 `0.30000000000000004`→`0.3`）；计算仍走 `double`，语义/性能不变
- **`mu(round)` 增强** — 支持小数位参数 `mu(round, x, n)`（原仅取整 `mu(round, x)`）

## [v1.2.1.1] — 2026-07-19

> 编辑器交互打磨：九宫格锚点选择器 + 文本底框去除 + 锚点定位语义修正。

### 修复 / 改进

- **九宫格锚点选择器（魔方图）** — 属性面板「布局」页下拉栏替换为 3×3 占位方块阵（66×66，红点指位），点击即选；`EditKind` 新增 `Anchor`，视觉对照锚点角，一行主序对应 `NineAnchor` 枚举
- **文本部件去底框** — `TextAtom.BgProp` 默认值 `#22000000` → `#00000000`，文字不再带半透明底色方块，仅由选中矩形轮廓包住
- **九宫格锚点定位语义修正** — 旧逻辑把画布基准点当原子左上角/中心，选 TopRight 时整件冲出画布；新模型「锚点角对齐」：画布基准点 + `AnchorFracX/Y` 反推原子对应角对齐画布对应角（Center/TopLeft 无回归）；`WriteBackOffsetFromBounds` 同步修正；删除 `ContainerAtom.RecalcPosition` 重写，容器统一走基类
- **魔方图尺寸收敛** — 选择器网格 96×96 → 66×66，红点 11→8，边距与圆角同步缩小

## [v1.2.1] — 2026-07-19

> v1.2.0 发布后的缺陷修复补丁。

### 修复

- **项目列表删除功能恢复** — 树重建后 `SelectedItem` 归零导致删除失效，`Delete_Click` 改以 `_currentAtom` 为准，删后清理属性 Tab 与画布选中
- **项目列表 ↔ 桌面画布选中双向同步** — 新增 `SelectAtomFromTree`（树→画布，防环路）；`SelectAtom` 递归导航到父容器层级；`RebuildTree` 恢复高亮；`_syncingSelection` 隔断回环
- **堆叠组 / 重叠组选中与右键修复** — 窗口穿透判定 `HitTestAtScreen` 改用实际渲染框 `GetRenderBounds`（`TransformToVisual` + `RenderSize`），替换原 `Bounds` 坐标错乱路径；删除 `ContainerContains` 局部/全局坐标系混用
- **图像原子无图占位** — 公式无图像或解码失败时整尺寸淡灰（`0xD9D9D9`）铺满 + 居中「无图像」文本；有图时还原 `bg` 底色
- **容器虚线框贴合内容外轮廓** — 重叠组 `ShrinkCanvasToContent` 按子部件实际包围盒（含左上角偏移）收紧 Canvas，裁剪空白；堆叠组/序列组本就自动 hug，无需改动

## [v1.2.0] — 2026-07-19

> 实时数据 + 行为系统 + 编辑器打磨。公式体系从 19 精简到 14 函数，新增 GIF 动态图片原子。

### 数据接入（P4）

- **系统指标 `si`** — CPU / 内存 / 网速 / 磁盘 / 屏幕 DPI / 深色模式（PDH 采集）
- **电池 `bi`** — 电量 / 充电状态 / 插电状态
- **媒体 `mi`** — SMTC：标题 / 艺术家 / 专辑 / 封面 / 播放状态 / 进度 / 时长
- **启动坞 `ai` / `an`** — 应用枚举与启动
- **公式 Provider 闭环** — 聚合式 `DataProvider` + `EvalContext`，支持系统 / 媒体 / 启动器 / RSS 数据
- **调色板 `bp`** — 中位切分提取封面主色

### 行为系统（P5）

- **9 种进场 / 循环动画** — Fade / Slide / Zoom / Drop / Pulse / Rotate / Blink / Float / Bounce（`Storyboard` 驱动）
- **触发器 → Flow 流程系统** — 7 种动作 + 循环引用防护
- **10 类按钮动作** — RunApp / RunFlow / ToggleEdit / SwitchPage / LockScreen / OpenLink / MediaControl / SetVar / Delay / ReadFile
- **交互式进度动画 `ProgressAnimator`** — Timer / Formula / Touch 三种触发源；6 种动作属性（Fade / TranslateX / TranslateY / Rotate / Scale / Zoom）；5 种缓动曲线（linear / easeInOut / bounce / overshoot）

### 背景 / 设置 / 打磨（P6）

- **背景渲染** — 纯色 / 公式 / 图片，每页独立背景
- **设置面板 + Profile 导入导出** — 语言 / 自启 / 快捷键信息
- **14 种程序化质感** — 毛玻璃 / 玻璃感 / 塑料感 / 金属 / 霓虹 / 哑光 / 木纹 / 大理石 / 碳纤维 / 虹彩 / 纸张 / 布纹 / 液态，纯代码实现，不依赖位图
- **图层混合模式** — 7 种 KLWP 混合模式（`BlendModes.ps` 像素着色器）
- **图层模糊** — `BlurEffect` 即时生效
- **文本原子 4 种尺寸模式** — FixedHeight / AutoWidth / FixedWidth / FitBounds
- **函数体系精简 19 → 14** — 合并删除 `ts` / `tz` / `dp` / `uc` / `re` / `rng`；每函数 `Params` 内参标签，点击自动填入
- **背景全透明智能穿透** — 空白处点击穿透到桌面，原子上拦截输入
- **编辑器打磨** — PropWindow 10 项 BUG/WARNING 修复、控件样式统一、页面管理 Tab、容器选中修复、选中框增强

### 新增（本轮额外）

- **GIF 动态图片原子** — `GifBitmapDecoder` 解码帧 + 处置方式（RestoreBackground / RestorePrevious）感知合成，避免透明拖影；`DispatcherTimer` 换帧，`Speed` 倍速 0.25×–4×；`Render` 重建时停旧计时器防泄漏

### 打包 / 发布（Release）

- **版本号治理** — `Lumen.csproj` 单一 `<Version>1.2.0</Version>` 源，`AssemblyVersion` / `FileVersion` / `ProductVersion` 由此派生（此前硬编码 1.0.0.0）
- **便携 ZIP 打包脚本** — `packaging/package.ps1`：`dotnet publish --self-contained false`（framework-dependent，不含 .NET 8 运行时），扁平压缩，解压后 `lumen.exe` 位于根目录；移除 `*.pdb`、输出 SHA256
- **运行依赖** — 目标机须预装 **.NET 8 Desktop Runtime**（含 WPF / WinForms）；WinRT 投影层（`Microsoft.Windows.SDK.NET.dll` 等）为应用直接依赖，随包提供，无需额外安装。包体由 ~77MB 降至 ~7.6MB
- **产物** — `dist/Lumen-<version>-win-x64.zip`（`*.zip` / `dist/` / `artifacts/` 已 gitignore，不入库）
- **范围** — 仅便携 ZIP，无安装器 / 无 MSIX / 暂不代码签名（SmartScreen 警告可接受）

### 修复

- 进度动画编辑字段恢复显示（此前被注释 `REMOVED FOR DEBUGGING`）
- 图层 Tab 混合 / 模糊从 UI 占位升级为真实渲染
- i18n 旧 key 清理（`prop.tab.style` / `layout` / `interaction` / `anim`）

### 已知问题

- NFR 门禁 7 项中 2/7 需真机验证（性能 / 内存），无代码阻塞
- v2.0 扩展功能未开始（多屏 / 天气 / WebView2 / 本地 LLM / 动态壁纸 / 插件化）

---

## [v1.1.0] — 2026-07-19

### 首个公开预发布版本

> 完整功能：覆盖窗口 + 六类原子 + 公式引擎 + 网格/页面/预设 + 系统数据 + 行为系统 + 编辑器。

### 新增

- **ChromeWindow 统一窗口框架** — 5 个编辑器窗口（设置/属性/树/页面背景/配置档）共享 ControlTemplate，统一标题栏+内容+页脚
- **PropWindow 三合一编辑器** — 项目/属性/页面 扁平 TabControl，KLWP 风格红条选中指示
- **KLWP 风格项目列表** — 类型色块 + 名称 + 子描述（参数概要）+ □ 公式状态框
- **公式编辑器增强** — FunctionCatalog 全面重写：中文参数名、实用 Insert 示例、分类 emoji 前缀
- **公式实时预览** — 公式输入框下方绿字显示求值结果，红字显示语法错误
- **14 种程序化质感** — Frosted/Glass/Plastic/Metal/Neon/Matte/Wood/Marble/Carbon/Holographic/Paper/Fabric/Liquid，不依赖位图，WPF 纯代码实现
- **交互式进度动画** — 三种触发源（Timer / Formula / Touch），六种动作属性（Fade / TranslateX / TranslateY / Rotate / Scale），五种缓动曲线（linear / easeInOut / bounce / overshoot）
- **添加组件面板** — 8 类原子卡片网格选型界面，替代旧文本弹窗
- **Tab 体系 KLWP 对齐** — 绘色(Paint) / 图层(Layer) / 位置(Position) / 动画(Animation) / 触摸(Touch)
- **多配置档（Profile）** — 全部页面 + 每页设置 + 全局变量 + 用户预设的整体切换/导出/导入
- **i18n 中英双语界面** — 简体中文 / 英式英语，设置中运行时热切换，外置 lang/*.json
- **Theme.xaml 视觉 Token 系统** — 15+ 色彩令牌 + 圆角令牌 + 尺寸令牌，单一事实来源
- **内置使用手册** — 首次运行自动载入手册配置档，`Ctrl+Alt+→ / ←` 翻页

### 修复

- WPF 两层 TitleBar 重叠（`WindowStyle=None` 构造函数内显式设置）
- `XamlParseException` 静默杀进程 — 全局异常处理器落 `%TEMP%/lumen.log`
- 枚举属性全限定名写法导致的 XAML 解析崩溃
- 侧边面板崩溃 — 回退到独立 TreeWindow
- 多窗口 i18n key 缺失 — prop.tab.* 体系完整覆盖
- Shape 纹理透明背衬修复
- 原子尺寸编辑缺失修复

### 变化

- **Tab 别名对齐 KLWP**：样式→绘色 · 布局→位置 · 交互→触摸 · `prop.tab.anim` → `prop.tab.animation`
- `AtomTrigger.cs` → `Flow.cs`（触发器重命名为流程系统）
- `PropertyEditorPanel` 不再自建 TabControl，由 PropWindow 统一管理
- 移除看门狗守护进程（单进程直接 UI）
- 移除侧边面板 SidebarPanel（已回退）
- Shape 类型体系收敛为 Rect / Ellipse 两基元（Line 并入细矩形，RoundRect 并入矩形+圆角参数）
- 公式函数与变量体系重构 — 八位 base62 `Atom.Id` 统一寻址

### 已知问题（历史，已于 v1.2.0 修复）

- 进度动画编辑字段当前被注释（`Atom.cs` 中标记 `REMOVED FOR DEBUGGING`），运行时仍生效，动画 Tab 中暂不显示
- 图层混合/模糊为 UI 框架占位，渲染实现留 v1.2
- 程序首次启动时如未创建配置文件，部分窗口可能因缺少种子数据而展示空白
- 编辑模式下联动偶现 PropWindow 内容未及时刷新

---

## [Unreleased]

### 路线图（待启动）

- v2.0 扩展功能：多屏多实例 / 天气数据 / 网页 WebView2 / 本地 LLM / 动态壁纸视频 / 原子插件化 / 混合模式与 PDH 精化
- 预建组件模板（依赖预览图体系，计划 V1.5）

---

[v1.1.0]: https://github.com/pingju555/lumen/releases/tag/v1.1.0
[v1.2.0]: https://github.com/pingju555/lumen/releases/tag/v1.2.0
