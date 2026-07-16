using Lumen.Globals;

namespace Lumen.Formula
{
    /// <summary>
    /// 求值上下文：持有全局变量表 + 数据提供器 + 当前时间。
    /// 公式 / 全局变量引用经此读取。详见 docs/project/公式引擎设计.md §5
    /// </summary>
    public class EvalContext
    {
        public GvStore Gv { get; }
        public IDataProvider Provider { get; }

        public EvalContext(GvStore gv, IDataProvider provider)
        {
            Gv = gv;
            Provider = provider;
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
