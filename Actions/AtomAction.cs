using System;

namespace Lumen.Actions
{
    /// <summary>
    /// 原子点击动作类型（P5 行为系统）。可附加到任意原子，
    /// 桌面模式下左键点击即触发；编辑模式下点击用于拖拽、不触发。
    /// </summary>
    public enum ActionKind
    {
        None,
        RunApp,        // Arg = 开始菜单应用名 或 .lnk/可执行/URL 路径
        MediaControl,  // Arg = play | pause | next | prev | stop
        SwitchPage,    // Arg = 页索引(0基) 或 +1 / -1（相对切页）
        ToggleEditMode,
        OpenSettings,
        LockScreen,
        OpenURL,       // Arg = 网址
        Command,       // Arg = 命令行（cmd /c ...）
        SwitchPreset   // Arg = 预设名(Day/Night/...) 或 +1 / -1（循环套用到全部页面）
    }

    /// <summary>
    /// 单个点击动作：类型 + 参数。序列化为 "Kind|Arg"（Arg 内 '|' 转义为 "\|"）。
    /// </summary>
    public class AtomAction
    {
        public ActionKind Kind = ActionKind.None;
        public string Arg = "";

        public static AtomAction None() => new AtomAction();

        public string Serialize()
        {
            if (Kind == ActionKind.None) return "";
            var arg = (Arg ?? "").Replace("\\", "\\\\").Replace("|", "\\|");
            return Kind.ToString() + "|" + arg;
        }

        public static AtomAction Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return None();
            var i = s.IndexOf('|');
            if (i < 0)
            {
                return Enum.TryParse<ActionKind>(s.Trim(), out ActionKind k0) ? new AtomAction { Kind = k0 } : None();
            }
            var kindStr = s.Substring(0, i).Trim();
            var arg = s.Substring(i + 1).Replace("\\|", "|").Replace("\\\\", "\\");
            return Enum.TryParse<ActionKind>(kindStr, out var k) ? new AtomAction { Kind = k, Arg = arg } : None();
        }
    }
}
