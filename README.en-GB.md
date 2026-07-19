# Lumen

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

> **[中文版](README.zh-CN.md)** · 简体中文自述文件可用。

---

## Why Lumen

I spent a long time looking for a way to truly customise the Windows desktop.

On mobile, there's KLWP (Kustom Live Wallpaper). It gives Android users the freedom to put anything on their home screen — clocks, battery levels, weather, live system data, dynamic animations — and you control every pixel of the layout and interaction. On Windows, though, the options are bleak: forgotten desktop gadgets (dead since Windows 7), clunky sidebar panels, or always-on-top windows that get in the way of your actual work.

What I wanted was:

- An editable canvas that sits over the wallpaper but **under** normal windows
- Real-time system data rendered as beautiful components (CPU usage, network speed, memory, battery)
- Formulas to transform raw data into visual elements (`$si(cpu)$` → progress bar, `$bi(level)$` → battery text)
- Multiple pages, like slides in a presentation, each with its own independent layout
- Drag-and-drop editing — what you see is what you get
- Click interactions directly on the desktop (launch apps, switch pages, trigger actions)

KLWP nailed it on Android. Why shouldn't Windows have the same?

Lumen is my answer to that question — bringing KLWP-grade freedom and expressiveness to the Windows desktop.

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
- Custom formula parser, rendering coordinate system (nine-grid anchors that adapt to window size)
- System data collected via P/Invoke (PDH, SMTC, Shell)

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
├── Render/                  Coordinates / grid / layers / background
├── Formula/                 Lexer / parser / evaluator + 19 functions
├── Actions/                 Animation / flow / action system
├── Persistence/             Multi-profile persistence (ConfigStore)
├── Globals/ Engine/ Native/ Global vars / dirty scheduler / P/Invoke
├── Presets/ Ui/ Pages/      Presets / windows / page management
├── Resources/               Built-in seed data (user manual, etc.)
├── I18n/                    Bilingual language packs (zh-CN / en-GB) and the Loc class
└── Icons/                   App icons
```

## Author

- GitHub: [@pingju555](https://github.com/pingju555)
- Email: 2336317586@qq.com

## License

This project is licensed under the **MIT License** — see [LICENSE](LICENSE).

---

> Inspired by [KLWP (Kustom Live Wallpaper)](https://kustom.rocks/) — the most powerful customisation tool on Android.  
> Lumen aims to bring that same philosophy to the Windows desktop.
