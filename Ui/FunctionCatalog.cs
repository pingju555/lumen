using System.Collections.Generic;
using Lumen.I18n;

namespace Lumen.Ui
{
    /// <summary>公式函数目录（供属性编辑器「插入函数」使用）。与 Formula/FunctionRegistry 的 19 函数一一对应。</summary>
    public class FunctionCatalog
    {
        public string Category; // 函数分类（按类型划分的二级菜单）
        public string Name;
        public string Sig;    // 显示签名
        public string Desc;   // 中文说明
        public string Insert; // 插入到公式框的内联文本（不含外层 $）

        public static readonly List<FunctionCatalog> All = new()
        {
            // ────────────────────── 时间日期 ──────────────────────
            new() { Category = Loc.T("func.cat.time"),   Name = "df",  Sig = "df(格式字符串)",
                    Desc = Loc.T("func.df"), Insert = "df(HH:mm:ss)" },
            new() { Category = Loc.T("func.cat.time"),   Name = "tf",  Sig = "tf(秒数)",
                    Desc = Loc.T("func.tf"), Insert = "tf(3661)" },
            new() { Category = Loc.T("func.cat.time"),   Name = "ts",  Sig = "ts([格式])",
                    Desc = Loc.T("func.ts"), Insert = "ts(yyyy-MM-dd)" },
            new() { Category = Loc.T("func.cat.time"),   Name = "tu",  Sig = "tu()",
                    Desc = Loc.T("func.tu"), Insert = "tu()" },
            new() { Category = Loc.T("func.cat.time"),   Name = "tz",  Sig = "tz()",
                    Desc = Loc.T("func.tz"), Insert = "tz()" },

            // ────────────────────── 系统 ──────────────────────
            new() { Category = Loc.T("func.cat.system"), Name = "si",  Sig = "si(指标名)",
                    Desc = Loc.T("func.si"), Insert = "si(cpu)" },
            new() { Category = Loc.T("func.cat.system"), Name = "bi",  Sig = "bi(字段名)",
                    Desc = Loc.T("func.bi"), Insert = "bi(level)" },
            new() { Category = Loc.T("func.cat.system"), Name = "dp",  Sig = "dp(px值)",
                    Desc = Loc.T("func.dp"), Insert = "dp(100)" },

            // ────────────────────── 变量 ──────────────────────
            new() { Category = Loc.T("func.cat.var"),    Name = "gv",  Sig = "gv(scope,name[,索引])",
                    Desc = Loc.T("func.gv"), Insert = "gv(0, accent)" },

            // ────────────────────── 逻辑判断 ──────────────────────
            new() { Category = Loc.T("func.cat.logic"),  Name = "if",  Sig = "if(条件, 真值, 假值)",
                    Desc = Loc.T("func.if"), Insert = "if(si(cpu)>50, 繁忙, 空闲)" },
            new() { Category = Loc.T("func.cat.logic"),  Name = "fl",  Sig = "fl(索引, ..., ...)",
                    Desc = Loc.T("func.fl"), Insert = "fl(2, 选项A, 选项B, 选项C)" },

            // ────────────────────── 文本 ──────────────────────
            new() { Category = Loc.T("func.cat.text"),   Name = "tc",  Sig = "tc(命令, 文本[, 参数])",
                    Desc = Loc.T("func.tc"), Insert = "tc(ell, 这是一个长文本, 8)" },
            new() { Category = Loc.T("func.cat.text"),   Name = "uc",  Sig = "uc(文本)",
                    Desc = Loc.T("func.uc"), Insert = "uc(hello)" },
            new() { Category = Loc.T("func.cat.text"),   Name = "re",  Sig = "re(文本, 正则, 替换为)",
                    Desc = Loc.T("func.re"), Insert = "re(abc123, \\\\d+, #)" },

            // ────────────────────── 数学运算 ──────────────────────
            new() { Category = Loc.T("func.cat.math"),   Name = "mu",  Sig = "mu(运算, x[, y, z])",
                    Desc = Loc.T("func.mu"), Insert = "mu(round, 3.14159, 2)" },
            new() { Category = Loc.T("func.cat.math"),   Name = "rng", Sig = "rng([最小值, 最大值])",
                    Desc = Loc.T("func.rng"), Insert = "rng(100, 999)" },

            // ────────────────────── 媒体 ──────────────────────
            new() { Category = Loc.T("func.cat.media"),  Name = "mi",  Sig = "mi(字段名)",
                    Desc = Loc.T("func.mi"), Insert = "mi(title)" },

            // ────────────────────── 颜色 ──────────────────────
            new() { Category = Loc.T("func.cat.color"),  Name = "ce",  Sig = "ce(运算, 颜色[, 幅度])",
                    Desc = Loc.T("func.ce"), Insert = "ce(lum, #3399FF, 20)" },
            new() { Category = Loc.T("func.cat.color"),  Name = "bp",  Sig = "bp(类型[, 来源])",
                    Desc = Loc.T("func.bp"), Insert = "bp(dominant, mi(cover))" },

            // ────────────────────── 外部数据 ──────────────────────
            new() { Category = Loc.T("func.cat.data"),   Name = "wg",  Sig = "wg(url[, 索引, 字段])",
                    Desc = Loc.T("func.wg"), Insert = "wg(https://example.com/feed.xml, 1, title)" },
        };
    }
}
