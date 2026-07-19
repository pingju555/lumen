using System.Collections.Generic;
using Lumen.Atoms;
using Lumen.Render;

namespace Lumen.Presets
{
    /// <summary>
    /// 预设种类：
    /// Appearance = 外观模板（仅网格 + 背景，不动原子）—— 如 GridWorkspace / Day / Night。
    /// Scene      = 完整场景（原子布局 + 网格 + 背景整页快照），套用即整页替换。
    /// </summary>
    public enum PresetKind { Appearance = 0, Scene = 1 }

    /// <summary>
    /// 预设模型（P3-04，P7 升级）：
    /// Appearance 只含「层 / 网格 / 背景」；Scene 额外含整页原子列表（Atoms），可一键套用整个桌面布置。
    /// 详见 docs/project/phases/P3_网格小组件页面/P3-04_预设库.md
    /// </summary>
    public class Preset
    {
        public string Name { get; set; } = "Preset";
        /// <summary>预设种类：外观模板 / 完整场景。</summary>
        public PresetKind Kind { get; set; } = PresetKind.Appearance;
        /// <summary>三类预设层组合（Wallpaper/Grid/Canvas）。</summary>
        public List<LayerKind> Layers { get; set; } = new();
        /// <summary>网格档位（GridWorkspace 用）。</summary>
        public double GridSize { get; set; } = 40;
        /// <summary>背景引用（P6 实际渲染）。</summary>
        public BackgroundRef Background { get; set; } = new BackgroundRef();
        /// <summary>场景预设的整页原子（Appearance 预设为空）。套用/保存时均深拷贝，互不共享实例。</summary>
        public List<Atom> Atoms { get; set; } = new();
    }
}
