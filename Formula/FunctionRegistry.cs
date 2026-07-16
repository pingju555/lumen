using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Lumen.Formula
{
    /// <summary>
    /// 公式函数注册表：v1 必做 19 函数（A 档 11 + B 档 8）。
    /// 各函数委托给 ctx.Provider / ctx.Gv；纯计算直接求值。
    /// 详见 docs/project/公式函数参考.md §4
    /// </summary>
    public static class FunctionRegistry
    {
        private delegate Value Fn(Value[] a, EvalContext ctx);
        private static readonly System.Collections.Generic.Dictionary<string, Fn> _fns =
            new(StringComparer.OrdinalIgnoreCase);

        static FunctionRegistry()
        {
            // A 档 11
            Register("df", Df);
            Register("tf", Tf);
            Register("si", Si);
            Register("bi", Bi);
            Register("gv", Gv);
            Register("if", IfFn);
            Register("tc", Tc);
            Register("mi", Mi);   // P4-2 媒体信息
            Register("mu", Mu);   // P4-2 媒体控制
            Register("ai", Ai);   // P4-2 应用列表
            Register("an", An);   // P4-2 启动应用
            // B 档 8
            Register("ts", Ts);
            Register("tu", (a, c) => Value.Of(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")));
            Register("tz", (a, c) => Value.Of((double)TimeZoneInfo.Local.BaseUtcOffset.TotalHours));
            Register("uc", (a, c) => Value.Of(a.Length > 0 ? a[0].AsStr().ToUpperInvariant() : ""));
            Register("re", Re);
            Register("fl", Fl);
            Register("dp", (a, c) => Value.Of(a.Length > 0 ? a[0].AsNum() * c.Provider.Dpi() : 0));
            Register("rng", Rng);
        }

        public static void Register(string name, Func<Value[], EvalContext, Value> fn)
            => _fns[name] = (a, c) => fn(a, c);

        public static Value Call(string name, Value[] args, EvalContext ctx)
        {
            if (_fns.TryGetValue(name, out var fn))
            {
                try { return fn(args ?? Array.Empty<Value>(), ctx) ?? Value.Null(); }
                catch { return Value.Null(); }
            }
            return Value.Null();
        }

        // ---------- A 档 ----------

        private static Value Df(Value[] a, EvalContext ctx)
        {
            string fmt = a.Length > 0 ? a[0].AsStr() : "HH:mm:ss";
            var when = ctx.Provider.Now;
            return Value.Of(when.ToString(MapDf(fmt), CultureInfo.InvariantCulture));
        }

        private static Value Tf(Value[] a, EvalContext ctx)
        {
            double secs = a.Length > 0 ? a[0].AsNum() : 0;
            var ts = TimeSpan.FromSeconds(Math.Abs(secs));
            return Value.Of(ts.ToString(@"h\:mm\:ss"));
        }

        private static Value Si(Value[] a, EvalContext ctx)
        {
            string t = a.Length > 0 ? a[0].AsStr().ToLowerInvariant() : "";
            return t switch
            {
                "rwidth" => Value.Of(ctx.Provider.ScreenWidth()),
                "rheight" => Value.Of(ctx.Provider.ScreenHeight()),
                "density" => Value.Of(ctx.Provider.Dpi()),
                "dark" => Value.Of(ctx.Provider.IsDark() ? 1 : 0),
                // --- P4 实时系统指标 ---
                "cpu" => Value.Of(ctx.Provider.CpuPercent()),
                "mem" => Value.Of(ctx.Provider.MemPercent()),
                "memused" => Value.Of(ctx.Provider.MemUsedGb()),
                "memtotal" => Value.Of(ctx.Provider.MemTotalGb()),
                "diskfree" => Value.Of(ctx.Provider.DiskFreeGb()),
                "disktotal" => Value.Of(ctx.Provider.DiskTotalGb()),
                "diskp" => Value.Of(ctx.Provider.DiskPercent()),
                "netup" => Value.Of(ctx.Provider.NetUp()),
                "netdown" => Value.Of(ctx.Provider.NetDown()),
                _ => Value.Of("")
            };
        }

        private static Value Bi(Value[] a, EvalContext ctx)
        {
            string t = a.Length > 0 ? a[0].AsStr().ToLowerInvariant() : "level";
            if (t == "level")
            {
                int lvl = ctx.Provider.BatteryLevel();
                return Value.Of(lvl < 0 ? 100 : lvl);
            }
            if (t == "plugged" || t == "charging")
                return Value.Of(ctx.Provider.BatteryPlugged());
            return Value.Of("");
        }

        private static Value Gv(Value[] a, EvalContext ctx)
        {
            string name = a.Length > 0 ? a[0].AsStr() : "";
            var v = ctx.ResolveGv(name);
            if (v.Type == ValueType.Null)
                return a.Length > 1 ? a[1] : Value.Null();
            return v;
        }

        // ---------- P4-2 媒体 / 应用 ----------

        private static Value Mi(Value[] a, EvalContext ctx)
        {
            string t = a.Length > 0 ? a[0].AsStr().ToLowerInvariant() : "title";
            return t switch
            {
                "title" => Value.Of(ctx.Provider.MediaTitle()),
                "artist" => Value.Of(ctx.Provider.MediaArtist()),
                "album" => Value.Of(ctx.Provider.MediaAlbum()),
                "app" => Value.Of(ctx.Provider.MediaApp()),
                "playing" => Value.Of(ctx.Provider.MediaPlaying()),
                "pos" => Value.Of(ctx.Provider.MediaPosition()),
                "dur" or "duration" => Value.Of(ctx.Provider.MediaDuration()),
                "avail" or "available" => Value.Of(ctx.Provider.MediaAvailable()),
                _ => Value.Of("")
            };
        }

        private static Value Mu(Value[] a, EvalContext ctx)
        {
            string cmd = a.Length > 0 ? a[0].AsStr().ToLowerInvariant() : "play";
            ctx.Provider.MediaControl(cmd);
            return Value.Of(1);
        }

        private static Value Ai(Value[] a, EvalContext ctx)
        {
            if (a.Length == 0) return Value.Of((double)ctx.Provider.AppCount());
            int idx = (int)Math.Round(a[0].AsNum());
            return Value.Of(ctx.Provider.AppName(idx - 1)); // 1-based 友好
        }

        private static Value An(Value[] a, EvalContext ctx)
        {
            int idx = a.Length > 0 ? (int)Math.Round(a[0].AsNum()) : 0;
            return Value.Of(ctx.Provider.AppLaunch(idx - 1));
        }

        private static Value IfFn(Value[] a, EvalContext ctx)
        {
            if (a.Length == 0) return Value.Null();
            return a[0].AsBool()
                ? (a.Length > 1 ? a[1] : Value.Of(""))
                : (a.Length > 2 ? a[2] : Value.Of(""));
        }

        private static Value Tc(Value[] a, EvalContext ctx)
        {
            if (a.Length < 2) return Value.Of("");
            string cmd = a[0].AsStr().ToLowerInvariant();
            string text = a[1].AsStr();
            switch (cmd)
            {
                case "cut":
                    {
                        int n = a.Length > 2 ? (int)Math.Round(a[2].AsNum()) : 0;
                        return Value.Of(n > 0 && text.Length > n ? text.Substring(0, n) : text);
                    }
                case "ell":
                    {
                        int n = a.Length > 2 ? (int)Math.Round(a[2].AsNum()) : 0;
                        return Value.Of(n > 0 && text.Length > n ? text.Substring(0, Math.Max(0, n - 1)) + "…" : text);
                    }
                case "reg":
                    {
                        string pat = a.Length > 2 ? a[2].AsStr() : "";
                        string repl = a.Length > 3 ? a[3].AsStr() : "";
                        try { return Value.Of(Regex.Replace(text, pat, repl)); }
                        catch { return Value.Of(text); }
                    }
                case "up": return Value.Of(text.ToUpperInvariant());
                case "low": return Value.Of(text.ToLowerInvariant());
                case "cap": return Value.Of(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text));
                default: return Value.Of(text);
            }
        }

        // ---------- B 档 ----------

        private static Value Ts(Value[] a, EvalContext ctx)
        {
            if (a.Length == 0)
            {
                long epoch = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                return Value.Of((double)epoch);
            }
            return Value.Of(ctx.Provider.Now.ToString(MapDf(a[0].AsStr()), CultureInfo.InvariantCulture));
        }

        private static Value Re(Value[] a, EvalContext ctx)
        {
            if (a.Length < 2) return Value.Of("");
            string text = a[0].AsStr();
            string pat = a[1].AsStr();
            string repl = a.Length > 2 ? a[2].AsStr() : "";
            try { return Value.Of(Regex.Replace(text, pat, repl)); }
            catch { return Value.Of(text); }
        }

        private static Value Fl(Value[] a, EvalContext ctx)
        {
            if (a.Length < 2) return Value.Null();
            int idx = (int)Math.Round(a[0].AsNum());
            if (idx >= 1 && idx < a.Length) return a[idx];
            return Value.Null();
        }

        private static readonly Random _rng = new();
        private static Value Rng(Value[] a, EvalContext ctx)
        {
            if (a.Length == 0) return Value.Of(_rng.NextDouble());
            if (a.Length == 1) return Value.Of(_rng.NextDouble() * a[0].AsNum());
            double min = a[0].AsNum(), max = a[1].AsNum();
            return Value.Of(min + _rng.NextDouble() * (max - min));
        }

        // ---------- KLWP 日期格式 → .NET 自定义格式 ----------
        private static string MapDf(string fmt)
        {
            if (string.IsNullOrEmpty(fmt)) return "HH:mm:ss";
            var sb = new StringBuilder();
            int i = 0, n = fmt.Length;
            while (i < n)
            {
                if (i + 3 < n)
                {
                    var four = fmt.Substring(i, 4);
                    if (four == "MMMM") { sb.Append("MMMM"); i += 4; continue; }
                    if (four == "EEEE") { sb.Append("dddd"); i += 4; continue; }
                }
                if (i + 2 < n)
                {
                    var three = fmt.Substring(i, 3);
                    if (three == "MMM") { sb.Append("MMM"); i += 3; continue; }
                    if (three == "EEE") { sb.Append("ddd"); i += 3; continue; }
                }
                var two = i + 1 < n ? fmt.Substring(i, 2) : "";
                switch (two)
                {
                    case "hh": sb.Append("hh"); i += 2; continue;
                    case "kk": sb.Append("HH"); i += 2; continue;
                    case "mm": sb.Append("mm"); i += 2; continue;
                    case "ss": sb.Append("ss"); i += 2; continue;
                    case "dd": sb.Append("dd"); i += 2; continue;
                    case "MM": sb.Append("MM"); i += 2; continue;
                    case "yy": sb.Append("yy"); i += 2; continue;
                }
                char one = fmt[i];
                switch (one)
                {
                    case 'h': sb.Append('h'); break;
                    case 'k': sb.Append('H'); break;
                    case 'm': sb.Append('m'); break;
                    case 's': sb.Append('s'); break;
                    case 'a': sb.Append("tt"); break;
                    case 'd': sb.Append('d'); break;
                    case 'M': sb.Append('M'); break;
                    case 'y': sb.Append('y'); break;
                    case 'e': sb.Append("ddd"); break;
                    default: sb.Append(one); break;
                }
                i++;
            }
            return sb.ToString();
        }
    }
}
