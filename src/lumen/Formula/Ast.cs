using System.Collections.Generic;
using System.Globalization;

namespace Lumen.Formula
{
    public abstract class AstNode
    {
        public abstract Value Eval(EvalContext ctx);
    }

    public class LiteralNode : AstNode
    {
        private readonly Value _v;
        public LiteralNode(Value v) { _v = v; }
        public override Value Eval(EvalContext ctx) => _v;
        public static LiteralNode Null { get; } = new LiteralNode(Value.Null());
    }

    /// <summary>裸标识符 → 读全局变量 gv(name)（宽松：未注册返回 Null）。</summary>
    public class GvVarNode : AstNode
    {
        private readonly string _name;
        public GvVarNode(string name) { _name = name; }
        public override Value Eval(EvalContext ctx) => ctx.ResolveGv(_name);
    }

    public class CallNode : AstNode
    {
        private readonly string _name;
        private readonly List<AstNode> _args;
        public CallNode(string name, List<AstNode> args) { _name = name; _args = args; }
        public override Value Eval(EvalContext ctx)
        {
            var vals = new Value[_args.Count];
            for (int k = 0; k < _args.Count; k++) vals[k] = _args[k].Eval(ctx);
            return FunctionRegistry.Call(_name, vals, ctx);
        }
    }

    public class IfNode : AstNode
    {
        private readonly AstNode _cond, _then, _else;
        public IfNode(AstNode cond, AstNode then, AstNode els) { _cond = cond; _then = then; _else = els; }
        public override Value Eval(EvalContext ctx)
            => _cond.Eval(ctx).AsBool() ? _then.Eval(ctx) : _else.Eval(ctx);
    }

    public class UnaryNode : AstNode
    {
        private readonly string _op;
        private readonly AstNode _e;
        public UnaryNode(string op, AstNode e) { _op = op; _e = e; }
        public override Value Eval(EvalContext ctx)
        {
            var v = _e.Eval(ctx);
            if (_op == "-") return Value.Of(-v.AsNum());
            return v;
        }
    }

    public class BinaryNode : AstNode
    {
        private readonly AstNode _l, _r;
        private readonly string _op;
        public BinaryNode(AstNode l, string op, AstNode r) { _l = l; _op = op; _r = r; }

        public override Value Eval(EvalContext ctx)
        {
            // 短路逻辑
            if (_op == "&") return Value.Of(_l.Eval(ctx).AsBool() && _r.Eval(ctx).AsBool());
            if (_op == "|") return Value.Of(_l.Eval(ctx).AsBool() || _r.Eval(ctx).AsBool());

            var lv = _l.Eval(ctx);
            var rv = _r.Eval(ctx);
            return ApplyBinary(_op, lv, rv);
        }

        private static Value ApplyBinary(string op, Value lv, Value rv)
        {
            switch (op)
            {
                case "=":
                    if (lv.Type == ValueType.Num && rv.Type == ValueType.Num)
                        return Value.Of(lv.Num == rv.Num);
                    return Value.Of(lv.AsStr() == rv.AsStr());
                case "!=":
                    if (lv.Type == ValueType.Num && rv.Type == ValueType.Num)
                        return Value.Of(lv.Num != rv.Num);
                    return Value.Of(lv.AsStr() != rv.AsStr());
                case ">":
                case ">=":
                case "<":
                case "<=":
                    {
                        double a = lv.AsNum(), b = rv.AsNum();
                        return op switch
                        {
                            ">" => Value.Of(a > b),
                            ">=" => Value.Of(a >= b),
                            "<" => Value.Of(a < b),
                            _ => Value.Of(a <= b)
                        };
                    }
                case "~=":
                    {
                        try { return Value.Of(System.Text.RegularExpressions.Regex.IsMatch(lv.AsStr(), rv.AsStr())); }
                        catch { return Value.Of(false); }
                    }
                case "+":
                    return Value.Of(lv.AsStr() + rv.AsStr());
                case "-":
                    return Value.Of(lv.AsNum() - rv.AsNum());
                case "*":
                    return Value.Of(lv.AsNum() * rv.AsNum());
                case "/":
                    {
                        double d = rv.AsNum();
                        return Value.Of(d == 0 ? 0 : lv.AsNum() / d);
                    }
                default:
                    return Value.Null();
            }
        }
    }
}
