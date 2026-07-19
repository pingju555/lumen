using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lumen.Formula
{
    /// <summary>递归下降解析器：expr → if/logic → add → mul → unary → primary。</summary>
    public static class Parser
    {
        private static List<Token> _toks;
        private static int _pos;

        public static AstNode Parse(List<Token> toks)
        {
            _toks = toks; _pos = 0;
            if (_toks == null || _toks.Count == 0) return LiteralNode.Null;
            var node = ParseExpr();
            return node ?? LiteralNode.Null;
        }

        private static Token Peek() => _toks[_pos];
        private static Token Next() => _toks[_pos++];
        private static bool Eat(TokType t)
        {
            if (Peek().Type == t) { _pos++; return true; }
            return false;
        }

        private static AstNode ParseExpr()
        {
            if (Peek().Type == TokType.Ident &&
                Peek().Text.Equals("if", StringComparison.OrdinalIgnoreCase))
                return ParseIf();
            return ParseLogic();
        }

        private static AstNode ParseIf()
        {
            Next(); // if
            Eat(TokType.LParen);
            var cond = ParseExpr();
            Eat(TokType.Comma);
            var then = ParseExpr();
            AstNode els = LiteralNode.Null;
            if (Eat(TokType.Comma)) els = ParseExpr();
            Eat(TokType.RParen);
            return new IfNode(cond, then, els);
        }

        private static AstNode ParseLogic()
        {
            var left = ParseCompare();
            while (Peek().Type == TokType.Op && (Peek().Text == "&" || Peek().Text == "|"))
            {
                var op = Next().Text;
                var right = ParseCompare();
                left = new BinaryNode(left, op, right);
            }
            return left;
        }

        // 比较层（expr → logic → compare → add → mul → unary → primary）。
        // 支持 = != > >= < <= ~=(正则)，对应 BinaryNode.ApplyBinary 已实现的分支。
        // 等号用单字符 '='（与 KLWP 一致）；'==' 不是合法 token（会被拆成两个 '='，应避免）。
        private static readonly HashSet<string> CompareOps = new() { ">", "<", ">=", "<=", "=", "!=", "~=" };

        private static AstNode ParseCompare()
        {
            var left = ParseAdd();
            while (Peek().Type == TokType.Op && CompareOps.Contains(Peek().Text))
            {
                var op = Next().Text;
                var right = ParseAdd();
                left = new BinaryNode(left, op, right);
            }
            return left;
        }

        private static AstNode ParseAdd()
        {
            var left = ParseMul();
            while (Peek().Type == TokType.Op && (Peek().Text == "+" || Peek().Text == "-"))
            {
                var op = Next().Text;
                var right = ParseMul();
                left = new BinaryNode(left, op, right);
            }
            return left;
        }

        private static AstNode ParseMul()
        {
            var left = ParseUnary();
            while (Peek().Type == TokType.Op && (Peek().Text == "*" || Peek().Text == "/"))
            {
                var op = Next().Text;
                var right = ParseUnary();
                left = new BinaryNode(left, op, right);
            }
            return left;
        }

        private static AstNode ParseUnary()
        {
            if (Peek().Type == TokType.Op && Peek().Text == "-")
            {
                Next();
                return new UnaryNode("-", ParseUnary());
            }
            return ParsePrimary();
        }

        private static AstNode ParsePrimary()
        {
            var t = Peek();
            if (t.Type == TokType.Num)
            {
                Next();
                return new LiteralNode(Value.Of(double.Parse(t.Text, CultureInfo.InvariantCulture)));
            }
            if (t.Type == TokType.Str) { Next(); return new LiteralNode(Value.Of(t.Text)); }
            if (t.Type == TokType.Color) { Next(); return new LiteralNode(ParseColor(t.Text)); }
            if (t.Type == TokType.Ident)
            {
                Next();
                if (Peek().Type == TokType.LParen)
                {
                    Next(); // (
                    var args = new List<AstNode>();
                    if (Peek().Type != TokType.RParen)
                    {
                        args.Add(ParseFuncArg());
                        while (Eat(TokType.Comma)) args.Add(ParseFuncArg());
                    }
                    Eat(TokType.RParen);
                    return new CallNode(t.Text, args);
                }
                return new GvVarNode(t.Text);
            }
            if (t.Type == TokType.LParen)
            {
                Next();
                var e = ParseExpr();
                Eat(TokType.RParen);
                return e;
            }
            Next(); // 跳过未知
            return LiteralNode.Null;
        }

        /// <summary>函数参数解析：标识符自动转为字符串字面量（而非 GvVarNode），
        /// 避免 bi(level) 中 level 被误解析为变量引用。</summary>
        private static AstNode ParseFuncArg()
        {
            var t = Peek();
            if (t.Type == TokType.Ident)
            {
                Next();
                return new LiteralNode(Value.Of(t.Text));
            }
            return ParseExpr();
        }

        private static Value ParseColor(string s)
        {
            string hex = s.Substring(1);
            if (hex.Length == 6)
                return Value.OfColor(0xFF000000 | uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            if (hex.Length == 8)
                return Value.OfColor(uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            if (hex.Length == 3)
            {
                uint r = uint.Parse(hex[0].ToString(), NumberStyles.HexNumber);
                uint g = uint.Parse(hex[1].ToString(), NumberStyles.HexNumber);
                uint b = uint.Parse(hex[2].ToString(), NumberStyles.HexNumber);
                return Value.OfColor(0xFF000000 | (r << 20) | (g << 12) | (b << 4));
            }
            return Value.Null();
        }
    }
}
