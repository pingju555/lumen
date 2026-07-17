using System.Windows;

namespace Lumen.Atoms
{
    /// <summary>重叠组：子原子以 Canvas 绝对定位（按各自 Bounds 叠加），对应 KLWP 重叠组能力。
    /// v1 仅交付绝对定位叠加；图层混合模式（滤色/正片叠底等）留待 v1.x 增强。</summary>
    public class OverlapGroupAtom : ContainerAtom
    {
        public OverlapGroupAtom() : base("Overlap") { Bounds = new Rect(440, 120, 400, 300); }
        protected override string LayoutKey => "Overlap";
    }
}
