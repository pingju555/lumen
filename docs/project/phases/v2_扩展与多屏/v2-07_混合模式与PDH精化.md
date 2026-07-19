# v2-07 混合模式精化 + PDH 精度

> 版本：v2.0 · 扩展与多屏（deferred 分支）
> 上游：`版本排期.md` §5、`v1开发计划.md` 备注（混合模式 / PDH 精度遗留）、`技术栈选型.md` §6.1 / §6.2
> 关联：`P1-01_多层模型.md`（层合成）、`P4-01_系统指标si.md`（PDH 采样）

## 目标
补齐 v1 遗留的两项精化：
1. **混合模式**：层间 `multiply` / `screen` 等混合（v1 仅 alpha 叠加）。
2. **PDH 精度**：系统指标（网速 / 磁盘）多实例聚合与更精确速率计算。

## 范围
**包含**
### 混合模式
- `Layer.BlendMode`：`Normal` / `Multiply` / `Screen` / `Overlay`。
- 实现：WPF `Visual` 混合用 `VisualBrush` + `BlendEffect`（需 `Microsoft.Expression.Drawing` 或手写 `ShaderEffect`），或落到 `WriteableBitmap` 像素混合（重但可控）。

### PDH 精度
- 网速 / 磁盘：多网卡 / 多磁盘实例聚合（求和或选活跃）；采样窗口对齐（避免抖动）。
- 计数器实例枚举：`PerformanceCounterCategory.GetInstanceNames` 选正确实例。

**不含**：任意混合模式全集（v1 三项常用）；历史聚合曲线（仅当前值）。

## 关键设计
### 混合
```csharp
enum BlendMode { Normal, Multiply, Screen, Overlay }
// 方案 A：ShaderEffect 自定义混合（轻量）
// 方案 B：RenderTargetBitmap 离线混合（精确但耗 CPU，需 P2-05 降频）
```

### PDH 聚合
```csharp
// 网速：遍历所有 "Network Interface" 实例，求和 Bytes/sec
foreach (var inst in cat.GetInstanceNames())
    if (!inst.Contains("Loopback")) total += pc[inst].NextValue();
```

## 技术选型
WPF `ShaderEffect` / `RenderTargetBitmap`（`技术栈选型.md` §6.1 混合模式留 v1.x）；`PerformanceCounter` 多实例（`技术栈选型.md` §6.2 PDH 精化留 v1.x）。混合模式若需 `ShaderEffect` 库需评估体积（守 NFR-01）。

## FR 映射
`技术栈选型.md` §6.1 / §6.2 遗留项；层混合（扩展 FR-LAYER）。

## 验收
层设 Multiply → 与下层正片叠底正确；Screen 反相提亮正确；网速聚合多网卡无重复 / 漏算。

## 依赖与顺序
- 依赖：`P1-01`(层合成)、`P4-01`(PDH)。
- 被依赖：v2 高级视觉 / 精确监控部件。

## 风险 / 开放
- 混合模式 `ShaderEffect` 依赖 `Microsoft.Expression.Drawing` 或手写 HLSL，体积 / 许可需评估（可能超 NFR-01 上限 → 用 `RenderTargetBitmap` 像素法替代，但更耗 CPU）。
- PDH 多实例选择策略（"活跃网卡"判定）与 `P4-01` 一致，需统一。
