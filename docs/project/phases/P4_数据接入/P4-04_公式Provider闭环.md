# P4-04 公式 Provider 闭环

> Phase：P4 · 数据接入（si / mi / mu / ai / an）
> 上游：`v1开发计划.md` P4（数据接入公式 Provider 闭环）、`技术栈选型.md` §6（数据接入）
> 关联：`P4-01_系统指标si.md`、`P4-02_媒体mi_mu.md`、`P4-03_启动坞ai_an.md`、`P2-03_公式引擎.md`、`P2-05_增量重算与容错.md`

## 目标
将 P4-01~03 的各数据源聚合为一个 `IDataProvider` 实现，注册进 `P2-03` 的 `EvalContext`，使公式引擎能产出**真实系统数据**，并接入 `P2-05` 脏调度实现实时刷新。本模块是 P2 引擎与 P4 数据源的"闭环接缝"。

## 范围
**包含**
- `CompositeDataProvider : IDataProvider`：按函数名前缀路由到 `si` / `mi` / `mu` / `ai` / `an` 子 provider。
- 注册：启动时构造各子 provider → 注入 `EvalContext.DataProvider`。
- 刷新驱动：子 provider 的 Timer / 事件 → 调 `P2-05.DirtyScheduler.MarkDirty(相关原子)`。
- 控制 API 暴露：`mu(...)` / `ai.launch(name)` 供 P5 按钮调用（经 Composite 转发）。

**不含**：数据源具体采集算法（在各子模块）；持久化（P2-06）；UI（P5/P6）。

## 关键设计
### 聚合
```csharp
class CompositeDataProvider : IDataProvider {
    Dictionary<string, IDataProvider> _routing = new() {
        ["si"] = new SiProvider(),
        ["mi"] = new MiProvider(),
        ["mu"] = new MuController(),   // 控制类也走 IDataProvider 接口或单独注册
        ["ai"] = new AiProvider(),
        ["an"] = new AiProvider(),
    };
    public Value Get(string name, Value[] args) =>
        _routing.TryGetValue(name, out var p) ? p.Get(name, args) : Value.Null;
}
// 启动时
var ctx = new EvalContext { DataProvider = new CompositeDataProvider(), Gv = gvStore };
```

### 脏驱动
- `SiProvider` 的 1s tick → `dirty.MarkDirty(atomsUsingSi)`。
- `MiProvider` 的 SMTC 事件 → `dirty.MarkDirty(atomsUsingMi)`。
- `AiProvider` 安装变更 → 标记启动坞原子脏。
- 原子→数据源依赖在首次求值时记录（轻量依赖图，复用 `P2-05`）。

## 技术选型
组合模式 + `IDataProvider` 接口（定义于 `P2-03`）；`技术栈选型.md` §6 数据接入行。无第三方。

## FR 映射
`公式引擎设计.md` DataProvider 章；P4 全数据源闭环。

## 验收
`$si(cpu)$` / `$mi(title)$` / `$an()$` 经同一 `EvalContext` 产出真实数据；数据源变化时对应原子自动刷新；`mu(playpause)` / `ai.launch` 经 Composite 转发成功。

## 依赖与顺序
- 依赖：`P2-03`(EvalContext/IDataProvider)、`P2-05`(脏调度)、`P4-01/02/03`(子 provider)。
- 被依赖：P5 行为（按钮调 mu/ai）、P6-03 打磨（NFR 抽检数据刷新 CPU）。

## 风险 / 开放
- 依赖图精度：若仅"含 si 的原子"标记，需解析公式引用的函数名 → 在 `P2-03` 解析期收集（预留 hook）。
- 多源并发：各 provider 独立 Timer，统一脏收口防重算风暴（合并到同帧 Flush）。
