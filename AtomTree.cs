using System.Collections.Generic;
using Lumen.Atoms;
using Lumen.Pages;

namespace Lumen
{
    /// <summary>
    /// 原子树工具（P3 部件级菜单用）：在页面各集合中定位某个原子所在的父级列表，
    /// 以便执行删除 / 复制 / 置顶 / 置底。覆盖：页顶层原子、容器嵌套子原子。
    /// 用引用相等（ReferenceEquals）匹配（页面持有原子对象本身，不做深拷贝）。
    /// </summary>
    public static class AtomTree
    {
        /// <summary>返回包含 target 的父级列表；找不到返回 null。</summary>
        public static List<Atom> FindParentList(Page page, Atom target)
        {
            if (page == null || target == null) return null;

            if (page.Atoms.Contains(target)) return page.Atoms;
            foreach (var a in page.Atoms)
            {
                var r = FindInContainer(a, target);
                if (r != null) return r;
            }
            return null;
        }

        private static List<Atom> FindInContainer(Atom node, Atom target)
        {
            if (node is ContainerAtom c)
            {
                if (c.Children.Contains(target)) return c.Children;
                foreach (var ch in c.Children)
                {
                    var r = FindInContainer(ch, target);
                    if (r != null) return r;
                }
            }
            return null;
        }
    }
}
