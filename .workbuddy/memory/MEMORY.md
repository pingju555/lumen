# 项目长期记忆：Lumen（桌面覆盖层）
> 原名 Overlay。2026-07-16 重命名 Lumen：命名空间 Lumen.*、源码 src/lumen、配置 %LocalAppData%/Lumen/、日志 %TEMP%/lumen.log、标题/托盘 "Lumen"。

## 选型结论
非置顶全屏覆盖层：WPF 无边框 Topmost=False，铺满 SystemParameters.WorkArea（任务栏自动隐藏不预留），盖桌面图标/壁纸、居普通窗口下；任务栏系统自动隐藏，弹出浮于覆盖层上，其余区域拦截输入。单进程直接 UI（App.OnStartup→new LumenWindow().Show()），Ctrl+Alt+Q 退出，看门狗已移除。

## 关键坑（已验证）
- WPF+SetParent 进桌面 WorkerW=不可见。
- MA_NOACTIVATE/WS_EX_TRANSPARENT 整窗不激活→最小化窗口被系统逐个还原；穿透用 WM_NCHITTEST 选择性 HTTRANSPARENT。
- HWND_TOPMOST 置顶盖住任务栏触发区+普通窗口；v1 非置顶，子窗独立 Topmost=True。
- WS_EX_NO_ACTIVATE 覆盖层上勿用 WPF Popup 承载键盘输入浮层→用独立真实 Window（LumenWindow.CreateEditorWindow）。
- XAML 枚举属性勿写全限定名（TextWrapping="TextWrapping.Wrap"→"Wrap"），否则 XamlParseException→WER 静默杀进程。App.xaml.cs 全局异常处理器落 %TEMP%/lumen.log。

## 结构（src/lumen，WPF/.NET8）
- 入口 App；主窗 LumenWindow（PageManager 驱动，共享 LayerStack+PageHost）。
- Atoms/：Text/Shape/Icon/Image/Progress 五类 + 容器 StackGroup/OverlapGroup/SeriesGroup（2026-07-17 起 ContainerAtom 改抽象基类，LayoutKey 虚属性定布局，三子类独立注册；旧 Type:"Container" 按 layout 映射兼容）。AtomRegistry 注册；AtomTree.FindParentList 定位父列表做置顶·置底·复制·删除。
- Render/：GridModel（四档网格+九宫格锚点+XY偏移）、Layer/WallpaperLayer/BackgroundRef（每页独立背景，透明回退首非透明页）、Coord、LayerStack。
- Formula/：Lexer/Parser/Ast/Value/FormulaEngine/EvalContext + FunctionCatalog；Provider：si(PDH)、bi(内置)、gv(全局变量)、mi/mu(SMTC)、ai/an(启动坞)。$expr$ 包裹求值。
- 数据：Globals/GvStore、Engine/DirtyScheduler(1s tick+触发器)、Native/(PDH/SMTC/托盘/热键)、Persistence/ConfigStore(多 profile：profiles/<slug>.json 含 name/version/userPresets/pages[]/gv；meta.json 记激活档+语言)、Pages/、Presets/。
- Ui/：PropertyEditorPanel、TreeWindow、ColorPickerWindow、SettingsWindow、PageGridBgWindow、ProfileWindow、GvManagerPanel、PropWindow、InputBox、FilePickerWindow、FunctionCatalog。（FormulaTextBox 内联于 PropertyEditorPanel）
- 双模式 Atom.EditMode（默认false=桌面模式）。切页 Ctrl+Alt+←/→、热键 Ctrl+Alt+Q退出/H显隐/G网格/P预设/N新页。

