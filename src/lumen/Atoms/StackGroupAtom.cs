using System.Windows;

namespace Lumen.Atoms
{
    /// <summary>堆叠组：子原子纵向依次堆叠（StackPanel），自适应高度。</summary>
    public class StackGroupAtom : ContainerAtom
    {
        public StackGroupAtom() : base("Stack") { Bounds = new Rect(440, 120, 300, 200); }
        protected override string LayoutKey => "Stack";
    }
}
