using System;
using System.Globalization;

namespace Lumen.Formula
{
    /// <summary>公式求值结果类型（显式，避免隐式转换崩溃）。</summary>
    public enum ValueType { Null, Num, Str, Color, Bool }

    /// <summary>
    /// 公式值：统一承载 数字/字符串/颜色(#AARRGGBB)/布尔。宽松互转，错误即回退。
    /// 详见 docs/project/公式引擎设计.md §5
    /// </summary>
    public class Value
    {
        public ValueType Type { get; }
        public double Num { get; }
        public string Str { get; }
        public uint ColorArgb { get; } // #AARRGGBB
        public bool Bool { get; }

        private Value(ValueType type, double num, string str, uint color, bool boolv)
        {
            Type = type; Num = num; Str = str; ColorArgb = color; Bool = boolv;
        }

        public static Value Null() => new Value(ValueType.Null, 0, null, 0, false);
        public static Value Of(double n) => new Value(ValueType.Num, n, FormatNum(n), 0, n != 0);
        public static Value Of(string s) => new Value(ValueType.Str, 0, s ?? "", 0, !string.IsNullOrEmpty(s));
        public static Value Of(bool b) => new Value(ValueType.Bool, b ? 1 : 0, b ? "true" : "false", 0, b);
        public static Value OfColor(uint argb) => new Value(ValueType.Color, 0, null, argb, false);

        public static Value FromObject(object o)
        {
            if (o == null) return Null();
            if (o is Value v) return v;
            if (o is double d) return Of(d);
            if (o is int i) return Of((double)i);
            if (o is long l) return Of((double)l);
            if (o is bool b) return Of(b);
            if (o is string s) return Of(s);
            if (o is uint u) return OfColor(u);
            return Of(o.ToString());
        }

        private static string FormatNum(double n)
        {
            if (double.IsNaN(n)) return "NaN";
            if (double.IsInfinity(n)) return n > 0 ? "Infinity" : "-Infinity";
            if (n == Math.Floor(n) && Math.Abs(n) < 1e15)
                return ((long)n).ToString(CultureInfo.InvariantCulture);

            // 去浮点噪声：0.1+0.2 等二进制不可表示的尾数误差。
            // 从 1 位小数起，找第一个与真值偏差落在容差内的表示（最短即最干净）。
            double scale = Math.Max(1.0, Math.Abs(n));
            const double tol = 1e-12;
            for (int k = 1; k <= 15; k++)
            {
                double r = Math.Round(n, k);
                if (Math.Abs(r - n) <= tol * scale)
                    return r.ToString("F" + k, CultureInfo.InvariantCulture);
            }
            return n.ToString(CultureInfo.InvariantCulture);
        }

        public string AsStr()
        {
            switch (Type)
            {
                case ValueType.Null: return "";
                case ValueType.Num: return Str;
                case ValueType.Str: return Str;
                case ValueType.Color: return "#" + ColorArgb.ToString("X8");
                case ValueType.Bool: return Bool ? "true" : "false";
                default: return "";
            }
        }

        public double AsNum()
        {
            switch (Type)
            {
                case ValueType.Num: return Num;
                case ValueType.Str:
                    if (double.TryParse(Str, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
                    return 0;
                case ValueType.Bool: return Bool ? 1 : 0;
                default: return 0;
            }
        }

        public bool AsBool()
        {
            switch (Type)
            {
                case ValueType.Bool: return Bool;
                case ValueType.Num: return Num != 0;
                case ValueType.Str: return !string.IsNullOrEmpty(Str) && Str != "0" && Str != "false";
                case ValueType.Color: return ColorArgb != 0;
                default: return false;
            }
        }
    }
}
