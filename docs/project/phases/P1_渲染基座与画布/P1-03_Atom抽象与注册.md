# P1-03 Atom 抽象与注册

> Phase：P1 · 渲染基座 + 多层 + Px 画布 + 单原子
> 上游：`v1开发计划.md` P1、`功能需求.md` FR-ATOM-01~06/18、`技术栈选型.md` §6（原子框架）
> 关联：`P1-01_多层模型.md`、`P1-04_Px画布与Text原子.md`、`P2-01_六类原子.md`、`P2-03_公式引擎.md`

## 目标
建立 `Atom` 抽象基类与 `ATOM_REGISTRY` 运行时注册表，实现"按类型分发渲染"。P1 仅验证 Text 原子注册 → 渲染 → 挂到 `CanvasLayer.Root` 的链路。

## 范围
**包含**
- `Atom` 抽象：`Type` / `Bounds(Rect)` / `Properties`(属性三元组占位，P2 填充) / `Render()` 产出 `UIElement`。
- `ATOM_REGISTRY`：`Dictionary<string, Func<Atom>>` 注册 + 按 `Type` 取工厂。
- 渲染分发：遍历原子 → `Render()` → 加入层 `Canvas`。

**不含**：六类原子具体实现（P2-01）、属性三元组求值（P2-02/03）、增量重算（P2-05）。

## 关键设计
### 抽象
```csharp
abstract class Atom {
    public string Type { get; }
    public Rect Bounds { get; set; }          // Canvas(px)
    public abstract UIElement Render();        // 产出可视元素
    public virtual void Update() { }           // P2 增量重算钩子
}
```

### 注册表与分发
```csharp
static class ATOM_REGISTRY {
    static Dictionary<string, Func<Atom>> _reg = new();
    public static void Register(string type, Func<Atom> factory);
    public static Atom Create(string type);
}
class AtomHost {                                // 挂在 CanvasLayer
    void Compose(IEnumerable<Atom> atoms) {
        foreach (var a in atoms) {
            var ui = a.Render();
            Canvas.SetLeft(ui, a.Bounds.X);
            Canvas.SetTop(ui, a.Bounds.Y);
            _canvas.Children.Add(ui);
        }
    }
}
```

## 技术选型
C# 抽象类 + 字典注册表（轻量，零依赖，`技术栈选型.md` §6 原子框架行）。渲染产物为 WPF `UIElement`。

## FR 映射
FR-ATOM-01（原子抽象）、FR-ATOM-02（类型注册）、FR-ATOM-03（渲染分发）、FR-ATOM-04~06/18（具体在 P2，此处留接口）。

## 验收
注册 Text 原子 → `Create("Text")` 产出实例 → `Compose` 将其 `UIElement` 挂到画布且位置正确。

## 依赖与顺序
- 依赖：`P1-01_多层模型.md`（目标层）、`P1-02_坐标系.md`（Bounds 单位）。
- 被依赖：P1-04 Text 原子实现、P2-01 六类原子、P3-02 小组件（原子树打包）。

## 风险 / 开放
- 属性三元组（静态 / gv / 公式）在 P2 引入，`Render()` 届时需支持动态 `Update()`。
- 插件化动态加载原子（FR-ATOM-11）留 v2，当前注册表为编译期。
