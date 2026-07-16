using System;
using Lumen.Formula;
using Lumen.Globals;

namespace Lumen.Atoms
{
    /// <summary>属性三元组模式（静态 / 全局变量 gv / 公式 $...$）。</summary>
    public enum PropMode { Static, Global, Formula }

    /// <summary>
    /// 属性值三态：运行时统一解析为 <see cref="Value"/>。
    /// 序列化形如 "字面量" / "gv:name" / "$expr$"。
    /// 详见 docs/project/phases/P2_原子全集与公式引擎/P2-02_属性三元组.md
    /// </summary>
    public abstract class PropertyValue
    {
        public PropMode Mode { get; protected set; }

        public abstract Value Resolve(EvalContext ctx);

        /// <summary>从序列化串解析三元组。</summary>
        public static PropertyValue Parse(string raw)
        {
            if (raw == null) return new StaticValue("");
            if (raw.StartsWith("gv:", StringComparison.OrdinalIgnoreCase))
                return new GvRef(raw.Substring(3).Trim());
            if (raw.Contains("$"))
                return new FormulaValue(raw);
            return new StaticValue(raw);
        }

        /// <summary>还原为可求值的模板串（静态→字面量；gv→$gv(name)$；公式→原串）。</summary>
        public string Materialize()
        {
            switch (this)
            {
                case FormulaValue fv: return fv.Expr;
                case GvRef g: return "$gv(" + g.Name + ")$";
                case StaticValue s: return s.Literal?.ToString() ?? "";
                default: return "";
            }
        }

        /// <summary>持久化序列化：静态→字面量；gv→"gv:name"；公式→"$expr$"。</summary>
        public static string Serialize(PropertyValue p) => p switch
        {
            FormulaValue fv => fv.Expr,
            GvRef g => "gv:" + g.Name,
            StaticValue s => s.Literal?.ToString() ?? "",
            _ => ""
        };

        public static string ResolveText(PropertyValue p, EvalContext ctx)
            => ctx.EvalText(p.Materialize());
    }

    public class StaticValue : PropertyValue
    {
        public object Literal;
        public StaticValue(object v) { Mode = PropMode.Static; Literal = v; }
        public override Value Resolve(EvalContext ctx) => Value.FromObject(Literal);
    }

    public class GvRef : PropertyValue
    {
        public string Name;
        public GvRef(string name) { Mode = PropMode.Global; Name = name; }
        public override Value Resolve(EvalContext ctx) => ctx.ResolveGv(Name);
    }

    public class FormulaValue : PropertyValue
    {
        public string Expr;
        public FormulaValue(string expr) { Mode = PropMode.Formula; Expr = expr; }
        public override Value Resolve(EvalContext ctx)
        {
            // 序列化形如 "$expr$"：文本类属性经 EvalText 处理 $，但数值/其他类属性直接走 Eval，
            // 需先剥掉外层 $…$ 再求值，否则词法分析遇 $ 报错返回 Null。
            var e = Expr;
            if (e != null && e.StartsWith("$") && e.EndsWith("$") && e.Length >= 2)
                e = e.Substring(1, e.Length - 2);
            return ctx.Eval(e ?? "");
        }
    }
}
