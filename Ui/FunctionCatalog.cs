using System.Collections.Generic;

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
            new() { Category = "时间", Name = "df",  Sig = "df(fmt)",            Desc = "当前时间，按 KLWP 风格格式，如 HH:mm / yyyy-MM-dd", Insert = "df(HH:mm)" },
            new() { Category = "时间", Name = "tf",  Sig = "tf(secs)",           Desc = "秒数 → h:mm:ss 时长", Insert = "tf(125)" },
            new() { Category = "时间", Name = "ts",  Sig = "ts([fmt])",          Desc = "时间戳(秒) 或按格式的时间", Insert = "ts(HH:mm:ss)" },
            new() { Category = "时间", Name = "tu",  Sig = "tu()",               Desc = "UTC 时间串 yyyy-MM-dd HH:mm:ss", Insert = "tu()" },
            new() { Category = "时间", Name = "tz",  Sig = "tz()",               Desc = "本地时区偏移（小时，如 +8）", Insert = "tz()" },
            // ---------- 系统 ----------
            new() { Category = "系统", Name = "si",  Sig = "si(key)",            Desc = "系统指标(实时PDH)：cpu / mem / memused / memtotal / diskfree / disktotal / diskp / netup / netdown / rwidth / rheight / density / dark", Insert = "si(cpu)" },
            new() { Category = "系统", Name = "bi",  Sig = "bi(key)",            Desc = "电池：level(电量%) / plugged(充电) / charging", Insert = "bi(level)" },
            new() { Category = "系统", Name = "dp",  Sig = "dp(px)",             Desc = "DPI 缩放：px × 当前屏幕 DPI", Insert = "dp(100)" },
            // ---------- 变量 ----------
            new() { Category = "变量", Name = "gv",  Sig = "gv(name[, default])", Desc = "读取全局变量（不存在时取 default）", Insert = "gv(accent)" },
            // ---------- 逻辑 ----------
            new() { Category = "逻辑", Name = "if",  Sig = "if(cond,a,b)",       Desc = "条件：cond 为真取 a，否则取 b", Insert = "if(1>0, 高, 低)" },
            // ---------- 文本 ----------
            new() { Category = "文本", Name = "tc",  Sig = "tc(cmd,text[,n])",   Desc = "文本处理：cut(n)/ell(n)/reg(pat,repl)/up/low/cap", Insert = "tc(ell, 文本, 10)" },
            new() { Category = "文本", Name = "uc",  Sig = "uc(text)",           Desc = "转大写", Insert = "uc(hello)" },
            new() { Category = "文本", Name = "re",  Sig = "re(text,pat,repl)",  Desc = "正则替换 text 中 pat 为 repl", Insert = "re(文本, \\d+, #)" },
            // ---------- 媒体 ----------
            new() { Category = "媒体", Name = "mi",  Sig = "mi(key)",            Desc = "媒体信息(SMTC)：title / artist / album / app / playing / pos / dur / avail", Insert = "mi(title)" },
            new() { Category = "媒体", Name = "mu",  Sig = "mu(cmd)",            Desc = "媒体控制：play / pause / next / prev / stop", Insert = "mu(play)" },
            // ---------- 应用 ----------
            new() { Category = "应用", Name = "ai",  Sig = "ai([n])",            Desc = "应用：无参=应用数量；ai(n)=第 n 个应用名(1起)", Insert = "ai(1)" },
            new() { Category = "应用", Name = "an",  Sig = "an(n)",              Desc = "启动第 n 个应用(1起)", Insert = "an(1)" },
            // ---------- 杂项 ----------
            new() { Category = "杂项", Name = "fl",  Sig = "fl(idx,...)",        Desc = "取第 idx 个参数（idx 从 1 起）", Insert = "fl(2, a, b, c)" },
            new() { Category = "杂项", Name = "rng", Sig = "rng([min,max])",     Desc = "随机小数（无参 0~1；两参 min~max）", Insert = "rng(0,100)" },
        };
    }
}
