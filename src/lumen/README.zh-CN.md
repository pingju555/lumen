# Lumen

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

> **[English Version](README.en-GB.md)** · English README available.

---

## 为什么有 Lumen

我一直在寻找一种能真正自由定制 Windows 桌面的方式。

手机上有 KLWP（Kustom Live Wallpaper），它让 Android 用户能在桌面上放任何东西——时钟、电池、天气、系统数据、动态动画——而且完全由你控制布局和交互。但在 Windows 上，你能得到的要么是简陋的小工具（Windows 7 之后基本死了），要么是笨重的侧边栏，要么就是置顶窗口把正常界面挡住。

我想要的是：

- 盖在壁纸上、但不挡住窗口的一层「可编辑画布」
- 能实时展示系统数据（CPU 占用、网速、内存、电池）
- 能用公式把数据变成可视化组件（`$si(cpu)$` → 进度条、`$bi(level)$` → 电量文本）
- 能像 PPT 一样有多页，每页独立布局
- 能在编辑器里拖拽摆放，所见即所得
- 能在桌面上直接交互（点击运行程序、切页、触发动作）

KLWP 在手机上做到了，为什么 Windows 不行？

Lumen 就是对这个问题的回答——让 Windows 桌面也能拥有 KLWP 级别的自由度和表现力。

## 特性

- **全屏覆盖、非置顶**：盖住桌面图标/壁纸，普通窗口正常浮于其上；任务栏可自动隐藏。
- **六类原子**：文本 / 形状 / 图标 / 图片 / 进度 / 容器，可拖拽、改属性、组合嵌套。
- **多页 + 双模式**：编辑模式（摆放、调属性）与桌面模式（静态展示 + 点击交互 + 触发器自动化）一键切换。
- **公式引擎**：`$expr$` 包裹求值，内置函数（df/tf/si/bi/gv/mi/mu/ce/bp/wg/if/tc/uc/re/fl/rng/ts/tu/tz/dp）；支持运算符、颜色、逻辑；可绑定系统/媒体/外部数据。
- **数据源与变量**：系统数据（`si`：CPU/内存/网速/磁盘）、电池（`bi`）、媒体（`mi`）、数学（`mu`）、颜色运算（`ce`）、调色板（`bp`）、RSS（`wg`）；变量（`gv` 全局 / 组件内 `gv("",name)`）跨页共享。
- **点击动作 + 触发器**：点击运行程序 / 媒体控制 / 切页 / 锁屏 / 打开链接等；条件满足时自动执行动作流（Once / While）。
- **多配置档（Profile）**：每档 = 全部页面 + 每页设置 + 全局变量 + 用户预设，可切换、导出、导入。
- **内置使用手册**：首次运行自动载入「使用手册」配置档，用 `Ctrl+Alt+→ / ←` 翻页浏览。
- **双语界面**：内置简体中文 / 英式英语界面语言切换（设置中可选，记忆到本地）。

## 技术栈

- WPF / C# / .NET 8（`net8.0-windows10.0.22621.0`）
- 纯 XAML + 后台代码，无第三方 UI 框架
- 自研公式解析器、渲染坐标系统（九宫格锚点自适应窗口尺寸）
- 系统数据通过 P/Invoke（PDH、SMTC、Shell）采集

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
├── Actions/                 动画/流程/动作系统
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

> 灵感来源：[KLWP (Kustom Live Wallpaper)](https://kustom.rocks/) — Android 上最强大的自定义桌面工具。  
> Lumen 致力于将类似的理念带到 Windows 桌面。
