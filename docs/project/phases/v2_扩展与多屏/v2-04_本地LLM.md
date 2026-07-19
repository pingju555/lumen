# v2-04 本地 LLM

> 版本：v2.0 · 扩展与多屏（deferred 分支）
> 上游：`版本排期.md` §5、`功能需求.md` §6.6 / FR-ATOM（对话类）
> 关联：`P4-04_公式Provider闭环.md`（Provider 路由）、`P1-04_Px画布与Text原子.md`（对话 UI 容器）

## 目标
提供本地 LLM 对话能力：原生 `HttpClient` 调本地 LLM 服务（如 llama.cpp / ollama 暴露的 HTTP API）+ XAML 对话 UI 部件。无云依赖，守隐私。

## 范围
**包含**
- `LlmProvider`：HTTP 流式 / 非流式请求本地 LLM 端点（如 `http://localhost:11434/api/generate`）。
- 对话 UI：`Container` + `Text` 原子组合的对话面板（输入框 + 输出区），或专用 `ChatAtom`。
- 流式输出：分块 append 到文本（接 `P2-05` 增量刷新）。

**不含**：云端 LLM（v1 坚持本地）；模型下载 / 量化管理（用户自备本地服务）。

## 关键设计
```csharp
class LlmProvider {
    HttpClient _http = new();
    async IAsyncEnumerable<string> Generate(string prompt) {
        // POST localhost LLM API，逐块 yield 文本
    }
}
// 对话 UI：InputBox → Generate → 输出 TextAtom 增量更新
```

## 技术选型
`System.Net.Http.HttpClient` + WPF XAML（`技术栈选型.md` §5 deferred LLM 行）。零 NuGet（仅系统 HTTP）。

## FR 映射
`功能需求.md` §6.6 本地 LLM；对话部件。

## 验收
本地起 LLM 服务 → 覆盖层内对话面板提问 → 流式回复显示；无外部网络请求。

## 依赖与顺序
- 依赖：`P4-04`(Provider 路由)、`P2-05`(增量刷新)。
- 被依赖：v2 智能部件（结合天气 / 网页）。

## 风险 / 开放
- **本地服务形态未定**：ollama / llama.cpp server / 自定义？需用户拍板端点与模型（`版本排期.md` §8 开放）。
- 性能：本地推理占用 CPU/GPU，需与覆盖层 NFR 平衡（可在空闲时推理）。
- 流式解析：SSE / JSON 流解析需稳定实现。
