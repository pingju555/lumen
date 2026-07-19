using System;
using System.Text;

namespace Lumen.Formula
{
    /// <summary>
    /// 公式引擎门面（五段式 Lexer→Parser→AST→Registry→Provider）。
    /// 容错：任何异常返回 Null，由上层回退静态值。
    /// 详见 docs/project/公式引擎设计.md
    /// </summary>
    public static class FormulaEngine
    {
        public static Value Eval(string expr, EvalContext ctx)
        {
            if (string.IsNullOrWhiteSpace(expr)) return Value.Of("");
            try
            {
                var toks = Lexer.Tokenize(expr);
                var ast = Parser.Parse(toks);
                return ast.Eval(ctx) ?? Value.Null();
            }
            catch (Exception)
            {
                return Value.Null();
            }
        }

        /// <summary>解析文本内联 $...$：逐段求值并替换，非公式段原样保留。</summary>
        public static string EvalText(string tmpl, EvalContext ctx)
        {
            if (string.IsNullOrEmpty(tmpl)) return "";
            int i = 0, n = tmpl.Length;
            var sb = new StringBuilder();
            while (i < n)
            {
                if (tmpl[i] == '$')
                {
                    int j = tmpl.IndexOf('$', i + 1);
                    if (j > i + 1)
                    {
                        string inner = tmpl.Substring(i + 1, j - i - 1);
                        sb.Append(Eval(inner, ctx).AsStr());
                        i = j + 1;
                        continue;
                    }
                }
                sb.Append(tmpl[i]);
                i++;
            }
            return sb.ToString();
        }
    }
}
