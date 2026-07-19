# v2-03 网页部件（WebView2）

> 版本：v2.0 · 扩展与多屏（deferred 分支）
> 上游：`版本排期.md` §5、`v1开发计划.md` 备注（网页 / iframe）、`功能需求.md` §6.6 / FR-ATOM（网页类）
> 关联：`P1-03_Atom抽象与注册.md`（新增 WebAtom）、`P0-01_覆盖窗口.md`（嵌入宿主）

## 目标
引入网页部件：在覆盖层内嵌入 WPF `WebView2` 控件（`Microsoft.UI.WebView2`），承载网页 / iframe 内容（如网页小组件、仪表盘）。**不切 Tauri / Electron**（守护逻辑不变，`技术栈选型.md` §5.2）。

## 范围
**包含**
- `WebAtom : Atom`：内含 `WebView2` 控件，加载 URL / HTML。
- 交互：网页内点击不穿透覆盖层（接 `P0-01` 输入模型）；可选允许网页接收输入。
- 守护不变：仍单 exe 双模式，`WebView2` 仅作控件嵌入。

**不含**：完整浏览器（无地址栏 / 多标签）；网页沙箱隔离深度（v1 基础）。

## 关键设计
```csharp
class WebAtom : Atom {
    WebView2 _wv = new();
    public override UIElement Render() {
        _wv.Source = new Uri(Url);
        return _wv;
    }
}
```
- 需初始化 `WebView2` 环境（`CoreWebView2Environment`），首次可能下载运行时。
- 嵌入覆盖层透明窗口：`WebView2` 默认不透明，需设背景或 `DefaultBackgroundColor`。

## 技术选型
`Microsoft.UI.WebView2`（NuGet，`技术栈选型.md` §5.2 网页行）。仍 C#/WPF 宿主，零框架切换。

## FR 映射
`功能需求.md` §6.6 网页部件；FR-ATOM 网页类（扩展）。

## 验收
WebAtom 加载指定 URL 并显示；网页内交互正常；覆盖层其余部分仍吸收输入；守护 / 自恢复不变。

## 依赖与顺序
- 依赖：`P1-03`(原子注册)、`P0-01`(输入模型)。
- 被依赖：v2 仪表盘 / 网页小组件。

## 风险 / 开放
- **运行时分发体积**：WebView2 运行时 ~大（~150MB+），违反 NFR-01 <80MB 核心目标 → 需 Evergreen（系统共享运行时）或固定版本包（体积暴涨）。关键开放决策。
- 透明 / 圆角：`WebView2` 与 WPF 透明窗口合成有坑（需 `AllowExternalDrop` / 背景色处理）。
- 安全：嵌入网页需防恶意内容，v1 仅加载用户配置的可信 URL。
