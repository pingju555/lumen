# Changelog

所有重要变更均记录在此文件。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

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

### 新增函数

- `si` — 系统指标（CPU / 内存 / 网速 / 磁盘 / 屏幕分辨率 / 深色模式）
- `bi` — 电池（电量 / 充电状态）
- `mi` — 媒体信息（SMTC：标题 / 艺术家 / 封面 / 播放状态 / 进度）
- `mu` — 数学运算（round / sin / cos / clamp / lerp 等 26 种）
- `ce` — 颜色运算（亮度 / 色相 / 饱和度 / 混合 / 反转）
- `bp` — 调色板（中位切分提取封面主色）
- `gv` / `if` / `tc` / `df` / `tf` / `ts` / `tu` / `tz` 等 19 函数

### 已知问题

- 进度动画编辑字段当前被注释（`Atom.cs` 中标记 `REMOVED FOR DEBUGGING`），运行时仍生效，动画 Tab 中暂不显示
- 图层混合/模糊为 UI 框架占位，渲染实现留 v1.2
- 程序首次启动时如未创建配置文件，部分窗口可能因缺少种子数据而展示空白
- 编辑模式下联动偶现 PropWindow 内容未及时刷新

---

## [Unreleased]

### 待定

- 进度动画编辑字段恢复显示
- 图层 Tab 渲染增强（混合模式 / 模糊）
- 编辑器控件统一（步进器 / 滑块 / □ 公式状态图标）
- i18n 旧 key 清理（`prop.tab.style/layout/interaction/anim`）
- NFR 系统化门禁测试（体积 / CPU / 帧率 / 内存）
- v2.0 扩展功能（多屏 / 天气 / WebView2 / 本地 LLM / 插件化）

---

[v1.1.0]: https://github.com/pingju555/lumen/releases/tag/v1.1.0
