using System.Windows;

namespace Lumen.Atoms
{
    /// <summary>序列组：子原子横向流式排布（WrapPanel），宽度不足时换行。</summary>
    public class SeriesGroupAtom : ContainerAtom
    {
        public SeriesGroupAtom() : base("Series") { Bounds = new Rect(440, 120, 360, 160); }
        protected override string LayoutKey => "Series";
    }
}
