using System.Collections.Generic;
using Lumen.Atoms;
using Lumen.Render;

namespace Lumen.Pages
{
    /// <summary>
    /// 页面模型（P3-03）：每页独立网格档位 + 背景 + 原子。
    /// 页面间相互独立（v1 不共享层）。背景为 P6 预留引用。
    /// 详见 docs/project/phases/P3_网格小组件页面/P3-03_页面.md
    /// </summary>
    public class Page
    {
        public string Name { get; set; } = "页面";
        public double GridSize { get; set; } = 40;
        public bool ShowGrid { get; set; } = true;
        public BackgroundRef Background { get; set; } = new BackgroundRef();

        /// <summary>该页的原子（可直接添加 / 编辑 / 删除）。</summary>
        public List<Atom> Atoms { get; set; } = new();

        /// <summary>全部参与渲染 / 调度的原子。</summary>
        public IEnumerable<Atom> AllAtoms()
        {
            foreach (var a in Atoms) yield return a;
        }
    }
}
