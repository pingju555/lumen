namespace Lumen.Render
{
    /// <summary>背景引用（P6 背景渲染预留）：类型 + 资源路径。v1 仅占位，套用预设时记录，P6 实际绘制。</summary>
    public class BackgroundRef
    {
        /// <summary>背景种类：solid / image / glass / blur（P6 细化）。</summary>
        public string Kind { get; set; } = "solid";
        /// <summary>资源路径或颜色（P6 用）。</summary>
        public string Source { get; set; } = "#FF1E1E1E";
    }
}
