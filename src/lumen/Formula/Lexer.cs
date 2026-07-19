using System;
using System.Collections.Generic;
using System.Text;

namespace Lumen.Formula
{
    public enum TokType { Num, Str, Color, Ident, LParen, RParen, Comma, Op, Eof }

    public class Token
    {
        public TokType Type;
        public string Text;
        public int Pos;
        public Token(TokType t, string text, int pos) { Type = t; Text = text; Pos = pos; }
    }

    /// <summary>词法分析：切分数字/字符串/颜色/#Ident/括号/逗号/运算符。</summary>
    public static class Lexer
    {
        public static List<Token> Tokenize(string src)
        {
            var toks = new List<Token>();
            if (src == null) { toks.Add(new Token(TokType.Eof, "", 0)); return toks; }
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '"' || c == '\'')
                {
                    char q = c; i++;
                    var sb = new StringBuilder();
                    while (i < n && src[i] != q)
                    {
                        if (src[i] == '\\' && i + 1 < n) { sb.Append(src[i + 1]); i += 2; }
                        else { sb.Append(src[i]); i++; }
                    }
                    i++;
                    toks.Add(new Token(TokType.Str, sb.ToString(), i));
                    continue;
                }

                if (c == '#')
                {
                    int start = i; i++;
                    while (i < n && (char.IsLetterOrDigit(src[i]) || src[i] == '_')) i++;
                    toks.Add(new Token(TokType.Color, src.Substring(start, i - start), start));
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(src[i + 1])))
                {
                    int start = i;
                    while (i < n && (char.IsDigit(src[i]) || src[i] == '.')) i++;
                    toks.Add(new Token(TokType.Num, src.Substring(start, i - start), start));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < n && (char.IsLetterOrDigit(src[i]) || src[i] == '_')) i++;
                    toks.Add(new Token(TokType.Ident, src.Substring(start, i - start), start));
                    continue;
                }

                if (c == '(') { toks.Add(new Token(TokType.LParen, "(", i)); i++; continue; }
                if (c == ')') { toks.Add(new Token(TokType.RParen, ")", i)); i++; continue; }
                if (c == ',') { toks.Add(new Token(TokType.Comma, ",", i)); i++; continue; }

                // 运算符：优先双字符 >= <= != ~=
                string op = null;
                if (i + 1 < n)
                {
                    string two = src.Substring(i, 2);
                    if (two == ">=" || two == "<=" || two == "!=" || two == "~=") op = two;
                }
                if (op == null) op = c.ToString();
                toks.Add(new Token(TokType.Op, op, i));
                i += op.Length;
            }
            toks.Add(new Token(TokType.Eof, "", i));
            return toks;
        }
    }
}
