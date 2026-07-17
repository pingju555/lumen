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
            // ---------- 时间 ----------
            new() { Category = Loc.T("func.cat.time"),   Name = "df",  Sig = "df(fmt)",            Desc = Loc.T("func.df"), Insert = "df(HH:mm)" },
            new() { Category = Loc.T("func.cat.time"),   Name = "tf",  Sig = "tf(secs)",           Desc = Loc.T("func.tf"), Insert = "tf(125)" },
            new() { Category = Loc.T("func.cat.time"),   Name = "ts",  Sig = "ts([fmt])",          Desc = Loc.T("func.ts"), Insert = "ts(HH:mm:ss)" },
            new() { Category = Loc.T("func.cat.time"),   Name = "tu",  Sig = "tu()",               Desc = Loc.T("func.tu"), Insert = "tu()" },
            new() { Category = Loc.T("func.cat.time"),   Name = "tz",  Sig = "tz()",               Desc = Loc.T("func.tz"), Insert = "tz()" },
            // ---------- 系统 ----------
            new() { Category = Loc.T("func.cat.system"), Name = "si",  Sig = "si(key)",            Desc = Loc.T("func.si"), Insert = "si(cpu)" },
            new() { Category = Loc.T("func.cat.system"), Name = "bi",  Sig = "bi(key)",            Desc = Loc.T("func.bi"), Insert = "bi(level)" },
            new() { Category = Loc.T("func.cat.system"), Name = "dp",  Sig = "dp(px)",             Desc = Loc.T("func.dp"), Insert = "dp(100)" },
            // ---------- 变量 ----------
            new() { Category = Loc.T("func.cat.var"),    Name = "gv",  Sig = "gv(scope,name[,N])", Desc = Loc.T("func.gv"), Insert = "gv(0, accent)" },
            // ---------- 逻辑 ----------
            new() { Category = Loc.T("func.cat.logic"),  Name = "if",  Sig = "if(cond,a,b)",       Desc = Loc.T("func.if"), Insert = "if(1>0, 高, 低)" },
            // ---------- 文本 ----------
            new() { Category = Loc.T("func.cat.text"),   Name = "tc",  Sig = "tc(cmd,text[,n])",   Desc = Loc.T("func.tc"), Insert = "tc(ell, 文本, 10)" },
            new() { Category = Loc.T("func.cat.text"),   Name = "uc",  Sig = "uc(text)",           Desc = Loc.T("func.uc"), Insert = "uc(hello)" },
            new() { Category = Loc.T("func.cat.text"),   Name = "re",  Sig = "re(text,pat,repl)",  Desc = Loc.T("func.re"), Insert = "re(文本, \\d+, #)" },
            // ---------- 媒体 / 数学 / 颜色 / 外部数据 ----------
            new() { Category = Loc.T("func.cat.media"),  Name = "mi",  Sig = "mi(key)",            Desc = Loc.T("func.mi"), Insert = "mi(title)" },
            new() { Category = Loc.T("func.cat.math"),   Name = "mu",  Sig = "mu(op,x[,y,z])",     Desc = Loc.T("func.mu"), Insert = "mu(round, 3.14)" },
            new() { Category = Loc.T("func.cat.color"),  Name = "ce",  Sig = "ce(op,color[,amt])", Desc = Loc.T("func.ce"), Insert = "ce(lum, #3399FF, 20)" },
            new() { Category = Loc.T("func.cat.color"),  Name = "bp",  Sig = "bp(type[,source])",  Desc = Loc.T("func.bp"), Insert = "bp(vibrant, cover)" },
            new() { Category = Loc.T("func.cat.data"),   Name = "wg",  Sig = "wg(url[,n,field])",  Desc = Loc.T("func.wg"), Insert = "wg(https://example.com/feed.xml)" },
            // ---------- 杂项 ----------
            new() { Category = Loc.T("func.cat.misc"),   Name = "fl",  Sig = "fl(idx,...)",        Desc = Loc.T("func.fl"), Insert = "fl(2, a, b, c)" },
            new() { Category = Loc.T("func.cat.misc"),   Name = "rng", Sig = "rng([min,max])",     Desc = Loc.T("func.rng"), Insert = "rng(0,100)" },
        };
    }
}
