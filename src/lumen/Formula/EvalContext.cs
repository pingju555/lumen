using Lumen.Globals;

namespace Lumen.Formula
{
    /// <summary>
    /// 组件变量解析器（只读）：组件实例持有自身的两张变量表（内部默认 + 外部覆盖），
    /// 实现此接口供公式引擎沿 ctx 父链查找最近的组件作用域。定义在 Formula 层以避免 Atoms↔Formula 循环依赖。
    /// </summary>
    public interface IComponentVarResolver
    {
        /// <summary>按名解析组件变量；未定义返回 Value.Null()。</summary>
        Value Resolve(string name);
    }

    /// <summary>
    /// 求值上下文：持有全局变量表 + 数据提供器 + 当前时间。
    /// 公式 / 全局变量引用经此读取。详见 docs/project/公式引擎设计.md §5
    ///
    /// 组件作用域：支持 <see cref="Parent"/> 链 + <see cref="Resolver"/>（指向某组件实例的变量表）。
    /// 组件子树经 <see cref="ContainerAtom.ChildContext"/> 拿到「链到父 ctx、多一层 Resolver」的子 ctx，
    /// 公式函数（如 cg）经 <see cref="NearestResolver"/> 沿父链找最近的组件变量，实现「当前实例」语义。
    /// </summary>
    public class EvalContext
    {
        public GvStore Gv { get; }
        public IDataProvider Provider { get; }

        /// <summary>父上下文（组件子 ctx 链向页面级 ctx）。根 ctx 为 null。</summary>
        public EvalContext Parent { get; }
        /// <summary>本层组件变量解析器（指向某组件实例的两张表）；非组件层的 ctx 为 null。</summary>
        public IComponentVarResolver Resolver { get; set; }

        public EvalContext(GvStore gv, IDataProvider provider, EvalContext parent = null)
        {
            Gv = gv;
            Provider = provider;
            Parent = parent;
        }

        /// <summary>沿 Parent 链自近向远找第一个非 null 的 Resolver（最近的组件作用域）。</summary>
        public IComponentVarResolver NearestResolver()
        {
            var c = this;
            while (c != null)
            {
                if (c.Resolver != null) return c.Resolver;
                c = c.Parent;
            }
            return null;
        }

        public Value ResolveGv(string name)
        {
            var tv = Gv.Get(name);
            if (tv == null) return Value.Null();
            return tv.ToValue();
        }

        /// <summary>解析含 $...$ 内联的文本模板（如 "电量 $bi(level)$%"）。</summary>
        public string EvalText(string tmpl) => FormulaEngine.EvalText(tmpl, this);

        /// <summary>纯表达式求值入口。</summary>
        public Value Eval(string expr) => FormulaEngine.Eval(expr, this);
    }
}
