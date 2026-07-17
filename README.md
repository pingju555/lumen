# Lumen

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

**Lumen** 是一个 Windows 桌面覆盖层（Desktop Overlay）：它铺满整个工作区、盖在桌面图标与壁纸之上，却位于普通窗口之下。你可以在上面摆放轻量小组件（时钟、电量、网速、媒体控制等），用类似 PPT 的多页方式组织信息，并通过公式实时绑定系统数据。

> 它不是传统意义的 HUD 置顶窗口，而是一层「安静地躺在桌面与窗口之间」的可编辑画布。

## 特性

- **全屏覆盖、非置顶**：盖住桌面图标/壁纸，普通窗口正常浮于其上；任务栏可自动隐藏。
- **六类原子**：文本 / 形状 / 图标 / 图片 / 进度 / 容器，可拖拽、改属性、组合嵌套。
- **多页 + 双模式**：编辑模式（摆放、调属性）与桌面模式（静态展示 + 点击交互 + 触发器自动化）一键切换。
- **公式引擎**：`$expr$` 包裹求值，19 个内置函数，支持运算符、颜色、逻辑；可绑定系统/媒体/启动坞数据。
- **数据源与变量**：系统数据（`si`：CPU/内存/网速/磁盘）、电池（`bi`）、媒体（`mi`/`mu`）、启动坞（`ai`/`an`）；全局变量（`gv`）跨页共享。
- **点击动作 + 触发器**：点击运行程序 / 媒体控制 / 切页 / 锁屏 / 打开链接等；条件满足时自动执行动作流（Once / While）。
- **多配置档（Profile）**：每档 = 全部页面 + 每页设置 + 全局变量 + 用户预设，可切换、导出、导入。
- **内置使用手册**：首次运行自动载入「使用手册」配置档，用 `Ctrl+Alt+→ / ←` 翻页浏览。
- **双语界面**：内置简体中文 / 英式英语界面语言切换（设置中可选，记忆到本地）。

## 技术栈

- WPF / C# / .NET 8（`net8.0-windows10.0.22621.0`）
- 纯 XAML + 后台代码，无第三方 UI 框架
- 自研公式解析器、渲染坐标系统（九宫格锚点自适应窗口尺寸）、`System.Data` 之外的系统数据通过 P/Invoke（PDH、SMTC、Shell）采集

## 构建与运行

要求 Windows 10/11 + .NET 8 SDK。

```powershell
cd src/lumen
dotnet build -c Release
# 运行
.\bin\Release\net8.0-windows10.0.22621.0\lumen.exe
```

- 退出：`Ctrl+Alt+Q`
- 显隐覆盖层：`Ctrl+Alt+H`
- 切页：`Ctrl+Alt+← / →`
- 编辑模式：`右键菜单` → 进入编辑 …（网格 `G`、预设 `P`、新页 `N` 仅在编辑模式生效）
- 配置档管理：`右键菜单` → 配置档…
- 界面语言：`右键菜单` → 设置 → 语言

## 目录结构

```
src/lumen/
├── App.xaml(.cs)            应用入口与全局异常处理
├── LumenWindow.xaml(.cs)    主窗口（页面/模式/配置档/快捷键）
├── Atoms/                   六类原子 + 注册表 + 属性系统
├── Render/                  坐标/网格/图层/背景
├── Formula/                 词法/语法/求值 + 19 函数
├── Persistence/             多 Profile 持久化（ConfigStore）
├── Globals/ Engine/ Native/ 全局变量 / 脏调度 / P/Invoke
├── Presets/ Ui/ Pages/      预设 / 窗口 / 页面管理
├── Resources/               内置种子档（使用手册等）
├── I18n/                    双语语言包（zh-CN / en-GB）与 Loc 静态类
└── Icons/                   程序图标
```

## 作者

- GitHub：[@pingju555](https://github.com/pingju555)
- 邮箱：2336317586@qq.com

## 许可

本项目采用 **MIT 许可证**，详见 [LICENSE](LICENSE)。

---

# Lumen (English)

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

**Lumen** is a Windows desktop overlay: it fills the entire work area, sitting above your desktop icons and wallpaper yet below ordinary windows. You can place lightweight widgets on it (clock, battery, network speed, media controls, and more), organise information across PowerPoint-style pages, and bind them to live system data through formulas.

> It is not a traditional always-on-top HUD window, but an editable canvas that quietly rests between the desktop and your windows.

## Features

- **Full-screen overlay, non-topmost**: covers desktop icons/wallpaper while ordinary windows float normally above it; the taskbar can auto-hide.
- **Six atom types**: text / shape / icon / image / progress / container — draggable, re-styleable, and nestable.
- **Multi-page + dual mode**: switch instantly between edit mode (arrange, tweak properties) and desktop mode (static display + click interaction + trigger automation).
- **Formula engine**: `$expr$`-wrapped evaluation, 19 built-in functions, with operators, colours and logic; bind to system / media / launcher data.
- **Data sources & variables**: system data (`si`: CPU/memory/network/disk), battery (`bi`), media (`mi`/`mu`), launcher (`ai`/`an`); global variables (`gv`) shared across pages.
- **Click actions + triggers**: run a program / media control / page switch / lock screen / open a link on click; automatically run action flows (Once / While) when conditions are met.
- **Multi-profile workspaces**: each profile = all pages + per-page settings + global variables + user presets; switchable, exportable and importable.
- **Built-in user manual**: the "User Manual" profile loads automatically on first run; browse pages with `Ctrl+Alt+→ / ←`.
- **Bilingual UI**: built-in Simplified Chinese / British English language switcher (choose in Settings, remembered locally).

## Tech Stack

- WPF / C# / .NET 8 (`net8.0-windows10.0.22621.0`)
- Pure XAML + code-behind, no third-party UI framework
- Custom formula parser, rendering coordinate system (nine-grid anchors that adapt to window size), and system data collected via P/Invoke (PDH, SMTC, Shell) beyond `System.Data`

## Build & Run

Requires Windows 10/11 + .NET 8 SDK.

```powershell
cd src/lumen
dotnet build -c Release
# run
.\bin\Release\net8.0-windows10.0.22621.0\lumen.exe
```

- Exit: `Ctrl+Alt+Q`
- Show / hide overlay: `Ctrl+Alt+H`
- Switch page: `Ctrl+Alt+← / →`
- Edit mode: `right-click menu` → Enter edit … (grid `G`, preset `P`, new page `N` only work in edit mode)
- Profile management: `right-click menu` → Profiles…
- UI language: `right-click menu` → Settings → Language

## Project Structure

```
src/lumen/
├── App.xaml(.cs)            App entry and global exception handling
├── LumenWindow.xaml(.cs)    Main window (pages / modes / profiles / shortcuts)
├── Atoms/                   Six atom types + registry + property system
├── Render/                 Coordinates / grid / layers / background
├── Formula/                Lexer / parser / evaluator + 19 functions
├── Persistence/            Multi-profile persistence (ConfigStore)
├── Globals/ Engine/ Native/ Global vars / dirty scheduler / P/Invoke
├── Presets/ Ui/ Pages/     Presets / windows / page management
├── Resources/              Built-in seed data (user manual, etc.)
├── I18n/                    Bilingual language packs (zh-CN / en-GB) and the Loc class
└── Icons/                  App icons
```

## Author

- GitHub: [@pingju555](https://github.com/pingju555)
- Email: 2336317586@qq.com

## License

This project is licensed under the **MIT License** — see [LICENSE](LICENSE).
