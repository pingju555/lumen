using System.Collections.Generic;
using Lumen.I18n;

namespace Lumen.Ui
{
    /// <summary>公式函数目录（供属性编辑器「插入函数」使用）。与 Formula/FunctionRegistry 的 14 函数一一对应。</summary>
    public class FunctionCatalog
    {
        public string Category;
        public string Name;
        public string Sig;
        public string Desc;
        public string Insert;
        /// <summary>内参命令标签（如 si 的 cpu/mem/net/disk 标签），点击自动填入公式。</summary>
        public string[] Params;

        public static readonly List<FunctionCatalog> All = new()
        {
            // ────── Date Format ──────
            new() { Category = "Date Format", Name = "df", Sig = "df(format[, date, locale])",
                    Desc = "Format current time or a given timestamp. Supports KLWP-style patterns like HH:mm, yyyy-MM-dd.",
                    Insert = "df(HH:mm:ss)", Params = new[] { "HH:mm", "HH:mm:ss", "yyyy-MM-dd", "MM/dd", "EEE", "MMMM d" } },

            // ────── Time Calc ──────
            new() { Category = "Time Calc", Name = "tf", Sig = "tf(seconds[, format])",
                    Desc = "Convert seconds to readable duration. Formats: h:mm:ss, mm:ss, s.",
                    Insert = "tf(3661)", Params = new[] { "3600", "3661", "86400", "h:mm:ss", "mm:ss", "s" } },
            new() { Category = "Time Calc", Name = "tu", Sig = "tu()",
                    Desc = "Returns current Unix timestamp as datetime string.",
                    Insert = "tu()" },

            // ────── Performance ──────
            new() { Category = "Performance", Name = "si", Sig = "si(metric)",
                    Desc = "System metrics: CPU%, memory, disk, network speed, DPI, dark mode.",
                    Insert = "si(cpu)", Params = new[] { "cpu", "mem", "memFree", "netUp", "netDown", "diskFree", "diskP", "density", "dark" } },

            // ────── Battery ──────
            new() { Category = "Battery", Name = "bi", Sig = "bi(field)",
                    Desc = "Battery status: charge level(%), charging state, plugged state.",
                    Insert = "bi(level)", Params = new[] { "level", "charging", "plugged", "time" } },

            // ────── Text Edit ──────
            new() { Category = "Text Edit", Name = "tc", Sig = "tc(cmd, text[, ...])",
                    Desc = "Text operations: cut, ellipsis, regex replace, case change, padding, ordinal, number-to-words.",
                    Insert = "tc(ell, text, 8)", Params = new[] { "ell(8)", "reg(pat,repl)", "up", "low", "cap", "lpad(8)", "ord", "n2w" } },

            // ────── Condition ──────
            new() { Category = "Condition", Name = "if", Sig = "if(cond, trueVal, falseVal)",
                    Desc = "If-then-else conditional. Nested conditions supported.",
                    Insert = "if(si(cpu)>50, busy, idle)" },

            // ────── Select ──────
            new() { Category = "Select", Name = "fl", Sig = "fl(index, val1, val2, ...)",
                    Desc = "Pick a value by 1-based index from a list.",
                    Insert = "fl(2, A, B, C)" },

            // ────── Math ──────
            new() { Category = "Math", Name = "mu", Sig = "mu(op, x[, y, z])",
                    Desc = "Math operations: arithmetic, trig, rounding, min/max, random(rnd).",
                    Insert = "mu(round, 3.14159, 2)", Params = new[] { "add", "sub", "mul", "div", "round", "sqrt", "sin", "cos", "tan", "pow", "min", "max", "avg", "rnd" } },

            // ────── Media ──────
            new() { Category = "Media", Name = "mi", Sig = "mi(field)",
                    Desc = "Now-playing metadata from SMTC: title, artist, album, position, duration.",
                    Insert = "mi(title)", Params = new[] { "title", "artist", "album", "app", "playing", "pos", "dur", "cover" } },

            // ────── Color ──────
            new() { Category = "Color", Name = "ce", Sig = "ce(op, color[, amount])",
                    Desc = "Color transformations: luminance, saturation, hue shift, invert, blend, contrast.",
                    Insert = "ce(lum, #3399FF, 20)", Params = new[] { "lum", "sat", "hue", "alpha", "invert", "blend", "contrast" } },

            // ────── Palette ──────
            new() { Category = "Palette", Name = "bp", Sig = "bp(type[, source])",
                    Desc = "Extract palette colors from images or media covers.",
                    Insert = "bp(dominant)", Params = new[] { "dominant", "vibrant", "muted", "light", "dark" } },

            // ────── Variables ──────
            new() { Category = "Variables", Name = "gv", Sig = "gv(scope, name[, index])",
                    Desc = "Read global/component variables. Scope 0=global, self=current component.",
                    Insert = "gv(0, accent)" },

            // ────── External ──────
            new() { Category = "External", Name = "wg", Sig = "wg(url[, index, field])",
                    Desc = "Fetch RSS feed. Returns title, link, or description of the Nth item.",
                    Insert = "wg(https://example.com/feed.xml, 1, title)" },
        };
    }
}
