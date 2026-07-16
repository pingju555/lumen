using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Lumen.Formula;

namespace Lumen.Atoms
{
    /// <summary>
    /// 原子宿主：把原子列表渲染并挂到目标 Canvas 层。注入 EvalContext 供公式重算；
    /// 提供 Flatten() 供增量调度器收集（含 Container 嵌套子原子）。
    /// </summary>
    public class AtomHost
    {
        private readonly Canvas _canvas;
        private List<Atom> _atoms = new();

        public AtomHost(Canvas canvas) { _canvas = canvas; }

        public void Compose(IEnumerable<Atom> atoms, EvalContext ctx)
        {
            _atoms = new List<Atom>(atoms);
            _canvas.Children.Clear();
            foreach (var a in _atoms)
            {
                a.Ctx = ctx;
                a.OnChanged = () => OnChanged?.Invoke();
                var ui = a.Render();
                Canvas.SetLeft(ui, a.Bounds.X);
                Canvas.SetTop(ui, a.Bounds.Y);
                _canvas.Children.Add(ui);
            }
        }

        public IReadOnlyList<Atom> Atoms => _atoms;

        /// <summary>递归展平所有原子（含 Container 的子），供脏标记调度。</summary>
        public IEnumerable<Atom> Flatten()
        {
            var list = new List<Atom>();
            foreach (var a in _atoms) Collect(a, list);
            return list;
        }

        private static void Collect(Atom a, List<Atom> outList)
        {
            outList.Add(a);
            if (a is ContainerAtom c)
                foreach (var ch in c.Children) Collect(ch, outList);
        }

        public event Action OnChanged;
    }
}
