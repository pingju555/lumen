# Lumen v1.1.0

> 让 Windows 桌面拥有 KLWP 级别的自由度和表现力。

---

## 新增

- **ChromeWindow 统一窗口框架** — 5 个编辑器窗口（设置/属性/部件树/页面背景/配置档）共享 ControlTemplate，统一标题栏+内容+页脚结构
- **PropWindow 三合一编辑器** — 项目/属性/页面 扁平 TabControl，KLWP 风格红条选中
- **KLWP 风格项目列表** — 类型色块 + 名称 + 子描述（参数概要）+ □ 公式状态框
- **Formula 编辑器增强** — FunctionCatalog 全面重写：中文参数名、实用 Insert 示例、分类 emoji 前缀
- **公式实时预览** — 输入框下方绿字显示求值结果，红字显示语法错误
- **14 种程序化质感** — Frosted/Glass/Plastic/Metal/Neon/Matte/Wood/Marble/Carbon/Holographic/Paper/Fabric/Liquid，不依赖位图
- **交互式进度动画** — Timer/Formula/Touch 触发，6 种动作属性（Fade/TranslateX/Y/Rotate/Scale），5 种缓动曲线
- **添加组件面板** — 8 类原子卡片网格，替代旧文本弹窗选类型
- **Tab 体系 KLWP 对齐** — 绘色/图层/位置/动画/触摸
- **多配置档（Profile）** — 切换/导出/导入，内置使用手册
- **i18n 双语界面** — 简体中文 / 英式英语，运行时热切换

## 修复

- WPF 两层 TitleBar 重叠问题（WindowStyle=None 构造函数内显式设置）
- `XamlParseException` 静默杀进程（全局异常处理器落 `%TEMP%/lumen.log`）
- 占位符枚举属性写全限定名导致的解析崩溃
- 侧边面板崩溃回退（回退到独立 TreeWindow）
- 多窗口 i18n key 缺失（prop.tab.* 体系完整覆盖）

## 变化

- **Tab 别名对齐 KLWP**：样式→绘色 · 布局→位置 · 交互→触摸 · `prop.tab.anim` → `prop.tab.animation`
- `AtomTrigger.cs` → `Flow.cs`（触发器重命名为流程）
- `PropertyEditorPanel` 不再自建 TabControl，改为由 `PropWindow` 统一管理
- 移除看门狗守护进程（单进程直接 UI）
- 移除侧边面板 SidebarPanel（已回退为独立 TreeWindow）

## 已知问题

- 进度动画编辑字段当前被注释（`Atom.cs` 中标记 `REMOVED FOR DEBUGGING`），运行时仍生效，但动画 Tab 中暂不显示
- 图层混合/模糊为 UI 框架占位，渲染实现留 v1.2
- 编辑模式下交互式动画属性面板尚未完全展示

## 构建方法

```powershell
cd src/lumen
dotnet build -c Release
.\bin\Release\net8.0-windows10.0.22621.0\lumen.exe
```

要求 Windows 10/11 + .NET 8 SDK。
