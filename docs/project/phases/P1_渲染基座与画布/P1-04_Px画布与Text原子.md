# P1-04 Px 画布与 Text 原子

> Phase：P1 · 渲染基座 + 多层 + Px 画布 + 单原子
> 上游：`v1开发计划.md` P1、`功能需求.md` FR-CANVAS-01~05、FR-ATOM（Text）、`技术栈选型.md` §6（Px 画布）
> 关联：`P1-02_坐标系.md`、`P1-03_Atom抽象与注册.md`、`P1-05_临时持久化.md`、`P2-01_六类原子.md`

## 目标
在 `CanvasLayer` 上实现 Px 绝对坐标画布：可拖拽定位一个 **Text 原子（静态字符串）**，配标尺 / 吸附参考线，验证端到端"放原子 → 拖动 → px 定位 → 刷新保留"。

## 范围
**包含**
- `Canvas` 绝对坐标（`Canvas.SetLeft/Top`）承载原子 `UIElement`。
- 拖拽：基于 `Thumb` 或 `DragDelta` 改 `Bounds`，实时更新画布位置。
- 标尺：顶部 / 左侧 px 刻度（`Grid` + `TextBlock` 或 `DrawingVisual`）。
- 参考线 / 吸附：拖动时吸附到 px 网格或参考线（p0 级 snap，P3 接 gcd 网格）。
- Text 原子：静态字符串渲染（`TextBlock`），属性三元组仅静态值（gv / 公式留 P2）。

**不含**：六类原子其余（P2-01）；公式驱动文本（P2-03）；网格 cell 吸附（P3-01）；持久化全量（P2-06，此处仅临时存一个原子）。

## 关键设计
### 画布 + 拖拽
```csharp
var canvas = new Canvas();                 // CanvasLayer.Root
var thumb = new Thumb { /* 覆盖 TextBlock */ };
thumb.DragDelta += (_, e) => {
    atom.Bounds = new Rect(atom.Bounds.X + e.HorizontalChange,
                           atom.Bounds.Y + e.VerticalChange,
                           atom.Bounds.Width, atom.Bounds.Height);
    Canvas.SetLeft(ui, atom.Bounds.X);
    Canvas.SetTop(ui, atom.Bounds.Y);
};
```

### Text 原子（P1 版静态）
```csharp
class TextAtom : Atom {
    public string Text { get; set; }       // 静态值
    public override UIElement Render() => new TextBlock { Text = Text };
}
```

### 标尺 / 吸附
- 标尺：`Canvas` 顶部/左侧固定 `Panel`，按 px 间隔画刻度（或 `Adorner`）。
- 吸附：拖拽 delta 取整到 `snapPx`（P1 用 1px 或粗网格，P3 替换为 gcd cell）。

## 技术选型
WPF `Canvas` + `Thumb` + `TextBlock`（`技术栈选型.md` §6 Px 画布行）。无第三方。

## FR 映射
FR-CANVAS-01（Px 画布）、FR-CANVAS-02（自由定位）、FR-CANVAS-03（拖拽）、FR-CANVAS-04（标尺 / 参考线）、FR-CANVAS-05（吸附）；FR-ATOM Text（静态）。

## 验收
画布放一个 Text，可拖动、按 px 定位；标尺显示刻度；拖动有吸附；经 `P1-05_临时持久化.md` 存盘后刷新坐标保留。

## 依赖与顺序
- 依赖：`P1-01`(层)、`P1-02`(坐标)、`P1-03`(Atom/注册)。
- 被依赖：P2-01 六类原子在此画布渲染；P3-01 网格吸附接管 snap。

## 风险 / 开放
- 标尺性能：高频重绘需用 `DrawingVisual` 而非大量 `TextBlock`，v1 原子量小可接受。
- 吸附与 grid 联动需在 P3 统一 `Coord.GridToCanvas`。
