# P4-03 启动坞 ai() / an()

> Phase：P4 · 数据接入（si / mi / mu / ai / an）
> 上游：`v1开发计划.md` P4、`功能需求.md` §6 启动坞、FR-LAUNCH-*`、`技术栈选型.md` §6（启动坞）
> 关联：`P4-04_公式Provider闭环.md`、`P5-03_按钮行为.md`、`P2-03_公式引擎.md`

## 目标
枚举已装程序，经 `ai()`（应用信息：路径 / 图标 / 类别）/ `an()`（应用名列表）暴露，支撑启动坞部件。来源：开始菜单 `.lnk` 扫描（`IShellLink`）+ UWP `PackageManager`。

## 范围
**包含**
- `AiProvider : IDataProvider`：`ai(name)` / `ai(path)` / `ai(icon)` / `an()`（所有应用名列表）。
- 桌面应用：递归扫描开始菜单目录 `*.lnk`，`IShellLink` 解析目标路径 + 图标。
- UWP 应用：`PackageManager.FindPackages()`（WinRT）枚举，取可执行 / URI 启动方式。
- 启动：给定应用名 → `Process.Start`（桌面 exe）或 `Launcher.LaunchUriAsync`（UWP，P5 按钮调用本模块 `Launch(name)`）。

**不含**：应用图标缩略图渲染（`P6-xx` 文件速览复用 `SHGetFileInfo`）；搜索 / 分类高级（v1 按名列表）。

## 关键设计
### 扫描
```csharp
class AiProvider : IDataProvider {
    List<AppEntry> _apps = new();          // 名称/路径/图标/类别
    void ScanStartMenu() {                  // %ProgramData% + %AppData% 开始菜单 *.lnk
        foreach (var lnk in Directory.EnumerateFiles(startMenu, "*.lnk", AllDir))
            _apps.Add(ResolveLnk(lnk));     // IShellLink.GetPath / GetIconLocation
    }
    void ScanUwp() {                        // PackageManager.FindPackages
        foreach (var p in _pm.FindPackages())
            _apps.Add(FromPackage(p));
    }
    public Value Get(string name, Value[] a) { /* ai/an 查询 */ }
    public void Launch(string appName) {    // 供 P5 按钮
        var e = _apps.First(x => x.Name==appName);
        if (e.IsUwp) Launcher.LaunchUriAsync(new Uri(e.Uri));
        else Process.Start(e.Path);
    }
}
```

### 缓存
- 启动 / 安装变更时扫描（可监听 `FileSystemWatcher` 开始菜单目录，留 P6 文件速览复用）。

## 技术选型
`IShellLink` COM P/Invoke（桌面 lnk）、`Windows.Management.Deployment.PackageManager`（WinRT，UWP）、`System.Diagnostics.Process`（启动）（`技术栈选型.md` §6 启动坞行）。零 NuGet。

## FR 映射
FR-LAUNCH-*（启动坞）；ai / an 函数（`公式函数参考.md`）。

## 验收
`$an()$` 列出已装程序名；点击启动坞图标（P5 按钮）→ `Launch(name)` 启动对应程序（桌面 / UWP 均通）。

## 依赖与顺序
- 依赖：`P2-03`(IDataProvider)、`P5-03`(按钮 Launch 调用)。
- 被依赖：`P4-04`(聚合)、P6 文件速览（`SHGetFileInfo` 复用）。

## 风险 / 开放
- UWP `PackageManager` 需 WinRT + 可能受限（非打包应用查全部包需相应 capability / 在自包含下投影正确）。
- 扫描性能：开始菜单 lnk 可能数百，首扫可放后台线程 + 进度。
- 图标解析：`IShellLink.GetIconLocation` 或 `SHGetFileInfo` 取图标句柄转 `ImageSource`。