## 行为/流程系统
- P5：点击动作9种 + 动画(进场/循环) + 触发器。
- 2026-07-18 触发器→Flow 重命名（AtomTrigger.cs 删，Flow.cs 新）；动作增 RunFlow/SetVar/Delay/ReadFile（全异步+循环引用防护）。RunFlow 按序号引用本原子流程，增删/重排会序号漂移（已接受）。
- P7 预设升级场景/Profile：PresetKind{Appearance,Scene}+Atoms(整页深拷贝)；Appearance 仅网格+背景(全部页)，Scene 整页替换(当前页)。CaptureFromPage 快照成 Scene。
- P7b 多 Profile：Profile=全部页面+每页设置+全局变量+用户预设，整体切换/导出/导入。ConfigStore 增 LoadActive/ListProfiles/Create/Delete/Rename/Export/Import；LumenWindow 增 _activeProfile+方法；右键菜单「配置档…」开 ProfileWindow。

## i18n 体系（已完成）
- UI 中/英，外置 lang/<code>.json（扁平 key + 点分 menu./settings./func./msg.）；内置+%LocalAppData%/Lumen/lang 覆盖优先；csproj Content 发布。
- XAML：{loc:Loc key}（I18n/LocExtension）。Loc.T(key,args) 查表→en-GB 回退→原样返 key。Load(code) 切语言+持久化+LangChanged。Init() 在 App.OnStartup 早期。
- 语言码 zh-CN/en-GB；默认跟随 CurrentUICulture，非 zh 落 en-GB。存 meta.json lang（与 active 解耦）。
- 热切换：菜单/窗口用时构建→重开即新语言；长驻主窗标题/托盘+已开窗口订阅 LangChanged（SettingsWindow 已订阅+Closing 退订）。
- 刻意不译：EditField.Choices 枚举值、AtomTypePicker 类型项、ColorPicker 通道标签、FunctionCatalog Sig/Insert/Name、原子 Type 规范值。
- 手册：激活档是手册时 OnLangChanged→RefreshManualIfActive 按新语言重装重载页面。

## Shape 原子（KLWP 对照）
- 仅 Rect/Ellipse（RoundRect=Rect+radius、Line=Rect 变体）；14 种程序化质感(_gloss 叠加，不依赖位图)：Frosted/Glass/Plastic/Metal/Neon/Matte/Wood/Marble/Carbon/Holographic/Paper/Fabric/Liquid；支持宽/高/描边/圆角/虚线/阴影(DropShadow)/旋转。下拉已 i18n（规范值不译）。
- KLWP 更丰富：Arc/Triangle/Hexagon/Polygon/Diagonal、渐变填充、参数化外阴影、Mask、图层混合。Lumen 14 质感是差异化优势须保留。
- 增强候选(待确认)：①基础形状 ②渐变填充 ③参数化阴影 ④Mask/窗口级毛玻璃 ⑤图层混合。

## 容器尺寸/定位（2026-07-17 拍板）
- 组件/Stack/Overlap/Series 皆容器，尺寸随内部自适应，不框定固定 W/H。定位=中心点+九宫格锚点+XY偏移：RecalcPosition 解析为中心，SyncPosition 按 ActualWidth/Height 反推 Canvas.Left/Top。
- 组件变量 cg(name[,def])：解析链 ExternalOverrides→InternalDefaults→gv→def；写侧仅属性面板「外部变量」Tab 手填。作用域靠 EvalContext.Parent+IComponentVarResolver。BuildCustomTab 内建变量编辑器。**函数公式与变量体系待重构（用户拍板）。**

## 编辑器重构设计（2026-07-18，详 docs/编辑器重构设计.md）
- KLWP=三栏常驻(项目列表/属性编辑器/工具栏)+Overlay弹窗。属性编辑器6 Tab：形状/油漆/图层/位置/动画/触摸。
- Lumen 对齐：形状/位置/动画/流程已齐；差距=公式编辑器缺实时预览+函数分类网格、图层缺混合/滤镜/模糊、控件风格待统一。质感须保留。
- 优先级：P0=属性面板6 Tab+控件统一；P1=FormulaTextBox预览+函数网格+TreeWindow内嵌；P2=AddComponent卡片网格+图层混合；P3=序列/文本变形/GIF/预建组件原子。8 待拍板见文档末。

## 构建
cd "D:/Main/AI Quest&Project/桌面覆盖层/src/lumen" && dotnet build -c Release（目标 net8.0-windows10.0.22621.0）。
