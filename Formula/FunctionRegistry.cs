using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lumen.Atoms;
using Lumen.Globals;

namespace Lumen.Formula
{
    /// <summary>
    /// 公式函数注册表。实现类：df tf ts tu tz si bi dp gv if tc uc re fl rng mi mu ce bp wg。
    /// 已删除：cg（并入 gv）、ai/an（原误标，按商榷整个不加）。
    /// 变量统一为 gv(scope, name[, N])：scope=0 全局、""/"self" 就近组件、具体 8 位 ID 取该组件变量；N=List 索引(1基)。
    /// 详见 docs/project/公式函数参考.md
    /// </summary>
    public static class FunctionRegistry
    {
        private delegate Value Fn(Value[] a, EvalContext ctx);
        private static readonly Dictionary<string, Fn> _fns =
            new(StringComparer.OrdinalIgnoreCase);

        static FunctionRegistry()
        {
            // ---------- 时间 ----------
            Register("df", Df);
            Register("tf", Tf);
            Register("ts", Ts);
            Register("tu", (a, c) => Value.Of(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")));
            Register("tz", (a, c) => Value.Of((double)TimeZoneInfo.Local.BaseUtcOffset.TotalHours));
            // ---------- 系统 ----------
            Register("si", Si);
            Register("bi", Bi);
            Register("dp", (a, c) => Value.Of(a.Length > 0 ? a[0].AsNum() * c.Provider.Dpi() : 0));
            // ---------- 变量 ----------
            Register("gv", Gv);
            // ---------- 逻辑 / 文本 ----------
            Register("if", IfFn);
            Register("tc", Tc);
            Register("uc", (a, c) => Value.Of(a.Length > 0 ? a[0].AsStr().ToUpperInvariant() : ""));
            Register("re", Re);
            Register("fl", Fl);
            Register("rng", Rng);
            // ---------- 媒体 / 颜色 / 外部数据 ----------
            Register("mi", Mi);
            Register("mu", Mu);   // 数学派发器（媒体控制已移至动作系统）
            Register("ce", Ce);   // 颜色运算
            Register("bp", Bp);   // 调色板（算法待议，默认实现）
            Register("wg", Wg);   // RSS 外部数据
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

        // ---------- 时间 ----------

        private static Value Df(Value[] a, EvalContext ctx)
        {
            if (a.Length == 0) return Value.Of("");   // 无参数返回空
            string fmt = a[0].AsStr();
            DateTime when = ctx.Provider.Now;
            // 第二参 [date]：Unix 秒 或 日期字符串 → 格式化该时刻（最小版）
            if (a.Length > 1 && !string.IsNullOrEmpty(a[1].AsStr()))
            {
                var ds = a[1].AsStr();
                if (double.TryParse(ds, NumberStyles.Any, CultureInfo.InvariantCulture, out var secs))
                    when = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(secs).ToLocalTime();
                else if (DateTime.TryParse(ds, out var dt)) when = dt;
            }
            var ci = a.Length > 2 ? LocaleCi(a[2].AsStr()) : CultureInfo.InvariantCulture;
            return Value.Of(FormatKustom(fmt, when, ci));
        }

        private static Value Tf(Value[] a, EvalContext ctx)
        {
            double secs = a.Length > 0 ? a[0].AsNum() : 0;
            string fmt = a.Length > 1 ? a[1].AsStr() : "";   // 可选 format：h:mm:ss / mm:ss / s
            var ts = TimeSpan.FromSeconds(Math.Abs(secs));    // 保留负号由调用方拼接
            string body = string.IsNullOrEmpty(fmt) ? ts.ToString(@"h\:mm\:ss") : FormatDuration(ts, fmt);
            return Value.Of(secs < 0 ? "-" + body : body);    // 保留负号（倒计时）
        }

        private static string FormatDuration(TimeSpan ts, string fmt)
        {
            // 极简：支持 "mm:ss" / "h:mm:ss" / "s"
            if (fmt == "s") return ((int)ts.TotalSeconds).ToString();
            int total = (int)ts.TotalSeconds;
            int h = total / 3600, m = (total % 3600) / 60, s = total % 60;
            if (fmt.Contains("h")) return $"{h}:{m:D2}:{s:D2}";
            return $"{m}:{s:D2}";
        }

        // ---------- 系统指标 ----------

        private static Value Si(Value[] a, EvalContext ctx)
        {
            string t = a.Length > 0 ? a[0].AsStr().ToLowerInvariant() : "";
            return t switch
            {
                "rwidth" => Value.Of(ctx.Provider.ScreenWidth()),
                "rheight" => Value.Of(ctx.Provider.ScreenHeight()),
                "density" => Value.Of(ctx.Provider.Dpi()),
                "dark" => Value.Of(ctx.Provider.IsDark() ? 1 : 0),
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
                return Value.Of(lvl < 0 ? 100 : lvl);   // 无电池回退 100
            }
            if (t == "plugged" || t == "charging")
                return Value.Of(ctx.Provider.BatteryPlugged());
            return Value.Of("");
        }

        // ---------- 变量：gv 统一化 ----------

        private static Value Gv(Value[] a, EvalContext ctx)
        {
            if (a.Length == 0) return Value.Null();
            // 兼容单参 gv(name) → 全局
            string scope, name; int idx = 0;
            if (a.Length == 1) { scope = "0"; name = a[0].AsStr(); }
            else { scope = a[0].AsStr(); name = a[1].AsStr(); if (a.Length > 2) idx = (int)Math.Round(a[2].AsNum()); }

            TypedValue tv = null;
            string sc = scope.Trim().ToLowerInvariant();
            if (sc == "0")
            {
                tv = ctx.Gv.Get(name);
            }
            else if (sc == "" || sc == "self")
            {
                var r = ctx?.NearestResolver();
                if (r is ComponentVarStore cvs) tv = cvs.ResolveTyped(name);
            }
            else
            {
                if (ComponentAtom.TryGetByVid(scope, out var comp)) tv = comp.Vars.ResolveTyped(name);
            }

            if (tv == null) return Value.Null();
            if (tv.Type == GvType.List && idx > 0)
            {
                var arr = (tv.Raw as string ?? "").Split('|');
                int i = idx - 1;
                if (i < 0 || i >= arr.Length) i = 0;
                return Value.Of(arr[i]);
            }
            return tv.ToValue();
        }

        // ---------- 媒体信息 ----------

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
                "pos" or "position" => Value.Of(ctx.Provider.MediaPosition()),
                "dur" or "duration" => Value.Of(ctx.Provider.MediaDuration()),
                "avail" or "available" => Value.Of(ctx.Provider.MediaAvailable()),
                "state" => Value.Of(ctx.Provider.MediaPlaying() ? "playing" : (ctx.Provider.MediaAvailable() ? "paused" : "stopped")),
                // cover：SMTC 媒体封面主色（dominant 等），无封面返回空串
                "cover" => CoverValue(a, ctx),
                _ => Value.Of("")
            };
        }

        /// <summary>mi(cover[,type])：默认返回封面图片文件路径（供 Image 原子作源）；
        /// 指定 type(dominant/vibrant/muted/light/dark) 时返回该风格主色 #AARRGGBB。无封面返回空串。</summary>
        private static Value CoverValue(Value[] a, EvalContext ctx)
        {
            string type = a.Length > 1 ? a[1].AsStr().ToLowerInvariant() : "";
            if (string.IsNullOrEmpty(type))
            {
                var path = ctx.Provider.MediaCoverImage();
                return Value.Of(string.IsNullOrEmpty(path) ? "" : path);
            }
            var pal = ctx.Provider.MediaCoverPalette();
            if (pal == null || pal.Count == 0) return Value.Of("");
            if (!pal.TryGetValue(type, out uint c)) c = pal["dominant"];
            return Value.Of("#" + c.ToString("X8"));
        }

        // ---------- 数学派发器 mu ----------

        private static Value Mu(Value[] a, EvalContext ctx)
        {
            string op = a.Length > 0 ? a[0].AsStr().ToLowerInvariant() : "";
            if ((op == "pi" || op == "e") && a.Length == 1)
                return op == "pi" ? Value.Of(Math.PI) : Value.Of(Math.E);
            if (a.Length < 2) return Value.Null();   // 未知子函数挂起返回 Null
            double x = a[1].AsNum();
            double y = a.Length > 2 ? a[2].AsNum() : 0;
            double z = a.Length > 3 ? a[3].AsNum() : 0;
            switch (op)
            {
                case "round": return Value.Of(Math.Round(x));
                case "floor": return Value.Of(Math.Floor(x));
                case "ceil": return Value.Of(Math.Ceiling(x));
                case "abs": return Value.Of(Math.Abs(x));
                case "sqrt": return Value.Of(Math.Sqrt(x));
                case "exp": return Value.Of(Math.Exp(x));
                case "log": return Value.Of(Math.Log(x));
                case "log10": return Value.Of(Math.Log10(x));
                case "sin": return Value.Of(Math.Sin(x));
                case "cos": return Value.Of(Math.Cos(x));
                case "tan": return Value.Of(Math.Tan(x));
                case "asin": return Value.Of(Math.Asin(x));
                case "acos": return Value.Of(Math.Acos(x));
                case "atan": return Value.Of(Math.Atan(x));
                case "sign": return Value.Of(Math.Sign(x));
                case "trunc": return Value.Of(Math.Truncate(x));
                case "sind": return Value.Of(Math.Sin(x * Math.PI / 180));
                case "cosd": return Value.Of(Math.Cos(x * Math.PI / 180));
                case "tand": return Value.Of(Math.Tan(x * Math.PI / 180));
                case "pow": return Value.Of(Math.Pow(x, y));
                case "mod": return Value.Of(x % (y == 0 ? 1 : y));
                case "atan2": return Value.Of(Math.Atan2(x, y));
                case "hypot": return Value.Of(Math.Sqrt(x * x + y * y));
                case "lerp": return Value.Of(x + (y - x) * z);                 // lerp(a,b,t)
                case "clamp": return Value.Of(Math.Min(Math.Max(x, y), z));    // clamp(v,min,max)
                case "dist":                                                        // dist(x1,y1[,x2,y2])
                    double x2 = a.Length > 3 ? a[3].AsNum() : 0;
                    double y2 = a.Length > 4 ? a[4].AsNum() : 0;
                    return Value.Of(Math.Sqrt((x - x2) * (x - x2) + (y - y2) * (y - y2)));
                case "min": return Value.Of(Math.Min(x, y));
                case "max": return Value.Of(Math.Max(x, y));
                default: return Value.Null();                                     // 未知子函数挂起
            }
        }

        // ---------- 颜色运算 ce ----------

        private static Value Ce(Value[] a, EvalContext ctx)
        {
            string op = a.Length > 0 ? a[0].AsStr().ToLowerInvariant() : "";
            string col = a.Length > 1 ? a[1].AsStr() : "#000000";
            double amt = a.Length > 2 ? a[2].AsNum() : 0;   // -100 ~ +100
            if (!ParseArgb(col, out uint argb)) return Value.Of(col);
            ArgbToHsl(argb, out double h, out double s, out double l, out double alpha);
            switch (op)
            {
                case "lum": l = Clamp01((amt + 100) / 200); break;                       // 亮度
                case "alpha": alpha = Clamp01((amt + 100) / 200); break;                 // 透明度
                case "hue": h = (h + amt * 3.6 + 360) % 360; break;                      // 色相 ±360
                case "sat": s = Clamp01(s + amt / 100); break;                           // 饱和度
                case "red": { double r = 0, g = 0, b = 0; ArgbToRgb(argb, out r, out g, out b); r = Clamp01(r + amt / 100); argb = RgbToArgb(r, g, b, alpha); break; }
                case "green": { double r = 0, g = 0, b = 0; ArgbToRgb(argb, out r, out g, out b); g = Clamp01(g + amt / 100); argb = RgbToArgb(r, g, b, alpha); break; }
                case "blue": { double r = 0, g = 0, b = 0; ArgbToRgb(argb, out r, out g, out b); b = Clamp01(b + amt / 100); argb = RgbToArgb(r, g, b, alpha); break; }
                case "invert":                                                          // 反相（无参也生效）
                    { double r = 0, g = 0, b = 0; ArgbToRgb(argb, out r, out g, out b); argb = RgbToArgb(1 - r, 1 - g, 1 - b, alpha); break; }
                case "contrast":
                    { double r = 0, g = 0, b = 0; ArgbToRgb(argb, out r, out g, out b);
                      double f = (amt + 100) / 100; r = Clamp01(0.5 + (r - 0.5) * f); g = Clamp01(0.5 + (g - 0.5) * f); b = Clamp01(0.5 + (b - 0.5) * f);
                      argb = RgbToArgb(r, g, b, alpha); break; }
                case "blend":                                                          // minimal：与第三参颜色按 amt 混合
                    { uint c2 = 0xFF000000; if (a.Length > 3) ParseArgb(a[3].AsStr(), out c2); uint mixed = Blend(argb, c2, (amt + 100) / 200); argb = mixed; break; }
                default: return Value.Of(col);
            }
            return Value.Of("#" + argb.ToString("X8"));
        }

        // ---------- 调色板 bp（算法待议，默认实现） ----------

        private static Value Bp(Value[] a, EvalContext ctx)
        {
            string type = a.Length > 0 ? a[0].AsStr().ToLowerInvariant() : "dominant";
            string source = a.Length > 1 ? a[1].AsStr().ToLowerInvariant() : "cover";
            uint baseColor;
            if (source == "cover")
            {
                // cover：经 SMTC 缩略图 + 中位切分得到的真实调色板（已在后台轮询中缓存）
                var pal = ctx.Provider.MediaCoverPalette();
                if (pal != null && pal.Count > 0)
                {
                    if (!pal.TryGetValue(type, out uint c)) c = pal["dominant"];
                    return Value.Of("#" + c.ToString("X8"));
                }
                baseColor = 0;
            }
            else if (!ParseArgb(source, out baseColor))
            {
                // source 非 ARGB：当作图片路径/URL，用中位切分提取调色板（自写，无第三方依赖）
                var pal = PaletteExtractor.Extract(source);
                if (pal.Count > 0)
                {
                    if (!pal.TryGetValue(type, out uint c)) c = pal["dominant"];
                    return Value.Of("#" + c.ToString("X8"));
                }
                return Value.Of("#FF000000");
            }
            if (baseColor == 0) return Value.Of("#FF000000");
            // 单色来源：HSL 风格化（原有逻辑）
            ArgbToHsl(baseColor, out double h, out double s, out double l, out double al);
            return Value.Of("#" + HslToArgb(h, DeriveSat(type, s), DeriveLum(type, l), al).ToString("X8"));
        }

        private static double DeriveSat(string type, double s) => type switch
        {
            "vibrant" => Clamp01(s + 0.25),
            "muted" => Clamp01(s - 0.3),
            _ => s
        };
        private static double DeriveLum(string type, double l) => type switch
        {
            "light" => Clamp01(l + 0.3),
            "dark" => Clamp01(l - 0.3),
            "vibrant" => Clamp01(0.5),
            "muted" => Clamp01(0.45),
            _ => l
        };

        // ---------- RSS 外部数据 wg ----------

        private static Value Wg(Value[] a, EvalContext ctx)
        {
            if (a.Length == 0) return Value.Of("");
            string url = a[0].AsStr();
            int n = a.Length > 1 ? (int)Math.Round(a[1].AsNum()) : 1;
            string field = a.Length > 2 ? a[2].AsStr().ToLowerInvariant() : "title";
            var items = ctx.Provider.RssFetch(url);
            if (items == null || items.Count == 0) return Value.Of("");
            int i = n - 1; if (i < 0 || i >= items.Count) i = 0;
            var it = items[i];
            return field switch
            {
                "link" => Value.Of(it.Link),
                "desc" or "description" => Value.Of(it.Desc),
                _ => Value.Of(it.Title)
            };
        }

        // ---------- 逻辑 / 文本 ----------

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
                    { int n = a.Length > 2 ? (int)Math.Round(a[2].AsNum()) : 0; return Value.Of(n > 0 && text.Length > n ? text.Substring(0, n) : text); }
                case "ell":
                    { int n = a.Length > 2 ? (int)Math.Round(a[2].AsNum()) : 0; return Value.Of(n > 0 && text.Length > n ? text.Substring(0, Math.Max(0, n - 1)) + "…" : text); }
                case "reg":
                    { string pat = a.Length > 2 ? a[2].AsStr() : ""; string repl = a.Length > 3 ? a[3].AsStr() : ""; try { return Value.Of(Regex.Replace(text, pat, repl)); } catch { return Value.Of(text); } }
                case "up": return Value.Of(text.ToUpperInvariant());
                case "low": return Value.Of(text.ToLowerInvariant());
                case "cap": return Value.Of(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text));
                case "ord": return Value.Of(Ordinal(text));
                case "n2w": return Value.Of(NumberToWords(text));
                case "lpad":
                    { int n = a.Length > 2 ? (int)Math.Round(a[2].AsNum()) : 0; char pad = a.Length > 3 && a[3].AsStr().Length > 0 ? a[3].AsStr()[0] : '0'; return Value.Of(n > 0 ? text.PadLeft(n, pad) : text); }
                default: return Value.Of(text);
            }
        }

        // ---------- 杂项 ----------

        private static Value Ts(Value[] a, EvalContext ctx)
        {
            if (a.Length == 0)
            {
                long epoch = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                return Value.Of((double)epoch);
            }
            return Value.Of(ctx.Provider.Now.ToString(FormatKustom(a[0].AsStr(), ctx.Provider.Now, CultureInfo.InvariantCulture)));
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

        // ---------- 颜色 / 文本 辅助 ----------

        private static bool ParseArgb(string s, out uint argb)
        {
            argb = 0xFF000000;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var h = s.Trim().TrimStart('#');
            if (h.Length == 6) h = "FF" + h;
            if (h.Length == 8 && uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out argb)) return true;
            return false;
        }

        private static void ArgbToRgb(uint argb, out double r, out double g, out double b)
        {
            r = ((argb >> 16) & 0xFF) / 255.0;
            g = ((argb >> 8) & 0xFF) / 255.0;
            b = (argb & 0xFF) / 255.0;
        }
        private static uint RgbToArgb(double r, double g, double b, double a)
        {
            byte rr = (byte)Math.Round(Clamp01(r) * 255);
            byte gg = (byte)Math.Round(Clamp01(g) * 255);
            byte bb = (byte)Math.Round(Clamp01(b) * 255);
            byte aa = (byte)Math.Round(Clamp01(a) * 255);
            return (uint)(aa << 24 | rr << 16 | gg << 8 | bb);
        }
        private static void ArgbToHsl(uint argb, out double h, out double s, out double l, out double a)
        {
            ArgbToRgb(argb, out double r, out double g, out double b);
            a = ((argb >> 24) & 0xFF) / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            double d = max - min;
            if (d < 1e-9) { h = 0; s = 0; return; }
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? (g - b) / d + (g < b ? 6 : 0)
              : max == g ? (b - r) / d + 2
              : (r - g) / d + 4;
            h *= 60;
        }
        private static uint HslToArgb(double h, double s, double l, double a)
        {
            h = (h % 360 + 360) % 360; s = Clamp01(s); l = Clamp01(l); a = Clamp01(a);
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return RgbToArgb(r + m, g + m, b + m, a);
        }
        private static uint Blend(uint c1, uint c2, double t)
        {
            t = Clamp01(t);
            double r1 = 0, g1 = 0, b1 = 0, r2 = 0, g2 = 0, b2 = 0;
            ArgbToRgb(c1, out r1, out g1, out b1); ArgbToRgb(c2, out r2, out g2, out b2);
            return RgbToArgb(r1 + (r2 - r1) * t, g1 + (g2 - g1) * t, b1 + (b2 - b1) * t, 1);
        }
        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        private static string Ordinal(string text)
        {
            if (!int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out int n)) return text;
            if (n <= 0) return text;
            string suf = (n % 100 >= 11 && n % 100 <= 13) ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return n + suf;
        }

        private static string NumberToWords(string text)
        {
            if (!int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out int n)) return text;
            if (n == 0) return "zero";
            if (n < 0) return "minus " + NumberToWords((-n).ToString());
            var units = new[] { "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
            var tens = new[] { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };
            string Words(int v)
            {
                if (v < 20) return units[v];
                if (v < 100) return tens[v / 10] + (v % 10 != 0 ? "-" + units[v % 10] : "");
                if (v < 1000) return units[v / 100] + " hundred" + (v % 100 != 0 ? " " + Words(v % 100) : "");
                if (v < 1000000) return Words(v / 1000) + " thousand" + (v % 1000 != 0 ? " " + Words(v % 1000) : "");
                return v.ToString();
            }
            return Words(n);
        }

        // ---------- KLWP 日期格式 → .NET ----------

        private static CultureInfo LocaleCi(string loc)
        {
            if (string.Equals(loc, "cn", StringComparison.OrdinalIgnoreCase)) return new CultureInfo("zh-CN");
            if (string.Equals(loc, "en", StringComparison.OrdinalIgnoreCase)) return new CultureInfo("en-GB");
            return CultureInfo.InvariantCulture;
        }

        private static string FormatKustom(string fmt, DateTime when, CultureInfo ci)
        {
            if (string.IsNullOrEmpty(fmt)) fmt = "HH:mm:ss";
            int week = IsoWeek(when);
            int doy = when.DayOfYear;
            int wim = WeekdayInMonth(when);
            string tz = TimeZoneInfo.Local.GetUtcOffset(when).Hours >= 0
                ? "+" + TimeZoneInfo.Local.GetUtcOffset(when).ToString("hh") + ":" + TimeZoneInfo.Local.GetUtcOffset(when).Minutes.ToString("00")
                : TimeZoneInfo.Local.GetUtcOffset(when).ToString("hh") + ":" + TimeZoneInfo.Local.GetUtcOffset(when).Minutes.ToString("00");

            var s = fmt;
            // 多字符 token → 占位符
            s = s.Replace("yyyy", "@Y4@").Replace("yy", "@Y2@");
            s = s.Replace("MMMM", "@MN@").Replace("MMM", "@M3@").Replace("MM", "@M2@");
            s = s.Replace("dd", "@D2@");
            s = s.Replace("EEE", "@E3@").Replace("EEEE", "@E4@");
            s = s.Replace("kk", "@K2@").Replace("HH", "@H2@").Replace("hh", "@h2@").Replace("mm", "@m2@").Replace("ss", "@s2@");
            // 单字母特殊量
            s = s.Replace("w", week.ToString("00"));
            s = s.Replace("D", doy.ToString());
            s = s.Replace("F", wim.ToString());
            s = s.Replace("G", "AD");
            s = s.Replace("z", tz);
            // 标准单字母
            s = s.Replace("M", "@M1@").Replace("d", "@d1@").Replace("y", "@y1@")
                  .Replace("h", "@h1@").Replace("k", "@k1@").Replace("m", "@m1@").Replace("s", "@s1@").Replace("a", "@a1@");
            s = s.Replace("e", (((int)when.DayOfWeek + 1) % 7 == 0 ? 7 : (int)when.DayOfWeek + 1).ToString());
            // 占位符 → .NET 格式
            string net = s
                .Replace("@Y4@", "yyyy").Replace("@Y2@", "yy")
                .Replace("@MN@", "MMMM").Replace("@M3@", "MMM").Replace("@M2@", "MM").Replace("@M1@", "M")
                .Replace("@D2@", "dd").Replace("@d1@", "d")
                .Replace("@E3@", "ddd").Replace("@E4@", "dddd")
                .Replace("@K2@", "HH").Replace("@H2@", "HH").Replace("@h2@", "hh").Replace("@h1@", "h").Replace("@k1@", "H")
                .Replace("@m2@", "mm").Replace("@m1@", "m")
                .Replace("@s2@", "ss").Replace("@s1@", "s")
                .Replace("@a1@", "tt");
            return when.ToString(net, ci);
        }

        private static int IsoWeek(DateTime dt)
        {
            var d = dt.Date;
            int day = (int)d.DayOfWeek; if (day == 0) day = 7;
            var thursday = d.AddDays(4 - day);
            var yearStart = new DateTime(thursday.Year, 1, 1);
            int week = (int)((thursday - yearStart).TotalDays / 7) + 1;
            return week;
        }

        private static int WeekdayInMonth(DateTime dt)
        {
            int day = dt.Day;
            int count = 0;
            for (int d = 1; d <= day; d += 7) count++;
            return count;
        }
    }
}
